using Pek.Extension;
using Pek.Net;

namespace Pek.Http;

/// <summary>Http服务器</summary>
public class HttpServer : NetServer, IHttpHost
{
    /// <summary>Http响应头Server名称</summary>
    public String ServerName { get; set; }

    /// <summary>路由映射</summary>
    public IDictionary<String, IHttpHandler> Routes { get; set; } = new Dictionary<String, IHttpHandler>(StringComparer.OrdinalIgnoreCase);

    /// <summary>实例化Http服务器</summary>
    public HttpServer()
    {
        Name = "Http";
        Port = 80;
        ProtocolType = NetType.Http;

        var version = GetType().Assembly.GetName().Version ?? new Version();
        ServerName = $"Pek-HttpServer/{version.Major}.{version.Minor}";
    }

    /// <summary>为会话创建网络数据处理器</summary>
    /// <param name="session">会话</param>
    /// <returns>Http会话处理器</returns>
    public override INetHandler? CreateHandler(INetSession session) => new HttpSession();

    /// <summary>映射路由处理器</summary>
    /// <param name="path">路径</param>
    /// <param name="handler">处理器</param>
    public void Map(String path, IHttpHandler handler) => SetRoute(path, handler);

    /// <summary>映射路由处理委托</summary>
    /// <param name="path">路径</param>
    /// <param name="handler">处理委托</param>
    public void Map(String path, HttpProcessDelegate handler)
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        SetRoute(path, new DelegateHandler { Callback = handler });
    }

    /// <summary>映射无参结果委托</summary>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <param name="path">路径</param>
    /// <param name="handler">处理委托</param>
    public void Map<TResult>(String path, Func<TResult> handler)
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        SetRoute(path, new DelegateHandler { Callback = () => (Object?)handler() });
    }

    /// <summary>映射带上下文的结果委托</summary>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <param name="path">路径</param>
    /// <param name="handler">处理委托</param>
    public void Map<TResult>(String path, Func<IHttpContext, TResult> handler)
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        SetRoute(path, new DelegateHandler { Callback = (Func<IHttpContext, Object?>)(context => handler(context)) });
    }

    /// <summary>映射静态文件目录</summary>
    /// <param name="path">映射路径</param>
    /// <param name="contentPath">内容目录</param>
    public void MapStaticFiles(String path, String contentPath)
    {
        if (contentPath.IsNullOrEmpty()) throw new ArgumentNullException(nameof(contentPath));

        path = path.EnsureStart("/");
        var path2 = path.EnsureEnd("/").EnsureEnd("*");
        SetRoute(path2, new StaticFilesHandler { Path = path.EnsureEnd("/"), ContentPath = contentPath });
    }

    private void SetRoute(String path, IHttpHandler handler)
    {
        if (path.IsNullOrEmpty()) throw new ArgumentNullException(nameof(path));
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        path = path.EnsureStart("/");
        Routes[path] = handler;
    }

    private readonly IDictionary<String, String> _pathCache = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);

    /// <summary>匹配处理器</summary>
    /// <param name="path">请求路径</param>
    /// <param name="request">Http请求</param>
    /// <returns>处理器</returns>
    public IHttpHandler? MatchHandler(String path, HttpRequest? request)
    {
        if (path.IsNullOrEmpty()) return null;

        if (Routes.TryGetValue(path, out var handler)) return handler;

        if (_pathCache.TryGetValue(path, out var cached) && Routes.TryGetValue(cached, out handler)) return handler;

        foreach (var item in Routes)
        {
            var key = item.Key;
            if (!key.Contains('*')) continue;
            if (!key.IsMatch(path)) continue;

            if (Routes.TryGetValue(key, out handler))
            {
                if (handler is StaticFilesHandler || path.Split('/').Length <= 3) _pathCache[path] = key;
                return handler;
            }
        }

        return null;
    }
}