namespace Pek.Http;

/// <summary>Http处理器接口</summary>
public interface IHttpHandler
{
    /// <summary>处理请求</summary>
    /// <param name="context">Http上下文</param>
    void ProcessRequest(IHttpContext context);
}

/// <summary>Http请求处理委托</summary>
/// <param name="context">Http上下文</param>
public delegate void HttpProcessDelegate(IHttpContext context);

/// <summary>委托Http处理器</summary>
public class DelegateHandler : IHttpHandler
{
    /// <summary>委托回调</summary>
    public Delegate? Callback { get; set; }

    /// <summary>处理请求</summary>
    /// <param name="context">Http上下文</param>
    public virtual void ProcessRequest(IHttpContext context)
    {
        var handler = Callback;
        if (handler == null) return;

        switch (handler)
        {
            case HttpProcessDelegate httpHandler:
                httpHandler(context);
                break;
            case Func<IHttpContext, Task<Object?>> func:
                SetResult(context, func(context).GetAwaiter().GetResult());
                break;
            case Func<Task<Object?>> func:
                SetResult(context, func().GetAwaiter().GetResult());
                break;
            case Func<IHttpContext, Task> func:
                func(context).GetAwaiter().GetResult();
                break;
            case Func<Task> func:
                func().GetAwaiter().GetResult();
                break;
            case Func<IHttpContext, ValueTask<Object?>> func:
                SetResult(context, func(context).GetAwaiter().GetResult());
                break;
            case Func<ValueTask<Object?>> func:
                SetResult(context, func().GetAwaiter().GetResult());
                break;
            case Func<IHttpContext, ValueTask> func:
                func(context).GetAwaiter().GetResult();
                break;
            case Func<ValueTask> func:
                func().GetAwaiter().GetResult();
                break;
            case Func<IHttpContext, Object?> func:
                SetResult(context, func(context));
                break;
            case Func<Object?> func:
                SetResult(context, func());
                break;
            case Action<IHttpContext> action:
                action(context);
                break;
            case Action action:
                action();
                break;
            default:
                throw new NotSupportedException($"Delegate type {handler.GetType().FullName} is not supported in AOT-safe DelegateHandler");
        }
    }

    private static void SetResult(IHttpContext context, Object? result)
    {
        if (result != null) context.Response.SetResult(result);
    }
}