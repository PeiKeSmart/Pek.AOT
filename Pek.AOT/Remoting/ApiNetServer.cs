using Pek.Data;
using Pek.Http;
using Pek.Log;
using Pek.Messaging;
using Pek.Net;

namespace Pek.Remoting;

class ApiNetServer : NetServer, IApiServer
{
    /// <summary>主机</summary>
    public IApiHost Host { get; set; } = null!;

    /// <summary>当前服务器所有会话</summary>
    public IApiSession[] AllSessions => [.. Sessions.Values.OfType<IApiSession>()];

    public ApiNetServer()
    {
        Name = "Api";
        UseSession = true;
    }

    /// <summary>初始化</summary>
    /// <param name="config">配置</param>
    /// <param name="host">主机</param>
    /// <returns>是否成功</returns>
    public virtual Boolean Init(Object config, IApiHost host)
    {
        if (config is not NetUri uri) throw new ArgumentNullException(nameof(config));

        Host = host;
        Local = uri;

        if (String.IsNullOrEmpty(Local.Host) || Local.Host == "*") AddressFamily = System.Net.Sockets.AddressFamily.Unspecified;

        Add(new HttpCodec { AllowParseHeader = true });
        Add(Host.GetMessageCodec());

        return true;
    }

    protected override INetSession CreateSession(ISocketSession session) => new ApiNetSession();
}

class ApiNetSession : NetSession<ApiNetServer>, IApiSession
{
    private ApiServer _host = null!;

    /// <summary>主机</summary>
    IApiHost IApiSession.Host => _host;

    /// <summary>最后活跃时间</summary>
    public DateTime LastActive { get; set; }

    /// <summary>所有服务器所有会话，包含自己</summary>
    public virtual IApiSession[] AllSessions => _host.Server?.AllSessions ?? [this];

    /// <summary>令牌</summary>
    public String Token { get; set; } = String.Empty;

    /// <summary>请求参数</summary>
    public IDictionary<String, Object> Parameters { get; set; } = new Dictionary<String, Object>(StringComparer.OrdinalIgnoreCase);

    /// <summary>第二会话数据</summary>
    public IDictionary<String, Object?>? Items2 { get; set; }

    /// <summary>获取/设置 用户会话数据。优先使用第二会话数据</summary>
    /// <param name="key">键名</param>
    /// <returns>键值</returns>
    public override Object? this[String key]
    {
        get
        {
            var items = Items2 ?? Items;
            return items.TryGetValue(key, out var value) ? value : null;
        }
        set
        {
            var items = Items2 ?? Items;
            items[key] = value;
        }
    }

    /// <summary>开始会话处理</summary>
    public override void Start()
    {
        _host = Host.Host as ApiServer ?? throw new InvalidOperationException("Host is not ApiServer");
        base.Start();
    }

    /// <summary>查找Api动作</summary>
    /// <param name="action">动作</param>
    /// <returns>Api动作</returns>
    public virtual ApiAction FindAction(String action) => _host.Manager.Find(action) ?? throw new ApiException(ApiCode.NotFound, $"无法找到名为[{action}]的服务！");

    /// <summary>创建控制器实例</summary>
    /// <param name="api">Api动作</param>
    /// <returns>控制器实例</returns>
    public virtual Object CreateController(ApiAction api)
    {
        var controller = api.Controller ?? api.ControllerFactory?.Invoke(_host.ServiceProvider);
        if (controller is ApiController apiController) apiController.Host = _host;

        return controller ?? throw new ApiException(ApiCode.Forbidden, $"无法创建名为[{api.Name}]的服务！");
    }

    protected override void OnReceive(ReceivedEventArgs e)
    {
        LastActive = DateTime.Now;

        if (e.Message is not IMessage msg || msg.Reply) return;

        if (_host.Multiplex)
        {
            if (msg.Payload != null) msg.Payload = msg.Payload.Clone();

            ThreadPool.QueueUserWorkItem(_ =>
            {
                var response = _host.Process(this, msg);
                if (response != null && !Disposed) Session.SendMessage(response);
            });
        }
        else
        {
            var response = _host.Process(this, msg);
            if (response != null && !Disposed) Session.SendMessage(response);
        }
    }

    /// <summary>单向远程调用，无需等待返回</summary>
    /// <param name="action">服务操作</param>
    /// <param name="args">参数</param>
    /// <param name="flag">标识</param>
    /// <returns>发送结果</returns>
    public Int32 InvokeOneWay(String action, Object? args = null, Byte flag = 0)
    {
        using var span = Host.Tracer?.NewSpan("rpc:" + action, args);
        if (span != null) args = span.Attach(args);

        var msg = _host.Encoder.CreateRequest(action, args);
        if (msg is DefaultMessage dm)
        {
            dm.OneWay = true;
            if (flag > 0) dm.Flag = flag;
        }

        try
        {
            return Session.SendMessage(msg);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, args);
            throw;
        }
    }
}