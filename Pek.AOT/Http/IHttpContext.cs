using Pek.Collections;
using Pek.Net;

namespace Pek.Http;

/// <summary>Http上下文</summary>
public interface IHttpContext
{
    /// <summary>请求</summary>
    HttpRequest Request { get; }

    /// <summary>响应</summary>
    HttpResponse Response { get; }

    /// <summary>连接会话</summary>
    INetSession? Connection { get; }

    /// <summary>Socket连接</summary>
    ISocketRemote? Socket { get; }

    /// <summary>WebSocket连接</summary>
    WebSocket? WebSocket { get; }

    /// <summary>执行路径</summary>
    String Path { get; }

    /// <summary>处理器</summary>
    IHttpHandler? Handler { get; }

    /// <summary>服务提供者</summary>
    IServiceProvider? ServiceProvider { get; }

    /// <summary>请求参数</summary>
    IDictionary<String, Object?> Parameters { get; }
}

/// <summary>默认Http上下文</summary>
public class DefaultHttpContext : IHttpContext
{
    /// <summary>请求</summary>
    public HttpRequest Request { get; set; }

    /// <summary>响应</summary>
    public HttpResponse Response { get; set; } = new();

    /// <summary>连接会话</summary>
    public INetSession? Connection { get; set; }

    /// <summary>Socket连接</summary>
    public ISocketRemote? Socket { get; set; }

    /// <summary>WebSocket连接</summary>
    public WebSocket? WebSocket { get; set; }

    /// <summary>执行路径</summary>
    public String Path { get; set; }

    /// <summary>处理器</summary>
    public IHttpHandler? Handler { get; set; }

    /// <summary>服务提供者</summary>
    public IServiceProvider? ServiceProvider { get; set; }

    /// <summary>请求参数</summary>
    public IDictionary<String, Object?> Parameters { get; } = new NullableDictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>实例化</summary>
    /// <param name="session">会话</param>
    /// <param name="request">请求</param>
    /// <param name="path">路径</param>
    /// <param name="handler">处理器</param>
    public DefaultHttpContext(INetSession session, HttpRequest request, String path, IHttpHandler? handler)
    {
        Connection = session;
        Request = request;
        Path = path;
        Handler = handler;
        Socket = session?.Session;
    }

    /// <summary>实例化</summary>
    /// <param name="socket">远程连接</param>
    /// <param name="request">请求</param>
    /// <param name="path">路径</param>
    /// <param name="handler">处理器</param>
    public DefaultHttpContext(ISocketRemote socket, HttpRequest request, String path, IHttpHandler? handler)
    {
        Socket = socket;
        Request = request;
        Path = path;
        Handler = handler;
    }

    [ThreadStatic]
    private static IHttpContext? _current;

    /// <summary>当前上下文</summary>
    public static IHttpContext? Current { get => _current; set => _current = value; }
}