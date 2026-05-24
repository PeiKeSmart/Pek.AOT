using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Pek.Data;

namespace Pek.Remoting;

/// <summary>Api参数绑定委托</summary>
/// <typeparam name="T">参数类型</typeparam>
/// <param name="context">控制器上下文</param>
/// <returns>绑定结果</returns>
public delegate T ApiParameterBinder<out T>(ControllerContext context);

/// <summary>Api动作静态注册表</summary>
public static class ApiActionRegistry
{
    private static readonly ConcurrentDictionary<Type, ConcurrentDictionary<String, Func<Object, ControllerContext, ValueTask<Object?>>>> _actions = new();

    /// <summary>按参数名绑定</summary>
    /// <typeparam name="T">参数类型</typeparam>
    /// <param name="parameterName">参数名</param>
    /// <returns>绑定委托</returns>
    public static ApiParameterBinder<T> FromParameter<T>(String parameterName) => context => (T)ConvertParameter(context, parameterName, typeof(T))!;

    /// <summary>从控制器上下文绑定</summary>
    /// <typeparam name="T">参数类型</typeparam>
    /// <returns>绑定委托</returns>
    public static ApiParameterBinder<T> FromContext<T>() => context => (T)(ResolveContextValue(context, typeof(T)) ?? throw new InvalidOperationException($"Type [{typeof(T).FullName}] is not available from ControllerContext"));

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

    /// <summary>注册有返回值Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <typeparam name="TResult">返回类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="handler">处理器</param>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, TResult>(String actionName, Func<TController, TResult> handler) where TController : class
        => RegisterCore<TController>(actionName, (controller, context) => ValueTask.FromResult<Object?>(handler(controller)));

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

    /// <summary>注册异步有返回值Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <typeparam name="TResult">返回类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="handler">处理器</param>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, TResult>(String actionName, Func<TController, Task<TResult>> handler) where TController : class
        => RegisterCore<TController>(actionName, async (controller, context) => await handler(controller).ConfigureAwait(false));

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

    /// <summary>注册ValueTask有返回值Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <typeparam name="TResult">返回类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="handler">处理器</param>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, TResult>(String actionName, Func<TController, ValueTask<TResult>> handler) where TController : class
        => RegisterCore<TController>(actionName, async (controller, context) => await handler(controller).ConfigureAwait(false));

    /// <summary>注册单参数无返回值Action</summary>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1>(String actionName, ApiParameterBinder<T1> binder1, Action<TController, T1> handler) where TController : class
        => RegisterCore<TController>(actionName, (controller, context) =>
        {
            handler(controller, binder1(context));
            return ValueTask.FromResult<Object?>(null);
        });

    /// <summary>注册单参数有返回值Action</summary>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, TResult>(String actionName, ApiParameterBinder<T1> binder1, Func<TController, T1, TResult> handler) where TController : class
        => RegisterCore<TController>(actionName, (controller, context) => ValueTask.FromResult<Object?>(handler(controller, binder1(context))));

    /// <summary>注册单参数异步有返回值Action</summary>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, TResult>(String actionName, ApiParameterBinder<T1> binder1, Func<TController, T1, Task<TResult>> handler) where TController : class
        => RegisterCore<TController>(actionName, async (controller, context) => await handler(controller, binder1(context)).ConfigureAwait(false));

    /// <summary>注册单参数ValueTask有返回值Action</summary>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, TResult>(String actionName, ApiParameterBinder<T1> binder1, Func<TController, T1, ValueTask<TResult>> handler) where TController : class
        => RegisterCore<TController>(actionName, async (controller, context) => await handler(controller, binder1(context)).ConfigureAwait(false));

    /// <summary>注册双参数无返回值Action</summary>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, T2>(String actionName, ApiParameterBinder<T1> binder1, ApiParameterBinder<T2> binder2, Action<TController, T1, T2> handler) where TController : class
        => RegisterCore<TController>(actionName, (controller, context) =>
        {
            handler(controller, binder1(context), binder2(context));
            return ValueTask.FromResult<Object?>(null);
        });

    /// <summary>注册双参数有返回值Action</summary>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, T2, TResult>(String actionName, ApiParameterBinder<T1> binder1, ApiParameterBinder<T2> binder2, Func<TController, T1, T2, TResult> handler) where TController : class
        => RegisterCore<TController>(actionName, (controller, context) => ValueTask.FromResult<Object?>(handler(controller, binder1(context), binder2(context))));

    /// <summary>注册双参数异步有返回值Action</summary>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, T2, TResult>(String actionName, ApiParameterBinder<T1> binder1, ApiParameterBinder<T2> binder2, Func<TController, T1, T2, Task<TResult>> handler) where TController : class
        => RegisterCore<TController>(actionName, async (controller, context) => await handler(controller, binder1(context), binder2(context)).ConfigureAwait(false));

    /// <summary>注册双参数ValueTask有返回值Action</summary>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, T2, TResult>(String actionName, ApiParameterBinder<T1> binder1, ApiParameterBinder<T2> binder2, Func<TController, T1, T2, ValueTask<TResult>> handler) where TController : class
        => RegisterCore<TController>(actionName, async (controller, context) => await handler(controller, binder1(context), binder2(context)).ConfigureAwait(false));

    /// <summary>注册三参数无返回值Action</summary>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, T2, T3>(String actionName, ApiParameterBinder<T1> binder1, ApiParameterBinder<T2> binder2, ApiParameterBinder<T3> binder3, Action<TController, T1, T2, T3> handler) where TController : class
        => RegisterCore<TController>(actionName, (controller, context) =>
        {
            handler(controller, binder1(context), binder2(context), binder3(context));
            return ValueTask.FromResult<Object?>(null);
        });

    /// <summary>注册三参数有返回值Action</summary>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, T2, T3, TResult>(String actionName, ApiParameterBinder<T1> binder1, ApiParameterBinder<T2> binder2, ApiParameterBinder<T3> binder3, Func<TController, T1, T2, T3, TResult> handler) where TController : class
        => RegisterCore<TController>(actionName, (controller, context) => ValueTask.FromResult<Object?>(handler(controller, binder1(context), binder2(context), binder3(context))));

    /// <summary>注册三参数异步有返回值Action</summary>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, T2, T3, TResult>(String actionName, ApiParameterBinder<T1> binder1, ApiParameterBinder<T2> binder2, ApiParameterBinder<T3> binder3, Func<TController, T1, T2, T3, Task<TResult>> handler) where TController : class
        => RegisterCore<TController>(actionName, async (controller, context) => await handler(controller, binder1(context), binder2(context), binder3(context)).ConfigureAwait(false));

    /// <summary>注册三参数ValueTask有返回值Action</summary>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, T2, T3, TResult>(String actionName, ApiParameterBinder<T1> binder1, ApiParameterBinder<T2> binder2, ApiParameterBinder<T3> binder3, Func<TController, T1, T2, T3, ValueTask<TResult>> handler) where TController : class
        => RegisterCore<TController>(actionName, async (controller, context) => await handler(controller, binder1(context), binder2(context), binder3(context)).ConfigureAwait(false));

    /// <summary>注册四参数无返回值Action</summary>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, T2, T3, T4>(String actionName, ApiParameterBinder<T1> binder1, ApiParameterBinder<T2> binder2, ApiParameterBinder<T3> binder3, ApiParameterBinder<T4> binder4, Action<TController, T1, T2, T3, T4> handler) where TController : class
        => RegisterCore<TController>(actionName, (controller, context) =>
        {
            handler(controller, binder1(context), binder2(context), binder3(context), binder4(context));
            return ValueTask.FromResult<Object?>(null);
        });

    /// <summary>注册四参数有返回值Action</summary>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, T2, T3, T4, TResult>(String actionName, ApiParameterBinder<T1> binder1, ApiParameterBinder<T2> binder2, ApiParameterBinder<T3> binder3, ApiParameterBinder<T4> binder4, Func<TController, T1, T2, T3, T4, TResult> handler) where TController : class
        => RegisterCore<TController>(actionName, (controller, context) => ValueTask.FromResult<Object?>(handler(controller, binder1(context), binder2(context), binder3(context), binder4(context))));

    /// <summary>注册四参数异步有返回值Action</summary>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, T2, T3, T4, TResult>(String actionName, ApiParameterBinder<T1> binder1, ApiParameterBinder<T2> binder2, ApiParameterBinder<T3> binder3, ApiParameterBinder<T4> binder4, Func<TController, T1, T2, T3, T4, Task<TResult>> handler) where TController : class
        => RegisterCore<TController>(actionName, async (controller, context) => await handler(controller, binder1(context), binder2(context), binder3(context), binder4(context)).ConfigureAwait(false));

    /// <summary>注册四参数ValueTask有返回值Action</summary>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, T2, T3, T4, TResult>(String actionName, ApiParameterBinder<T1> binder1, ApiParameterBinder<T2> binder2, ApiParameterBinder<T3> binder3, ApiParameterBinder<T4> binder4, Func<TController, T1, T2, T3, T4, ValueTask<TResult>> handler) where TController : class
        => RegisterCore<TController>(actionName, async (controller, context) => await handler(controller, binder1(context), binder2(context), binder3(context), binder4(context)).ConfigureAwait(false));

    /// <summary>解析静态注册的Action</summary>
    /// <param name="controllerType">控制器类型</param>
    /// <param name="actionName">Action名称</param>
    /// <returns>已注册的Action</returns>
    internal static Func<Object, ControllerContext, ValueTask<Object?>>? Resolve(Type controllerType, String actionName)
    {
        if (_actions.TryGetValue(controllerType, out var actions) && actions.TryGetValue(actionName, out var action)) return action;
        return null;
    }

    private static void RegisterCore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController>(String actionName, Func<TController, ControllerContext, ValueTask<Object?>> handler) where TController : class
    {
        if (String.IsNullOrEmpty(actionName)) throw new ArgumentNullException(nameof(actionName));
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        var actions = _actions.GetOrAdd(typeof(TController), static _ => new ConcurrentDictionary<String, Func<Object, ControllerContext, ValueTask<Object?>>>(StringComparer.OrdinalIgnoreCase));
        actions[actionName] = (controller, context) => handler((TController)controller, context);
    }

    private static Object? ConvertParameter(ControllerContext context, String parameterName, Type targetType)
    {
        if (String.IsNullOrEmpty(parameterName)) throw new ArgumentNullException(nameof(parameterName));

        var values = context.ActionParameters;
        if (values == null || !values.TryGetValue(parameterName, out var value))
            throw new InvalidOperationException($"Required parameter [{parameterName}] was not found for type [{targetType.FullName}]");

        if (value == null) return GetDefaultValue(targetType);
        if (targetType.IsInstanceOfType(value)) return value;

        throw new InvalidOperationException($"Parameter [{parameterName}] cannot be converted to [{targetType.FullName}] in AOT-safe ApiActionRegistry");
    }

    private static Object? ResolveContextValue(ControllerContext context, Type targetType)
    {
        if (targetType.IsInstanceOfType(context)) return context;

        var session = context.Session;
        if (session != null && targetType.IsInstanceOfType(session)) return session;

        var request = context.Request;
        if (request != null && targetType.IsInstanceOfType(request)) return request;
        if (targetType == typeof(Byte[]) && request is IPacket packet) return packet.ToArray();

        var actionParameters = context.ActionParameters;
        if (actionParameters != null && targetType.IsInstanceOfType(actionParameters)) return actionParameters;

        var parameters = context.Parameters;
        if (parameters != null && targetType.IsInstanceOfType(parameters)) return parameters;

        return null;
    }

    private static Object? GetDefaultValue(Type type)
    {
        if (Nullable.GetUnderlyingType(type) != null || !type.IsValueType) return null;
        if (type.IsEnum) return Enum.ToObject(type, 0);

        return Type.GetTypeCode(type) switch
        {
            TypeCode.Boolean => false,
            TypeCode.Char => (Char)0,
            TypeCode.SByte => (SByte)0,
            TypeCode.Byte => (Byte)0,
            TypeCode.Int16 => (Int16)0,
            TypeCode.UInt16 => (UInt16)0,
            TypeCode.Int32 => 0,
            TypeCode.UInt32 => (UInt32)0,
            TypeCode.Int64 => (Int64)0,
            TypeCode.UInt64 => (UInt64)0,
            TypeCode.Single => (Single)0,
            TypeCode.Double => (Double)0,
            TypeCode.Decimal => (Decimal)0,
            TypeCode.DateTime => DateTime.MinValue,
            _ => null,
        };
    }
}