using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Reflection;

using Pek.Extension;

namespace Pek.Http;

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

        ParameterInfo? errorParameter = null;
        foreach (var constructor in type.GetConstructors().OrderByDescending(item => item.GetParameters().Length))
        {
            var parameters = constructor.GetParameters();
            var values = new Object?[parameters.Length];
            var success = true;
            for (var i = 0; i < parameters.Length; i++)
            {
                var service = serviceProvider?.GetService(parameters[i].ParameterType);
                if (service == null)
                {
                    success = false;
                    errorParameter = parameters[i];
                    break;
                }

                values[i] = service;
            }

            if (success) return constructor.Invoke(values);
        }

        if (type.GetConstructor(Type.EmptyTypes) != null) return Activator.CreateInstance(type);

        throw new InvalidOperationException($"No suitable constructor was found for '{type}'. Unable to resolve parameter '{errorParameter}'");
    }
}

/// <summary>控制器Action注册表</summary>
public static class ControllerActionRegistry
{
    private static readonly ConcurrentDictionary<Type, ConcurrentDictionary<String, Func<Object, IHttpContext, ValueTask<Object?>>>> _actions = new();

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
}