using System.Collections.Concurrent;

using Pek;
using Pek.Data;
using Pek.Extension;
using Pek.Log;
using Pek.Model;
using Pek.Threading;

namespace Pek.Net;

/// <summary>网络客户端</summary>
public class NetClient : DisposeBase, IExtend, ILogFeature, ITracerFeature
{
    #region 属性
    /// <summary>名称</summary>
    public String Name { get; set; }

    /// <summary>服务端地址字符串</summary>
    public String? Server
    {
        get => Remote?.ToString();
        set => Remote = value.IsNullOrEmpty() ? null : new NetUri(value!);
    }

    /// <summary>远程服务端地址</summary>
    public NetUri? Remote { get; set; }

    private volatile ISocketClient? _client;

    /// <summary>当前内部Socket客户端</summary>
    public ISocketClient? Client => _client;

    /// <summary>是否已连接</summary>
    public Boolean Active => _client?.Active ?? false;

    /// <summary>本地绑定地址</summary>
    public NetUri Local { get; set; } = new NetUri();

    /// <summary>本地端口</summary>
    public Int32 Port { get => Local.Port; set => Local.Port = value; }

    /// <summary>超时时间</summary>
    public Int32 Timeout { get; set; } = 3_000;

    /// <summary>是否自动重连</summary>
    public Boolean AutoReconnect { get; set; } = true;

    /// <summary>重连延迟</summary>
    public Int32 ReconnectDelay { get; set; } = 5_000;

    /// <summary>最大重连次数。0表示无限重连</summary>
    public Int32 MaxReconnect { get; set; }

    /// <summary>消息管道</summary>
    public IPipeline? Pipeline { get; set; }

    /// <summary>链路追踪器</summary>
    public ITracer? Tracer { get; set; }
    #endregion

    #region 构造
    /// <summary>实例化网络客户端</summary>
    public NetClient() => Name = GetType().Name;

    /// <summary>通过服务端地址实例化</summary>
    /// <param name="server">服务端地址</param>
    public NetClient(String server) : this() => Server = server;

    /// <summary>通过远程地址实例化</summary>
    /// <param name="remote">远程地址</param>
    public NetClient(NetUri remote) : this() => Remote = remote;

    /// <summary>释放资源</summary>
    /// <param name="disposing">是否显式释放</param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        StopReconnect();

        var client = Interlocked.Exchange(ref _client, null);
        if (client == null) return;

        Detach(client);
        client.TryDispose();
    }

    /// <summary>返回字符串表示</summary>
    /// <returns>远程地址或名称</returns>
    public override String ToString() => Remote?.ToString() ?? Name;
    #endregion

    #region 连接管理
    private volatile Boolean _userClosed;

    /// <summary>打开连接</summary>
    /// <returns>是否成功</returns>
    public Boolean Open()
    {
        if (Disposed) return false;
        if (_client != null && _client.Active) return true;

        _userClosed = false;
        try
        {
            var client = CreateClient();
            if (!client.Open())
            {
                client.TryDispose();
                return false;
            }

            _client = client;
            return true;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>异步打开连接</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    public async Task<Boolean> OpenAsync(CancellationToken cancellationToken = default)
    {
        if (Disposed) return false;
        if (_client != null && _client.Active) return true;

        _userClosed = false;
        try
        {
            var client = CreateClient();
            if (!await client.OpenAsync(cancellationToken).ConfigureAwait(false))
            {
                client.TryDispose();
                return false;
            }

            _client = client;
            return true;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>关闭连接</summary>
    /// <param name="reason">关闭原因</param>
    /// <returns>是否成功</returns>
    public Boolean Close(String reason)
    {
        _userClosed = true;
        StopReconnect();

        var client = Interlocked.Exchange(ref _client, null);
        if (client == null) return true;

        Detach(client);
        var result = client.Close(reason);
        client.TryDispose();

        Closed?.Invoke(this, EventArgs.Empty);
        return result;
    }

    /// <summary>异步关闭连接</summary>
    /// <param name="reason">关闭原因</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    public async Task<Boolean> CloseAsync(String reason, CancellationToken cancellationToken = default)
    {
        _userClosed = true;
        StopReconnect();

        var client = Interlocked.Exchange(ref _client, null);
        if (client == null) return true;

        Detach(client);
        var result = await client.CloseAsync(reason, cancellationToken).ConfigureAwait(false);
        client.TryDispose();

        Closed?.Invoke(this, EventArgs.Empty);
        return result;
    }

    /// <summary>创建并初始化内部客户端</summary>
    /// <returns>Socket客户端</returns>
    protected virtual ISocketClient CreateClient()
    {
        var remote = Remote ?? throw new InvalidOperationException("未设置远程地址，请先设置 Server 或 Remote 属性");

        var client = remote.CreateRemote();
        client.Name = Name;
        client.Timeout = Timeout;
        client.Log = Log;

        if (Pipeline != null) client.Pipeline = Pipeline;
        if (Tracer != null) client.Tracer = Tracer;
        if (Local.Port > 0) client.Local = Local;

        Attach(client);
        return client;
    }

    private void Attach(ISocketClient client)
    {
        client.Received += OnClientReceived;
        client.Opened += OnClientOpened;
        client.Closed += OnClientClosed;
        client.Error += OnClientError;
    }

    private void Detach(ISocketClient client)
    {
        client.Received -= OnClientReceived;
        client.Opened -= OnClientOpened;
        client.Closed -= OnClientClosed;
        client.Error -= OnClientError;
    }
    #endregion

    #region 断线重连
    private TimerX? _reconnectTimer;
    private volatile Int32 _reconnectCount;

    private void StopReconnect()
    {
        var timer = Interlocked.Exchange(ref _reconnectTimer, null);
        timer?.TryDispose();
    }

    private void ScheduleReconnect()
    {
        if (!AutoReconnect || Disposed || _userClosed) return;
        if (_reconnectTimer != null) return;

        if (MaxReconnect > 0 && _reconnectCount >= MaxReconnect)
        {
            WriteLog("已达最大重连次数 {0}，停止重连", MaxReconnect);
            return;
        }

        var delay = ReconnectDelay > 0 ? ReconnectDelay : 5_000;
        WriteLog("连接断开，{0}ms 后发起第 {1} 次重连 {2}", delay, _reconnectCount + 1, Remote);

        _reconnectTimer = new TimerX(DoReconnect, null, delay, 0) { Async = true };
    }

    private async void DoReconnect(Object? state)
    {
        StopReconnect();
        if (Disposed || _userClosed || (_client != null && _client.Active)) return;

        _reconnectCount++;
        WriteLog("正在重连 [{0}] {1}", _reconnectCount, Remote);

        try
        {
            var client = CreateClient();
            if (await client.OpenAsync().ConfigureAwait(false))
            {
                _client = client;
                _reconnectCount = 0;
                WriteLog("重连成功 {0}", Remote);
            }
            else
            {
                client.TryDispose();
                ScheduleReconnect();
            }
        }
        catch (Exception ex)
        {
            WriteLog("重连失败：{0}", ex.Message);
            ScheduleReconnect();
        }
    }
    #endregion

    #region 发送数据
    /// <summary>发送数据包</summary>
    /// <param name="data">数据包</param>
    /// <returns>发送字节数</returns>
    public Int32 Send(IPacket data) => EnsureClient().Send(data);

    /// <summary>发送字节数组</summary>
    /// <param name="data">字节数组</param>
    /// <param name="offset">偏移</param>
    /// <param name="count">数量</param>
    /// <returns>发送字节数</returns>
    public Int32 Send(Byte[] data, Int32 offset = 0, Int32 count = -1) => EnsureClient().Send(data, offset, count);

    /// <summary>发送数组段</summary>
    /// <param name="data">数组段</param>
    /// <returns>发送字节数</returns>
    public Int32 Send(ArraySegment<Byte> data) => EnsureClient().Send(data);

    /// <summary>发送只读内存段</summary>
    /// <param name="data">数据</param>
    /// <returns>发送字节数</returns>
    public Int32 Send(ReadOnlySpan<Byte> data) => EnsureClient().Send(data);

    /// <summary>发送字符串</summary>
    /// <param name="data">字符串</param>
    /// <returns>发送字节数</returns>
    public Int32 Send(String data) => EnsureClient().Send(data);

    /// <summary>发送消息</summary>
    /// <param name="message">消息对象</param>
    /// <returns>发送字节数</returns>
    public Int32 SendMessage(Object message) => EnsureClient().SendMessage(message);

#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
    /// <summary>异步发送消息并等待响应</summary>
    /// <param name="message">消息对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应消息</returns>
    public ValueTask<Object> SendMessageAsync(Object message, CancellationToken cancellationToken = default) => EnsureClient().SendMessageAsync(message, cancellationToken);
#else
    /// <summary>异步发送消息并等待响应</summary>
    /// <param name="message">消息对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应消息</returns>
    public Task<Object> SendMessageAsync(Object message, CancellationToken cancellationToken = default) => EnsureClient().SendMessageAsync(message, cancellationToken);
#endif

    private ISocketClient EnsureClient()
    {
        var client = _client;
        if (client != null && client.Active) return client;

        if (!AutoReconnect)
            throw new InvalidOperationException($"网络客户端 [{Name}] 未连接，请先调用 Open()");

        Open();
        return _client ?? throw new InvalidOperationException($"网络客户端 [{Name}] 未连接，且连接尝试失败");
    }
    #endregion

    #region 接收数据
    /// <summary>同步接收数据包</summary>
    /// <returns>数据包</returns>
    public IOwnerPacket? Receive() => EnsureClient().Receive();

    /// <summary>异步接收数据包</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>数据包</returns>
    public Task<IOwnerPacket?> ReceiveAsync(CancellationToken cancellationToken = default) => EnsureClient().ReceiveAsync(cancellationToken);
    #endregion

    #region 事件转发
    /// <summary>连接打开事件</summary>
    public event EventHandler? Opened;

    /// <summary>连接关闭事件</summary>
    public event EventHandler? Closed;

    /// <summary>数据接收事件</summary>
    public event EventHandler<ReceivedEventArgs>? Received;

    /// <summary>错误事件</summary>
    public event EventHandler<ExceptionEventArgs>? Error;

    private void OnClientOpened(Object? sender, EventArgs e)
    {
        StopReconnect();
        Opened?.Invoke(this, e);
    }

    private void OnClientClosed(Object? sender, EventArgs e)
    {
        Closed?.Invoke(this, e);

        if (!_userClosed) ScheduleReconnect();
    }

    private void OnClientReceived(Object? sender, ReceivedEventArgs e) => Received?.Invoke(this, e);

    private void OnClientError(Object? sender, ExceptionEventArgs e)
    {
        Error?.Invoke(this, e);

        if (!_userClosed && (e.Action == "Disconnect" || e.Action == "Close" || e.Action == "Receive"))
            ScheduleReconnect();
    }
    #endregion

    #region 编解码器
    /// <summary>添加管道处理器</summary>
    /// <param name="handler">处理器</param>
    /// <returns>当前实例</returns>
    public NetClient Add(IPipelineHandler handler)
    {
        (Pipeline ??= new Pipeline()).Add(handler);
        return this;
    }

    /// <summary>添加管道处理器</summary>
    /// <typeparam name="T">处理器类型</typeparam>
    /// <returns>当前实例</returns>
    public NetClient Add<T>() where T : IPipelineHandler, new() => Add(new T());
    #endregion

    #region 扩展数据
    private ConcurrentDictionary<String, Object?>? _items;

    /// <summary>扩展数据字典</summary>
    public IDictionary<String, Object?> Items => _items ??= new ConcurrentDictionary<String, Object?>();

    /// <summary>获取或设置扩展数据</summary>
    /// <param name="key">键名</param>
    /// <returns>扩展值</returns>
    public Object? this[String key]
    {
        get => _items != null && _items.TryGetValue(key, out var obj) ? obj : null;
        set => Items[key] = value;
    }
    #endregion

    #region 日志
    /// <summary>日志对象</summary>
    public ILog Log { get; set; } = Logger.Null;

    private String? _logPrefix;

    /// <summary>日志前缀</summary>
    public virtual String LogPrefix
    {
        get
        {
            _logPrefix ??= $"{Name} ";
            return _logPrefix;
        }
        set => _logPrefix = value;
    }

    /// <summary>输出日志</summary>
    /// <param name="format">格式</param>
    /// <param name="args">参数</param>
    public virtual void WriteLog(String format, params Object?[] args) => Log.Info(LogPrefix + format, args);
    #endregion
}