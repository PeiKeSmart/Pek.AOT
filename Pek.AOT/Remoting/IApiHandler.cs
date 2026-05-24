using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;

using Pek.Caching;
using Pek.Collections;
using Pek.Data;
using Pek.Http;
using Pek.Log;
using Pek.Messaging;
using Pek.Security;

namespace Pek.Remoting;

/// <summary>Api处理器</summary>
public interface IApiHandler
{
    /// <summary>执行</summary>
    /// <param name="session">会话</param>
    /// <param name="action">动作</param>
    /// <param name="args">参数</param>
    /// <param name="msg">消息</param>
    /// <returns>执行结果</returns>
    Object? Execute(IApiSession session, String action, IPacket? args, IMessage msg);
}

/// <summary>默认处理器</summary>
/// <remarks>在基于令牌Token的无状态验证模式中，可以借助Token重写Prepare，来达到同一个Token共用相同的IApiSession.Items。</remarks>
public class ApiHandler : IApiHandler
{
    private static readonly ConcurrentDictionary<MethodInfo, Func<Object, ControllerContext, Object?>> _executors = new();

    /// <summary>Api接口主机</summary>
    public IApiHost Host { get; set; } = null!;

    /// <summary>执行</summary>
    /// <param name="session">会话</param>
    /// <param name="action">动作</param>
    /// <param name="args">参数</param>
    /// <param name="msg">消息</param>
    /// <returns>执行结果</returns>
    public virtual Object? Execute(IApiSession session, String action, IPacket? args, IMessage msg)
    {
        if (String.IsNullOrEmpty(action)) action = "Api/Info";

        var api = session.FindAction(action);
        var controller = session.CreateController(api);

        if (controller is IApi capi) capi.Session = session;
        api.LastSession = session is Pek.Net.INetSession netSession ? netSession.Remote + String.Empty : session + String.Empty;

        var counter = api.StatProcess;
        var startTicks = counter.StartCount();

        var context = Prepare(session, action, args, api, msg);
        context.Controller = controller;
        if (context.Parameters != null) DefaultSpan.Current?.Detach(context.Parameters);

        Object? result = null;
        try
        {
            if (controller is IActionFilter filter)
            {
                filter.OnActionExecuting(context);
                result = context.Result;
            }

            if (result == null)
            {
                var executor = api.Executor ??= _executors.GetOrAdd(api.Method, _ => CreateExecutor(api));
                result = executor(controller, context);
                context.Result = result;
            }

            if (controller is IActionFilter filter2)
            {
                filter2.OnActionExecuted(context);
                result = context.Result;
            }
        }
        catch (Exception ex)
        {
            context.Exception = ex.GetTrue();

            if (controller is IActionFilter filter)
            {
                filter.OnActionExecuted(context);
                result = context.Result;
            }

            if (context.Exception != null && !context.ExceptionHandled) throw;
        }
        finally
        {
            context.Reset();
            counter.StopCount(startTicks);
        }

        return result;
    }

    /// <summary>准备上下文，可以借助Token重写Session会话集合</summary>
    /// <param name="session">会话</param>
    /// <param name="action">动作</param>
    /// <param name="args">参数</param>
    /// <param name="api">动作元数据</param>
    /// <param name="msg">消息内容</param>
    /// <returns>控制器上下文</returns>
    protected virtual ControllerContext Prepare(IApiSession session, String action, IPacket? args, ApiAction api, IMessage msg)
    {
        var encoder = session["Encoder"] as IEncoder ?? Host.Encoder;

        var context = ControllerContext.Current;
        if (context == null)
        {
            context = new ControllerContext();
            ControllerContext.Current = context;
        }

        context.Action = api;
        context.ActionName = action;
        context.Session = session;
        context.Request = args;

        if (api.IsPacketParameter)
        {
            var values = new NullableDictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);
            var parameter = api.Parameters[0];
            if (parameter.Name != null) values[parameter.Name] = ConvertPacket(args, parameter.ParameterType);
            context.ActionParameters = values;

            return context;
        }

        var parameters = args == null || args.Total == 0
            ? new NullableDictionary<String, Object?>(StringComparer.OrdinalIgnoreCase)
            : ConvertParameters(encoder.DecodeParameters(action, args, msg));
        context.Parameters = parameters;
        session.Parameters = ToSessionParameters(parameters);

        if (parameters.TryGetValue("Token", out var token)) session.Token = token + String.Empty;
        if (String.IsNullOrEmpty(session.Token) && msg is HttpMessage httpMessage && httpMessage.Headers != null)
        {
            if (httpMessage.Headers.TryGetValue("x-token", out var token2))
                session.Token = token2;
            else if (httpMessage.Headers.TryGetValue("Authorization", out token2) && token2.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                session.Token = token2[7..];
        }

        context.ActionParameters = GetParams(api.Parameters, parameters, encoder);

        return context;
    }

    /// <summary>获取参数</summary>
    /// <param name="parameters">参数列表</param>
    /// <param name="args">参数字典</param>
    /// <param name="encoder">编码器</param>
    /// <returns>转换后的参数字典</returns>
    protected virtual IDictionary<String, Object?>? GetParams(ParameterInfo[] parameters, IDictionary<String, Object?> args, IEncoder encoder)
    {
        if (parameters.Length <= 0) return null;

        var result = new NullableDictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in parameters)
        {
            var name = parameter.Name ?? throw new InvalidOperationException($"Method [{parameter.Member.Name}] contains unnamed parameter.");

            args.TryGetValue(name, out var value);
            var parameterType = parameter.ParameterType;
            if (value == null && !IsSimpleType(parameterType)) value = args;

            result[name] = ConvertValue(value, parameterType, encoder);
        }

        return result;
    }

    private static Func<Object, ControllerContext, Object?> CreateExecutor(ApiAction api)
    {
        if (api.Parameters.Length > 4) throw new NotSupportedException($"AOT-safe ApiHandler currently supports up to 4 parameters. Action [{api.Name}] has {api.Parameters.Length} parameters.");
        if (api.Parameters.Any(static item => item.ParameterType.IsValueType)) throw new NotSupportedException($"AOT-safe ApiHandler does not support value-type parameters yet. Action [{api.Name}] contains unsupported parameter types.");
        if (api.Parameters.Any(static item => String.Equals(item.ParameterType.FullName, "Pek.Data.Packet", StringComparison.Ordinal))) throw new NotSupportedException($"AOT-safe ApiHandler does not support legacy Packet parameters. Action [{api.Name}] should use IPacket or Byte[].");

        var returnType = api.Method.ReturnType;
        if (returnType.IsGenericType && (returnType.GetGenericTypeDefinition() == typeof(Task<>) || returnType.GetGenericTypeDefinition() == typeof(ValueTask<>)))
            throw new NotSupportedException($"AOT-safe ApiHandler does not support generic Task/ValueTask results yet. Action [{api.Name}] should return a reference type, Task, or ValueTask.");
        if (returnType.IsValueType && returnType != typeof(void) && returnType != typeof(ValueTask))
            throw new NotSupportedException($"AOT-safe ApiHandler does not support value-type return values yet. Action [{api.Name}] should return a reference type, Task, or ValueTask.");

        return api.Parameters.Length switch
        {
            0 => CreateZeroExecutor(api.Method),
            1 => CreateOneExecutor(api.Method),
            2 => CreateTwoExecutor(api.Method),
            3 => CreateThreeExecutor(api.Method),
            4 => CreateFourExecutor(api.Method),
            _ => throw new NotSupportedException($"AOT-safe ApiHandler currently supports up to 4 parameters. Action [{api.Name}] has {api.Parameters.Length} parameters."),
        };
    }

    private static Func<Object, ControllerContext, Object?> CreateZeroExecutor(MethodInfo method)
    {
        var returnType = method.ReturnType;
        if (returnType == typeof(void))
        {
            var handler = (Action<Object>)method.CreateDelegate(typeof(Action<Object>));
            return (controller, context) =>
            {
                handler(controller);
                return null;
            };
        }

        if (returnType == typeof(Task))
        {
            var handler = (Func<Object, Task>)method.CreateDelegate(typeof(Func<Object, Task>));
            return (controller, context) =>
            {
                handler(controller).ConfigureAwait(false).GetAwaiter().GetResult();
                return null;
            };
        }

        if (returnType == typeof(ValueTask))
        {
            var handler = (Func<Object, ValueTask>)method.CreateDelegate(typeof(Func<Object, ValueTask>));
            return (controller, context) =>
            {
                handler(controller).GetAwaiter().GetResult();
                return null;
            };
        }

        var func = (Func<Object, Object?>)method.CreateDelegate(typeof(Func<Object, Object?>));
        return (controller, context) => func(controller);
    }

    private static Func<Object, ControllerContext, Object?> CreateOneExecutor(MethodInfo method)
    {
        var parameter = method.GetParameters()[0];
        var returnType = method.ReturnType;
        if (returnType == typeof(void))
        {
            var handler = (Action<Object, Object?>)method.CreateDelegate(typeof(Action<Object, Object?>));
            return (controller, context) =>
            {
                handler(controller, GetArgument(context, parameter));
                return null;
            };
        }

        if (returnType == typeof(Task))
        {
            var handler = (Func<Object, Object?, Task>)method.CreateDelegate(typeof(Func<Object, Object?, Task>));
            return (controller, context) =>
            {
                handler(controller, GetArgument(context, parameter)).ConfigureAwait(false).GetAwaiter().GetResult();
                return null;
            };
        }

        if (returnType == typeof(ValueTask))
        {
            var handler = (Func<Object, Object?, ValueTask>)method.CreateDelegate(typeof(Func<Object, Object?, ValueTask>));
            return (controller, context) =>
            {
                handler(controller, GetArgument(context, parameter)).GetAwaiter().GetResult();
                return null;
            };
        }

        var func = (Func<Object, Object?, Object?>)method.CreateDelegate(typeof(Func<Object, Object?, Object?>));
        return (controller, context) => func(controller, GetArgument(context, parameter));
    }

    private static Func<Object, ControllerContext, Object?> CreateTwoExecutor(MethodInfo method)
    {
        var parameters = method.GetParameters();
        var returnType = method.ReturnType;
        if (returnType == typeof(void))
        {
            var handler = (Action<Object, Object?, Object?>)method.CreateDelegate(typeof(Action<Object, Object?, Object?>));
            return (controller, context) =>
            {
                handler(controller, GetArgument(context, parameters[0]), GetArgument(context, parameters[1]));
                return null;
            };
        }

        if (returnType == typeof(Task))
        {
            var handler = (Func<Object, Object?, Object?, Task>)method.CreateDelegate(typeof(Func<Object, Object?, Object?, Task>));
            return (controller, context) =>
            {
                handler(controller, GetArgument(context, parameters[0]), GetArgument(context, parameters[1])).ConfigureAwait(false).GetAwaiter().GetResult();
                return null;
            };
        }

        if (returnType == typeof(ValueTask))
        {
            var handler = (Func<Object, Object?, Object?, ValueTask>)method.CreateDelegate(typeof(Func<Object, Object?, Object?, ValueTask>));
            return (controller, context) =>
            {
                handler(controller, GetArgument(context, parameters[0]), GetArgument(context, parameters[1])).GetAwaiter().GetResult();
                return null;
            };
        }

        var func = (Func<Object, Object?, Object?, Object?>)method.CreateDelegate(typeof(Func<Object, Object?, Object?, Object?>));
        return (controller, context) => func(controller, GetArgument(context, parameters[0]), GetArgument(context, parameters[1]));
    }

    private static Func<Object, ControllerContext, Object?> CreateThreeExecutor(MethodInfo method)
    {
        var parameters = method.GetParameters();
        var returnType = method.ReturnType;
        if (returnType == typeof(void))
        {
            var handler = (Action<Object, Object?, Object?, Object?>)method.CreateDelegate(typeof(Action<Object, Object?, Object?, Object?>));
            return (controller, context) =>
            {
                handler(controller, GetArgument(context, parameters[0]), GetArgument(context, parameters[1]), GetArgument(context, parameters[2]));
                return null;
            };
        }

        if (returnType == typeof(Task))
        {
            var handler = (Func<Object, Object?, Object?, Object?, Task>)method.CreateDelegate(typeof(Func<Object, Object?, Object?, Object?, Task>));
            return (controller, context) =>
            {
                handler(controller, GetArgument(context, parameters[0]), GetArgument(context, parameters[1]), GetArgument(context, parameters[2])).ConfigureAwait(false).GetAwaiter().GetResult();
                return null;
            };
        }

        if (returnType == typeof(ValueTask))
        {
            var handler = (Func<Object, Object?, Object?, Object?, ValueTask>)method.CreateDelegate(typeof(Func<Object, Object?, Object?, Object?, ValueTask>));
            return (controller, context) =>
            {
                handler(controller, GetArgument(context, parameters[0]), GetArgument(context, parameters[1]), GetArgument(context, parameters[2])).GetAwaiter().GetResult();
                return null;
            };
        }

        var func = (Func<Object, Object?, Object?, Object?, Object?>)method.CreateDelegate(typeof(Func<Object, Object?, Object?, Object?, Object?>));
        return (controller, context) => func(controller, GetArgument(context, parameters[0]), GetArgument(context, parameters[1]), GetArgument(context, parameters[2]));
    }

    private static Func<Object, ControllerContext, Object?> CreateFourExecutor(MethodInfo method)
    {
        var parameters = method.GetParameters();
        var returnType = method.ReturnType;
        if (returnType == typeof(void))
        {
            var handler = (Action<Object, Object?, Object?, Object?, Object?>)method.CreateDelegate(typeof(Action<Object, Object?, Object?, Object?, Object?>));
            return (controller, context) =>
            {
                handler(controller, GetArgument(context, parameters[0]), GetArgument(context, parameters[1]), GetArgument(context, parameters[2]), GetArgument(context, parameters[3]));
                return null;
            };
        }

        if (returnType == typeof(Task))
        {
            var handler = (Func<Object, Object?, Object?, Object?, Object?, Task>)method.CreateDelegate(typeof(Func<Object, Object?, Object?, Object?, Object?, Task>));
            return (controller, context) =>
            {
                handler(controller, GetArgument(context, parameters[0]), GetArgument(context, parameters[1]), GetArgument(context, parameters[2]), GetArgument(context, parameters[3])).ConfigureAwait(false).GetAwaiter().GetResult();
                return null;
            };
        }

        if (returnType == typeof(ValueTask))
        {
            var handler = (Func<Object, Object?, Object?, Object?, Object?, ValueTask>)method.CreateDelegate(typeof(Func<Object, Object?, Object?, Object?, Object?, ValueTask>));
            return (controller, context) =>
            {
                handler(controller, GetArgument(context, parameters[0]), GetArgument(context, parameters[1]), GetArgument(context, parameters[2]), GetArgument(context, parameters[3])).GetAwaiter().GetResult();
                return null;
            };
        }

        var func = (Func<Object, Object?, Object?, Object?, Object?, Object?>)method.CreateDelegate(typeof(Func<Object, Object?, Object?, Object?, Object?, Object?>));
        return (controller, context) => func(controller, GetArgument(context, parameters[0]), GetArgument(context, parameters[1]), GetArgument(context, parameters[2]), GetArgument(context, parameters[3]));
    }

    private static Object? GetArgument(ControllerContext context, ParameterInfo parameter)
    {
        var values = context.ActionParameters;
        var name = parameter.Name ?? throw new InvalidOperationException($"Method [{parameter.Member.Name}] contains unnamed parameter.");

        if (values == null || !values.TryGetValue(name, out var value)) return GetDefaultValue(parameter.ParameterType);

        return value ?? GetDefaultValue(parameter.ParameterType);
    }

    private static IDictionary<String, Object?> ConvertParameters(IDictionary<String, Object> source)
    {
        var result = new NullableDictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in source)
        {
            result[item.Key] = item.Value;
        }

        return result;
    }

    private static IDictionary<String, Object> ToSessionParameters(IDictionary<String, Object?> source)
    {
        var result = new NullableDictionary<String, Object>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in source)
        {
            result[item.Key] = item.Value!;
        }

        return result;
    }

    private static Object? ConvertValue(Object? value, Type parameterType, IEncoder encoder)
    {
        var actualType = Nullable.GetUnderlyingType(parameterType) ?? parameterType;

        if (typeof(IPacket).IsAssignableFrom(actualType)) return ConvertPacket(value as IPacket, actualType);
        if (actualType == typeof(Byte[]))
        {
            if (value == null) return Array.Empty<Byte>();
            if (value is Byte[] buffer) return buffer;
            if (value is String str && !String.IsNullOrEmpty(str)) return Convert.FromBase64String(str);
            if (value is IPacket packet) return packet.ToArray();
        }

        if (value == null) return GetDefaultValue(parameterType);
        if (actualType.IsInstanceOfType(value)) return value;

        if (actualType.IsEnum)
        {
            if (value is String enumText && !String.IsNullOrWhiteSpace(enumText)) return Enum.Parse(actualType, enumText, true);
            return Enum.ToObject(actualType, System.Convert.ChangeType(value, Enum.GetUnderlyingType(actualType), CultureInfo.InvariantCulture)!);
        }

        if (actualType == typeof(Guid)) return value is Guid guid ? guid : Guid.Parse(value + String.Empty);
        if (actualType == typeof(DateTimeOffset)) return value is DateTimeOffset dto ? dto : DateTimeOffset.Parse(value + String.Empty, CultureInfo.InvariantCulture);
        if (actualType == typeof(TimeSpan)) return value is TimeSpan span ? span : TimeSpan.Parse(value + String.Empty, CultureInfo.InvariantCulture);

        if (IsSimpleType(actualType)) return System.Convert.ChangeType(value, actualType, CultureInfo.InvariantCulture);

        return encoder.Convert(value, parameterType);
    }

    private static Object? ConvertPacket(IPacket? packet, Type parameterType)
    {
        if (packet == null) return GetDefaultValue(parameterType);
        if (parameterType.IsInstanceOfType(packet)) return packet;
        if (parameterType == typeof(Byte[])) return packet.ToArray();

        return packet;
    }

    private static Boolean IsSimpleType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type.IsEnum) return true;

        return Type.GetTypeCode(type) != TypeCode.Object || type == typeof(Guid) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan);
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

/// <summary>带令牌会话的处理器</summary>
/// <remarks>在基于令牌Token的无状态验证模式中，可以借助Token重写Prepare，来达到同一个Token共用相同的IApiSession.Items。支持内存缓存和Redis缓存。</remarks>
public class TokenApiHandler : ApiHandler
{
    /// <summary>会话存储</summary>
    public ICache Cache { get; set; } = new MemoryCache { Expire = 20 * 60 };

    /// <summary>准备上下文，可以借助Token重写Session会话集合</summary>
    /// <param name="session">会话</param>
    /// <param name="action">动作</param>
    /// <param name="args">参数</param>
    /// <param name="api">动作元数据</param>
    /// <param name="msg">消息</param>
    /// <returns>控制器上下文</returns>
    protected override ControllerContext Prepare(IApiSession session, String action, IPacket? args, ApiAction api, IMessage msg)
    {
        var context = base.Prepare(session, action, args, api, msg);

        var token = session.Token;
        if (!String.IsNullOrEmpty(token) && session is ApiNetSession netSession && netSession.Items["Token"] + String.Empty != token)
        {
            var values = Cache.GetDictionary<Object>(GetKey(token));
            var dictionary = new NullableDictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in values)
            {
                dictionary[item.Key] = item.Value;
            }

            netSession.Items2 = dictionary;
            netSession.Items["Token"] = token;
        }

        return context;
    }

    /// <summary>根据令牌获取缓存Key</summary>
    /// <param name="token">令牌</param>
    /// <returns>缓存Key</returns>
    protected virtual String GetKey(String token) => !String.IsNullOrEmpty(token) && token.Length > 16 ? token.MD5() : token;
}