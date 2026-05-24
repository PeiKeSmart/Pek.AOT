namespace Pek.Remoting;

/// <summary>控制器上下文</summary>
public class ControllerContext
{
    /// <summary>控制器实例</summary>
    public Object? Controller { get; set; }

    /// <summary>处理动作</summary>
    public ApiAction? Action { get; set; }

    /// <summary>真实动作名称</summary>
    public String? ActionName { get; set; }

    /// <summary>会话</summary>
    public IApiSession? Session { get; set; }

    /// <summary>请求</summary>
    public Object? Request { get; set; }

    /// <summary>请求参数</summary>
    public IDictionary<String, Object?>? Parameters { get; set; }

    /// <summary>操作方法参数</summary>
    public virtual IDictionary<String, Object?>? ActionParameters { get; set; }

    /// <summary>操作方法返回结果</summary>
    public Object? Result { get; set; }

    /// <summary>操作方法执行过程中发生的异常</summary>
    public virtual Exception? Exception { get; set; }

    /// <summary>是否已处理异常</summary>
    public Boolean ExceptionHandled { get; set; }

    [ThreadStatic]
    private static ControllerContext? _current;

    /// <summary>当前线程上下文</summary>
    public static ControllerContext? Current
    {
        get => _current;
        set => _current = value;
    }

    /// <summary>重置为默认状态</summary>
    public void Reset()
    {
        Controller = null;
        Action = null;
        ActionName = null;
        Session = null;
        Request = null;
        Parameters = null;
        ActionParameters = null;
        Result = null;
        Exception = null;
        ExceptionHandled = false;
    }
}