namespace Pek.Http;

/// <summary>Http控制器标记</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class HttpControllerAttribute : Attribute
{
    /// <summary>控制器路径</summary>
    public String? Path { get; set; }
}

/// <summary>Http动作标记</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class HttpActionAttribute : Attribute
{
    /// <summary>动作名称</summary>
    public String? Name { get; set; }
}

/// <summary>指定参数从请求参数字典绑定</summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class HttpFromParameterAttribute : Attribute
{
    /// <summary>参数名</summary>
    public String? Name { get; set; }
}

/// <summary>指定参数从请求体绑定</summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class HttpFromBodyAttribute : Attribute
{
}

/// <summary>指定参数从服务容器绑定</summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class HttpFromServiceAttribute : Attribute
{
}

/// <summary>指定参数从Http上下文对象绑定</summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class HttpFromContextAttribute : Attribute
{
}