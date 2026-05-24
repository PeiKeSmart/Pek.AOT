using System.Reflection;

using Pek.Data;
using Pek.Extension;
using Pek.Log;

namespace Pek.Remoting;

/// <summary>Api动作</summary>
public class ApiAction
{
    /// <summary>动作名称</summary>
    public String Name { get; }

    /// <summary>动作所在类型</summary>
    public Type Type { get; }

    /// <summary>方法</summary>
    public MethodInfo Method { get; }

    /// <summary>方法参数</summary>
    internal ParameterInfo[] Parameters { get; }

    /// <summary>控制器对象</summary>
    /// <remarks>如果指定控制器对象，则每次调用前不再实例化对象。</remarks>
    public Object? Controller { get; set; }

    /// <summary>控制器工厂</summary>
    internal Func<IServiceProvider?, Object?>? ControllerFactory { get; set; }

    /// <summary>动作执行器</summary>
    internal Func<Object, ControllerContext, Object?>? Executor { get; set; }

    /// <summary>是否二进制参数</summary>
    public Boolean IsPacketParameter { get; }

    /// <summary>是否二进制返回</summary>
    public Boolean IsPacketReturn { get; }

    /// <summary>处理统计</summary>
    public ICounter StatProcess { get; set; } = new PerfCounter();

    /// <summary>最后会话</summary>
    public String? LastSession { get; set; }

    /// <summary>实例化</summary>
    /// <param name="method">方法</param>
    /// <param name="type">动作所在类型</param>
    public ApiAction(MethodInfo method, Type type)
    {
        if (method == null) throw new ArgumentNullException(nameof(method));
        if (type == null) type = method.DeclaringType ?? throw new ArgumentNullException(nameof(type));

        Name = GetName(type, method);
        Type = type;
        Method = method;

        Parameters = method.GetParameters();
        if (Parameters.Length == 1 && typeof(IPacket).IsAssignableFrom(Parameters[0].ParameterType)) IsPacketParameter = true;

        if (typeof(IPacket).IsAssignableFrom(method.ReturnType)) IsPacketReturn = true;
    }

    /// <summary>获取名称</summary>
    /// <param name="type">类型</param>
    /// <param name="method">方法</param>
    /// <returns>动作名称</returns>
    public static String GetName(Type type, MethodInfo method)
    {
        if (method == null) throw new ArgumentNullException(nameof(method));
        type ??= method.DeclaringType ?? throw new ArgumentNullException(nameof(type));

        var typeName = type.Name.TrimEnd("Controller", "Service");
        var typeAttribute = type.GetCustomAttribute<ApiAttribute>(true);
        if (typeAttribute != null) typeName = typeAttribute.Name;

        var methodName = method.Name;
        var methodAttribute = method.GetCustomAttribute<ApiAttribute>();
        if (methodAttribute != null) methodName = methodAttribute.Name;

        if (typeName.IsNullOrEmpty() || methodName.Contains('/'))
            return methodName;

        return $"{typeName}/{methodName}";
    }

    /// <summary>已重载。</summary>
    /// <returns>动作说明</returns>
    public override String ToString()
    {
        var returnType = Method.ReturnType;
        var returnTypeName = returnType.Name;
        if (typeof(Task).IsAssignableFrom(returnType))
        {
            if (!returnType.IsGenericType)
                returnTypeName = "void";
            else
                returnTypeName = returnType.GetGenericArguments()[0].Name;
        }

        var parameters = Method.GetParameters().Select(item => $"{item.ParameterType.Name} {item.Name}").Join(", ");
        return $"{returnTypeName} {Method.Name}({parameters})";
    }
}