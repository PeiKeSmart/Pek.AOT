using System.Text;

using Pek;
using Pek.Data;
using Pek.IO;
using Pek.Log;
using Pek.Model;

namespace Pek.Net;

/// <summary>泛型网络服务会话</summary>
/// <typeparam name="TServer">网络服务器类型</typeparam>
public class NetSession<TServer> : NetSession where TServer : NetServer
{
    /// <summary>主服务</summary>
    public virtual TServer Host
    {
        get => ((INetSession)this).Host as TServer ?? throw new XException("Host is not {0}", typeof(TServer).Name);
        set => ((INetSession)this).Host = value;
    }
}

/// <summary>网络服务会话</summary>
public class NetSession : DisposeBase, INetSession, IServiceProvider, IExtend
{
    #region 属性
    /// <summary>唯一会话标识</summary>
    public virtual Int32 ID { get; internal set; }

    /// <summary>主服务</summary>
    NetServer INetSession.Host { get; set; } = null!;

    /// <summary>客户端 Socket 会话</summary>
    public ISocketSession Session { get; set; } = null!;

    /// <summary>Socket 服务器</summary>
    public ISocketServer Server { get; set; } = null!;

    /// <summary>客户端远程地址</summary>
    public NetUri Remote => Session.Remote;

    /// <summary>网络数据处理器</summary>
    public INetHandler? Handler { get; set; }

    /// <summary>用户会话数据</summary>
    public IDictionary<String, Object?> Items => Session.Items;

    /// <summary>获取或设置扩展数据</summary>
    /// <param name="key">键</param>
    /// <returns>值</returns>
    public virtual Object? this[String key] { get => Session[key]; set => Session[key] = value; }

    /// <summary>会话级服务提供者</summary>
    public IServiceProvider? ServiceProvider { get; set; }

    /// <summary>连接创建事件</summary>
    public event EventHandler<EventArgs>? Connected;

    /// <summary>连接断开事件</summary>
    public event EventHandler<EventArgs>? Disconnected;

    /// <summary>数据到达事件</summary>
    public event EventHandler<ReceivedEventArgs>? Received;

    private Int32 _running;
    private IServiceScope? _scope;
    #endregion

    #region 方法
    /// <summary>开始会话处理</summary>
    public virtual void Start()
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) return;

        WriteLog("Connected {0}", Session);

        var host = ((INetSession)this).Host;
        if (ServiceProvider == null)
        {
            var factory = host.ServiceProvider as IServiceScopeFactory
                ?? host.ServiceProvider?.GetService(typeof(IServiceScopeFactory)) as IServiceScopeFactory;
            _scope = factory?.CreateScope();
            ServiceProvider = _scope?.ServiceProvider ?? host.ServiceProvider;
        }

        using var span = host.Tracer?.NewSpan($"net:{host.Name}:Connect", Remote?.ToString());
        try
        {
            Handler = host.CreateHandler(this);
            Handler?.Init(this);

            OnConnected();

            var session = Session;
            session.Received += Session_Received;
            session.OnDisposed += Session_OnDisposed;
            session.Error += OnError;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }

    private void Session_OnDisposed(Object? sender, EventArgs e)
    {
        try
        {
            var reason = sender is SessionBase session && !String.IsNullOrEmpty(session.CloseReason)
                ? session.CloseReason!
                : "Disconnect";
            Close(reason);
        }
        catch { }

        Dispose();
    }

    private void Session_Received(Object? sender, ReceivedEventArgs e)
    {
        var host = ((INetSession)this).Host;
        using var span = host.Tracer?.NewSpan($"net:{host.Name}:Receive", e.Message, e.Packet?.Total ?? 0);

        try
        {
            Handler?.Process(e);

            if (!Disposed && (e.Packet != null || e.Message != null))
                OnReceive(e);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, e.Message ?? e.Packet);
            throw;
        }
    }

    /// <summary>释放资源</summary>
    /// <param name="disposing">是否显式释放</param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        Handler.TryDispose();
        Handler = null;

        var reason = GetType().Name + (disposing ? "Dispose" : "GC");

        try
        {
            Close(reason);
        }
        catch { }

        Session?.Dispose();

        _scope?.Dispose();
        _scope = null;
    }

    /// <summary>关闭跟客户端的网络连接</summary>
    /// <param name="reason">关闭原因</param>
    public void Close(String reason)
    {
        if (Interlocked.CompareExchange(ref _running, 0, 1) != 1) return;

        var host = ((INetSession)this).Host;
        using var span = host.Tracer?.NewSpan($"net:{host.Name}:Disconnect", new { remote = Remote?.ToString(), reason });
        try
        {
            WriteLog("Disconnect [{0}] {1}", Session, reason);

#pragma warning disable CS0618
            OnDisconnected();
#pragma warning restore CS0618
            OnDisconnected(reason);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }
    #endregion

    #region 业务核心
    /// <summary>新的客户端连接</summary>
    protected virtual void OnConnected() => Connected?.Invoke(this, EventArgs.Empty);

    /// <summary>客户端连接已断开</summary>
    /// <param name="reason">断开原因</param>
    protected virtual void OnDisconnected(String reason) => Disconnected?.Invoke(this, EventArgs.Empty);

    /// <summary>客户端连接已断开</summary>
    [Obsolete("=>OnDisconnected(String reason)")]
    protected virtual void OnDisconnected() => Disconnected?.Invoke(this, EventArgs.Empty);

    /// <summary>收到客户端发来的数据</summary>
    /// <param name="e">接收事件参数</param>
    protected virtual void OnReceive(ReceivedEventArgs e) => Received?.Invoke(this, e);

    /// <summary>发生错误</summary>
    /// <param name="sender">事件源</param>
    /// <param name="e">异常事件参数</param>
    protected virtual void OnError(Object? sender, ExceptionEventArgs e) => WriteError(e.Exception.Message);
    #endregion

    #region 发送数据
    /// <summary>发送数据包</summary>
    /// <param name="data">数据包</param>
    /// <returns>当前会话</returns>
    public virtual INetSession Send(IPacket data)
    {
        var host = ((INetSession)this).Host;
        using var span = host.Tracer?.NewSpan($"net:{host.Name}:Send", data, data.Total);

        Session.Send(data);

        return this;
    }

    /// <summary>发送字节数组</summary>
    /// <param name="data">数据</param>
    /// <param name="offset">偏移</param>
    /// <param name="count">长度</param>
    /// <returns>当前会话</returns>
    public virtual INetSession Send(Byte[] data, Int32 offset = 0, Int32 count = -1)
    {
        var host = ((INetSession)this).Host;
        var length = count > 0 ? count : data.Length - offset;
        using var span = host.Tracer?.NewSpan($"net:{host.Name}:Send", data.ToHex(offset, length > 64 ? 64 : length), length);

        Session.Send(data, offset, count);

        return this;
    }

    /// <summary>发送只读内存段</summary>
    /// <param name="data">数据</param>
    /// <returns>当前会话</returns>
    public virtual INetSession Send(ReadOnlySpan<Byte> data)
    {
        var host = ((INetSession)this).Host;
        using var span = host.Tracer?.NewSpan($"net:{host.Name}:Send", null, data.Length);

        Session.Send(data);

        return this;
    }

    /// <summary>发送数据流</summary>
    /// <param name="stream">数据流</param>
    /// <returns>当前会话</returns>
    public virtual INetSession Send(Stream stream)
    {
        var host = ((INetSession)this).Host;
        using var span = host.Tracer?.NewSpan($"net:{host.Name}:Send");

        Session.Send(stream);

        return this;
    }

    /// <summary>发送字符串</summary>
    /// <param name="msg">消息</param>
    /// <param name="encoding">编码</param>
    /// <returns>当前会话</returns>
    public virtual INetSession Send(String msg, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        var host = ((INetSession)this).Host;
        using var span = host.Tracer?.NewSpan($"net:{host.Name}:Send", msg, encoding.GetByteCount(msg));

        Session.Send(msg, encoding);

        return this;
    }

    /// <summary>通过管道发送消息</summary>
    /// <param name="message">消息</param>
    /// <returns>发送字节数</returns>
    public virtual Int32 SendMessage(Object message) => Session.SendMessage(message);

    /// <summary>通过管道发送响应消息</summary>
    /// <param name="message">消息</param>
    /// <param name="eventArgs">接收事件参数</param>
    /// <returns>发送字节数</returns>
    public virtual Int32 SendReply(Object message, ReceivedEventArgs eventArgs) => ((Session as SessionBase)!).SendMessage(message, eventArgs.Context);

    /// <summary>异步发送消息并等待响应</summary>
    /// <param name="message">消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应消息</returns>
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
    public virtual ValueTask<Object> SendMessageAsync(Object message, CancellationToken cancellationToken = default) => Session.SendMessageAsync(message, cancellationToken);
#else
    public virtual Task<Object> SendMessageAsync(Object message, CancellationToken cancellationToken = default) => Session.SendMessageAsync(message, cancellationToken);
#endif
    #endregion

    #region 日志
    /// <summary>日志提供者</summary>
    public ILog? Log { get; set; }

    private String? _LogPrefix;

    /// <summary>日志前缀</summary>
    public virtual String LogPrefix
    {
        get
        {
            if (_LogPrefix == null)
            {
                var host = ((INetSession)this).Host;
                var name = host?.Name ?? String.Empty;
                _LogPrefix = $"{name}[{ID}] ";
            }

            return _LogPrefix;
        }
        set => _LogPrefix = value;
    }

    /// <summary>写日志</summary>
    /// <param name="format">格式</param>
    /// <param name="args">参数</param>
    public virtual void WriteLog(String format, params Object?[] args) => Log?.Info(LogPrefix + format, args);

    /// <summary>写错误日志</summary>
    /// <param name="format">格式</param>
    /// <param name="args">参数</param>
    public virtual void WriteError(String format, params Object?[] args) => Log?.Error(LogPrefix + format, args);
    #endregion

    #region 辅助
    /// <summary>返回字符串表示</summary>
    /// <returns>会话信息</returns>
    public override String ToString() => $"{((INetSession)this).Host?.Name}[{ID}] {Session}";

    /// <summary>获取服务</summary>
    /// <param name="serviceType">服务类型</param>
    /// <returns>服务实例</returns>
    public virtual Object? GetService(Type serviceType)
    {
        if (serviceType == typeof(IServiceProvider)) return this;
        if (serviceType == typeof(NetSession)) return this;
        if (serviceType == typeof(INetSession)) return this;
        if (serviceType == typeof(NetServer)) return ((INetSession)this).Host;
        if (serviceType == typeof(ISocketSession)) return Session;
        if (serviceType == typeof(ISocketServer)) return Server;

        return ServiceProvider?.GetService(serviceType);
    }
    #endregion
}