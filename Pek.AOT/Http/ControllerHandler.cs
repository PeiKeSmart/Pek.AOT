using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Reflection;

using Pek.Buffers;
using Pek.Data;
using Pek.Extension;
using Pek.Net;
using Pek.Model;
using Pek.Serialization;

namespace Pek.Http;

/// <summary>Http参数绑定委托</summary>
/// <typeparam name="T">参数类型</typeparam>
/// <param name="context">Http上下文</param>
/// <returns>绑定结果</returns>
public delegate T HttpParameterBinder<out T>(IHttpContext context);

/// <summary>控制器处理器</summary>
public class ControllerHandler : IHttpHandler
{
    /// <summary>控制器类型</summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type? ControllerType { get; set; }

    /// <summary>处理请求</summary>
    /// <param name="context">Http上下文</param>
    public virtual void ProcessRequest(IHttpContext context)
    {
        var type = ControllerType;
        if (type == null) return;

        var sections = context.Path.Split('/');
        var actionName = sections.Length >= 3 ? sections[2] : null;
        if (actionName.IsNullOrEmpty())
        {
            context.Response.StatusCode = HttpStatusCode.NotFound;
            context.Response.StatusDescription = $"Cannot find operation [{actionName}] within controller [{type.FullName}]";
            return;
        }

        var action = ControllerActionRegistry.Resolve(type, actionName);
        if (action == null)
        {
            context.Response.StatusCode = HttpStatusCode.NotFound;
            context.Response.StatusDescription = $"Cannot find operation [{actionName}] within controller [{type.FullName}]";
            return;
        }

        var controller = ResolveController(context, type);
        var result = action(controller, context).GetAwaiter().GetResult();
        if (result != null) context.Response.SetResult(result);
    }

    /// <summary>解析控制器实例</summary>
    /// <param name="context">Http上下文</param>
    /// <param name="type">控制器类型</param>
    /// <returns>控制器实例</returns>
    protected virtual Object ResolveController(IHttpContext context, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type)
    {
        var serviceProvider = context.ServiceProvider;
        var controller = serviceProvider?.GetService(type);
        if (controller != null) return controller;

        controller = CreateController(type, serviceProvider);
        if (controller != null) return controller;

        throw new InvalidOperationException($"Cannot create controller [{type.FullName}] in AOT-safe ControllerHandler");
    }

    private static Object? CreateController([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type, IServiceProvider? serviceProvider)
    {
        if (type.IsAbstract) return null;

        serviceProvider ??= ObjectContainer.Provider;
        return ObjectContainer.CreateInstance(type, serviceProvider, null, false);
    }
}

/// <summary>控制器Action注册表</summary>
public static class ControllerActionRegistry
{
    private static readonly ConcurrentDictionary<Type, ConcurrentDictionary<String, Func<Object, IHttpContext, ValueTask<Object?>>>> _actions = new();

    /// <summary>按参数名绑定</summary>
    /// <typeparam name="T">参数类型</typeparam>
    /// <param name="parameterName">参数名</param>
    /// <returns>绑定委托</returns>
    public static HttpParameterBinder<T> FromParameter<T>(String parameterName) => context => (T)ConvertParameter(context, parameterName, typeof(T))!;

    /// <summary>从请求体绑定</summary>
    /// <typeparam name="T">参数类型</typeparam>
    /// <returns>绑定委托</returns>
    public static HttpParameterBinder<T> FromBody<T>() => context => (T)ConvertBody(context, typeof(T))!;

    /// <summary>从服务容器绑定</summary>
    /// <typeparam name="T">参数类型</typeparam>
    /// <returns>绑定委托</returns>
    public static HttpParameterBinder<T> FromService<T>() => context => (T)(context.ServiceProvider?.GetService(typeof(T)) ?? throw new InvalidOperationException($"Cannot resolve service [{typeof(T).FullName}] from IHttpContext.ServiceProvider"));

    /// <summary>从上下文绑定</summary>
    /// <typeparam name="T">参数类型</typeparam>
    /// <returns>绑定委托</returns>
    public static HttpParameterBinder<T> FromContext<T>() => context => (T)(ResolveContextValue(context, typeof(T)) ?? throw new InvalidOperationException($"Type [{typeof(T).FullName}] is not available from IHttpContext"));

    /// <summary>注册无返回值Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="handler">处理器</param>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController>(String actionName, Action<TController> handler) where TController : class
        => RegisterCore<TController>(actionName, (controller, context) =>
        {
            handler(controller);
            return ValueTask.FromResult<Object?>(null);
        });

    /// <summary>注册带上下文的无返回值Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="handler">处理器</param>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController>(String actionName, Action<TController, IHttpContext> handler) where TController : class
        => RegisterCore<TController>(actionName, (controller, context) =>
        {
            handler(controller, context);
            return ValueTask.FromResult<Object?>(null);
        });

    /// <summary>注册有返回值Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="handler">处理器</param>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController>(String actionName, Func<TController, Object?> handler) where TController : class
        => RegisterCore<TController>(actionName, (controller, context) => ValueTask.FromResult(handler(controller)));

    /// <summary>注册带上下文的有返回值Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="handler">处理器</param>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController>(String actionName, Func<TController, IHttpContext, Object?> handler) where TController : class
        => RegisterCore<TController>(actionName, (controller, context) => ValueTask.FromResult(handler(controller, context)));

    /// <summary>注册异步无返回值Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="handler">处理器</param>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController>(String actionName, Func<TController, Task> handler) where TController : class
        => RegisterCore<TController>(actionName, async (controller, context) =>
        {
            await handler(controller).ConfigureAwait(false);
            return null;
        });

    /// <summary>注册带上下文的异步无返回值Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="handler">处理器</param>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController>(String actionName, Func<TController, IHttpContext, Task> handler) where TController : class
        => RegisterCore<TController>(actionName, async (controller, context) =>
        {
            await handler(controller, context).ConfigureAwait(false);
            return null;
        });

    /// <summary>注册异步有返回值Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="handler">处理器</param>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController>(String actionName, Func<TController, Task<Object?>> handler) where TController : class
        => RegisterCore<TController>(actionName, async (controller, context) => await handler(controller).ConfigureAwait(false));

    /// <summary>注册带上下文的异步有返回值Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="handler">处理器</param>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController>(String actionName, Func<TController, IHttpContext, Task<Object?>> handler) where TController : class
        => RegisterCore<TController>(actionName, async (controller, context) => await handler(controller, context).ConfigureAwait(false));

    /// <summary>注册ValueTask无返回值Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="handler">处理器</param>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController>(String actionName, Func<TController, ValueTask> handler) where TController : class
        => RegisterCore<TController>(actionName, async (controller, context) =>
        {
            await handler(controller).ConfigureAwait(false);
            return null;
        });

    /// <summary>注册带上下文的ValueTask无返回值Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="handler">处理器</param>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController>(String actionName, Func<TController, IHttpContext, ValueTask> handler) where TController : class
        => RegisterCore<TController>(actionName, async (controller, context) =>
        {
            await handler(controller, context).ConfigureAwait(false);
            return null;
        });

    /// <summary>注册ValueTask有返回值Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="handler">处理器</param>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController>(String actionName, Func<TController, ValueTask<Object?>> handler) where TController : class
        => RegisterCore<TController>(actionName, (controller, context) => handler(controller));

    /// <summary>注册带上下文的ValueTask有返回值Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="handler">处理器</param>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController>(String actionName, Func<TController, IHttpContext, ValueTask<Object?>> handler) where TController : class
        => RegisterCore<TController>(actionName, (controller, context) => handler(controller, context));

    /// <summary>注册单参数无返回值Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="binder1">参数1绑定器</param>
    /// <param name="handler">处理器</param>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1>(String actionName, HttpParameterBinder<T1> binder1, Action<TController, T1> handler) where TController : class
        => RegisterCore<TController>(actionName, (controller, context) =>
        {
            handler(controller, binder1(context));
            return ValueTask.FromResult<Object?>(null);
        });

    /// <summary>注册单参数有返回值Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="binder1">参数1绑定器</param>
    /// <param name="handler">处理器</param>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1>(String actionName, HttpParameterBinder<T1> binder1, Func<TController, T1, Object?> handler) where TController : class
        => RegisterCore<TController>(actionName, (controller, context) => ValueTask.FromResult(handler(controller, binder1(context))));

    /// <summary>注册单参数异步有返回值Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="binder1">参数1绑定器</param>
    /// <param name="handler">处理器</param>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1>(String actionName, HttpParameterBinder<T1> binder1, Func<TController, T1, Task<Object?>> handler) where TController : class
        => RegisterCore<TController>(actionName, async (controller, context) => await handler(controller, binder1(context)).ConfigureAwait(false));

    /// <summary>注册单参数ValueTask有返回值Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="binder1">参数1绑定器</param>
    /// <param name="handler">处理器</param>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1>(String actionName, HttpParameterBinder<T1> binder1, Func<TController, T1, ValueTask<Object?>> handler) where TController : class
        => RegisterCore<TController>(actionName, (controller, context) => handler(controller, binder1(context)));

    /// <summary>注册双参数无返回值Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <typeparam name="T2">参数2类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="binder1">参数1绑定器</param>
    /// <param name="binder2">参数2绑定器</param>
    /// <param name="handler">处理器</param>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, T2>(String actionName, HttpParameterBinder<T1> binder1, HttpParameterBinder<T2> binder2, Action<TController, T1, T2> handler) where TController : class
        => RegisterCore<TController>(actionName, (controller, context) =>
        {
            handler(controller, binder1(context), binder2(context));
            return ValueTask.FromResult<Object?>(null);
        });

    /// <summary>注册双参数有返回值Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <typeparam name="T2">参数2类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="binder1">参数1绑定器</param>
    /// <param name="binder2">参数2绑定器</param>
    /// <param name="handler">处理器</param>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, T2>(String actionName, HttpParameterBinder<T1> binder1, HttpParameterBinder<T2> binder2, Func<TController, T1, T2, Object?> handler) where TController : class
        => RegisterCore<TController>(actionName, (controller, context) => ValueTask.FromResult(handler(controller, binder1(context), binder2(context))));

    /// <summary>注册双参数异步有返回值Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <typeparam name="T2">参数2类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="binder1">参数1绑定器</param>
    /// <param name="binder2">参数2绑定器</param>
    /// <param name="handler">处理器</param>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, T2>(String actionName, HttpParameterBinder<T1> binder1, HttpParameterBinder<T2> binder2, Func<TController, T1, T2, Task<Object?>> handler) where TController : class
        => RegisterCore<TController>(actionName, async (controller, context) => await handler(controller, binder1(context), binder2(context)).ConfigureAwait(false));

    /// <summary>注册双参数ValueTask有返回值Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <typeparam name="T2">参数2类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="binder1">参数1绑定器</param>
    /// <param name="binder2">参数2绑定器</param>
    /// <param name="handler">处理器</param>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, T2>(String actionName, HttpParameterBinder<T1> binder1, HttpParameterBinder<T2> binder2, Func<TController, T1, T2, ValueTask<Object?>> handler) where TController : class
        => RegisterCore<TController>(actionName, (controller, context) => handler(controller, binder1(context), binder2(context)));

    /// <summary>注册三参数无返回值Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <typeparam name="T2">参数2类型</typeparam>
    /// <typeparam name="T3">参数3类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="binder1">参数1绑定器</param>
    /// <param name="binder2">参数2绑定器</param>
    /// <param name="binder3">参数3绑定器</param>
    /// <param name="handler">处理器</param>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, T2, T3>(String actionName, HttpParameterBinder<T1> binder1, HttpParameterBinder<T2> binder2, HttpParameterBinder<T3> binder3, Action<TController, T1, T2, T3> handler) where TController : class
        => RegisterCore<TController>(actionName, (controller, context) =>
        {
            handler(controller, binder1(context), binder2(context), binder3(context));
            return ValueTask.FromResult<Object?>(null);
        });

    /// <summary>注册三参数有返回值Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <typeparam name="T2">参数2类型</typeparam>
    /// <typeparam name="T3">参数3类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="binder1">参数1绑定器</param>
    /// <param name="binder2">参数2绑定器</param>
    /// <param name="binder3">参数3绑定器</param>
    /// <param name="handler">处理器</param>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, T2, T3>(String actionName, HttpParameterBinder<T1> binder1, HttpParameterBinder<T2> binder2, HttpParameterBinder<T3> binder3, Func<TController, T1, T2, T3, Object?> handler) where TController : class
        => RegisterCore<TController>(actionName, (controller, context) => ValueTask.FromResult(handler(controller, binder1(context), binder2(context), binder3(context))));

    /// <summary>注册三参数异步有返回值Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <typeparam name="T2">参数2类型</typeparam>
    /// <typeparam name="T3">参数3类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="binder1">参数1绑定器</param>
    /// <param name="binder2">参数2绑定器</param>
    /// <param name="binder3">参数3绑定器</param>
    /// <param name="handler">处理器</param>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, T2, T3>(String actionName, HttpParameterBinder<T1> binder1, HttpParameterBinder<T2> binder2, HttpParameterBinder<T3> binder3, Func<TController, T1, T2, T3, Task<Object?>> handler) where TController : class
        => RegisterCore<TController>(actionName, async (controller, context) => await handler(controller, binder1(context), binder2(context), binder3(context)).ConfigureAwait(false));

    /// <summary>注册三参数ValueTask有返回值Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <typeparam name="T2">参数2类型</typeparam>
    /// <typeparam name="T3">参数3类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="binder1">参数1绑定器</param>
    /// <param name="binder2">参数2绑定器</param>
    /// <param name="binder3">参数3绑定器</param>
    /// <param name="handler">处理器</param>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, T2, T3>(String actionName, HttpParameterBinder<T1> binder1, HttpParameterBinder<T2> binder2, HttpParameterBinder<T3> binder3, Func<TController, T1, T2, T3, ValueTask<Object?>> handler) where TController : class
        => RegisterCore<TController>(actionName, (controller, context) => handler(controller, binder1(context), binder2(context), binder3(context)));

    /// <summary>注册四参数无返回值Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <typeparam name="T2">参数2类型</typeparam>
    /// <typeparam name="T3">参数3类型</typeparam>
    /// <typeparam name="T4">参数4类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="binder1">参数1绑定器</param>
    /// <param name="binder2">参数2绑定器</param>
    /// <param name="binder3">参数3绑定器</param>
    /// <param name="binder4">参数4绑定器</param>
    /// <param name="handler">处理器</param>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, T2, T3, T4>(String actionName, HttpParameterBinder<T1> binder1, HttpParameterBinder<T2> binder2, HttpParameterBinder<T3> binder3, HttpParameterBinder<T4> binder4, Action<TController, T1, T2, T3, T4> handler) where TController : class
        => RegisterCore<TController>(actionName, (controller, context) =>
        {
            handler(controller, binder1(context), binder2(context), binder3(context), binder4(context));
            return ValueTask.FromResult<Object?>(null);
        });

    /// <summary>注册四参数有返回值Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <typeparam name="T2">参数2类型</typeparam>
    /// <typeparam name="T3">参数3类型</typeparam>
    /// <typeparam name="T4">参数4类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="binder1">参数1绑定器</param>
    /// <param name="binder2">参数2绑定器</param>
    /// <param name="binder3">参数3绑定器</param>
    /// <param name="binder4">参数4绑定器</param>
    /// <param name="handler">处理器</param>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, T2, T3, T4>(String actionName, HttpParameterBinder<T1> binder1, HttpParameterBinder<T2> binder2, HttpParameterBinder<T3> binder3, HttpParameterBinder<T4> binder4, Func<TController, T1, T2, T3, T4, Object?> handler) where TController : class
        => RegisterCore<TController>(actionName, (controller, context) => ValueTask.FromResult(handler(controller, binder1(context), binder2(context), binder3(context), binder4(context))));

    /// <summary>注册四参数异步有返回值Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <typeparam name="T2">参数2类型</typeparam>
    /// <typeparam name="T3">参数3类型</typeparam>
    /// <typeparam name="T4">参数4类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="binder1">参数1绑定器</param>
    /// <param name="binder2">参数2绑定器</param>
    /// <param name="binder3">参数3绑定器</param>
    /// <param name="binder4">参数4绑定器</param>
    /// <param name="handler">处理器</param>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, T2, T3, T4>(String actionName, HttpParameterBinder<T1> binder1, HttpParameterBinder<T2> binder2, HttpParameterBinder<T3> binder3, HttpParameterBinder<T4> binder4, Func<TController, T1, T2, T3, T4, Task<Object?>> handler) where TController : class
        => RegisterCore<TController>(actionName, async (controller, context) => await handler(controller, binder1(context), binder2(context), binder3(context), binder4(context)).ConfigureAwait(false));

    /// <summary>注册四参数ValueTask有返回值Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <typeparam name="T2">参数2类型</typeparam>
    /// <typeparam name="T3">参数3类型</typeparam>
    /// <typeparam name="T4">参数4类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="binder1">参数1绑定器</param>
    /// <param name="binder2">参数2绑定器</param>
    /// <param name="binder3">参数3绑定器</param>
    /// <param name="binder4">参数4绑定器</param>
    /// <param name="handler">处理器</param>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, T2, T3, T4>(String actionName, HttpParameterBinder<T1> binder1, HttpParameterBinder<T2> binder2, HttpParameterBinder<T3> binder3, HttpParameterBinder<T4> binder4, Func<TController, T1, T2, T3, T4, ValueTask<Object?>> handler) where TController : class
        => RegisterCore<TController>(actionName, (controller, context) => handler(controller, binder1(context), binder2(context), binder3(context), binder4(context)));

    internal static Func<Object, IHttpContext, ValueTask<Object?>>? Resolve(Type controllerType, String actionName)
    {
        if (_actions.TryGetValue(controllerType, out var actions) && actions.TryGetValue(actionName, out var action)) return action;
        return null;
    }

    private static void RegisterCore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController>(String actionName, Func<TController, IHttpContext, ValueTask<Object?>> handler) where TController : class
    {
        if (actionName.IsNullOrEmpty()) throw new ArgumentNullException(nameof(actionName));
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        var actions = _actions.GetOrAdd(typeof(TController), _ => new ConcurrentDictionary<String, Func<Object, IHttpContext, ValueTask<Object?>>>(StringComparer.OrdinalIgnoreCase));
        actions[actionName] = (controller, context) => handler((TController)controller, context);
    }

    private static Object? ConvertParameter(IHttpContext context, String parameterName, Type targetType)
    {
        if (parameterName.IsNullOrEmpty()) throw new ArgumentNullException(nameof(parameterName));
        if (!context.Parameters.TryGetValue(parameterName, out var value))
            throw new InvalidOperationException($"Required parameter [{parameterName}] was not found for type [{targetType.FullName}]");

        return ConvertValue(value, targetType, parameterName);
    }

    private static Object? ConvertBody(IHttpContext context, Type targetType)
    {
        var value = ResolveContextValue(context, targetType);
        if (value != null) return value;

        var body = context.Request.Body;
        if (targetType == typeof(Byte[])) return body?.ToArray();
        if (typeof(IPacket).IsAssignableFrom(targetType)) return body;

        if (targetType == typeof(String))
        {
            if (body == null || body.Length == 0) return String.Empty;
            return body.ToStr();
        }

        if (body == null || body.Length == 0)
        {
            if (context.Parameters.Count == 0)
                throw new InvalidOperationException($"Request body is empty and cannot bind type [{targetType.FullName}]");

            return ConvertValue(context.Parameters, targetType, "$body");
        }

        var text = body.ToStr();
        if (text.IsNullOrWhiteSpace())
        {
            if (context.Parameters.Count == 0)
                throw new InvalidOperationException($"Request body is empty and cannot bind type [{targetType.FullName}]");

            return ConvertValue(context.Parameters, targetType, "$body");
        }

        if (text.TrimStart().StartsWith("{", StringComparison.Ordinal) || text.TrimStart().StartsWith("[", StringComparison.Ordinal) || text.Trim() == "null")
            return JsonHelper.Default.Read(text, targetType);

        return ConvertValue(text, targetType, "$body");
    }

    private static Object? ConvertValue(Object? value, Type targetType, String sourceName)
    {
        var actualType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (value == null)
        {
            if (!actualType.IsValueType || Nullable.GetUnderlyingType(targetType) != null) return null;
            throw new InvalidOperationException($"Source [{sourceName}] is null and cannot bind non-nullable type [{targetType.FullName}]");
        }

        if (actualType.IsInstanceOfType(value)) return value;

        try
        {
            return JsonHelper.Default.Convert(value, targetType);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to bind source [{sourceName}] to type [{targetType.FullName}]", ex);
        }
    }

    private static Object? ResolveContextValue(IHttpContext context, Type targetType)
    {
        if (targetType == typeof(IHttpContext) || targetType == context.GetType()) return context;
        if (targetType == typeof(HttpRequest)) return context.Request;
        if (targetType == typeof(HttpResponse)) return context.Response;
        if (targetType == typeof(INetSession)) return context.Connection;
        if (targetType == typeof(ISocketRemote)) return context.Socket;
        if (targetType == typeof(WebSocket)) return context.WebSocket;
        if (targetType == typeof(IServiceProvider)) return context.ServiceProvider;

        return null;
    }
}