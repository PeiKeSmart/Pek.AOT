using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Pek;
using Pek.Data;
using Pek.Extension;
using Pek.Log;
using Pek.Model;

namespace Pek.Net;

/// <summary>网络服务器。可同时支持多个Socket服务器，同时支持IPv4和IPv6，同时支持Tcp和Udp</summary>
public class NetServer : DisposeBase, IServer, IExtend, ILogFeature
{
    #region 属性
    /// <summary>服务名</summary>
    public String Name { get; set; }

    private NetUri _local = new();

    /// <summary>本地绑定地址</summary>
    public NetUri Local
    {
        get => _local;
        set
        {
            _local = value;
            if (AddressFamily <= AddressFamily.Unspecified && value.Host != "*")
                AddressFamily = value.Address.AddressFamily;
        }
    }

    /// <summary>监听端口</summary>
    public Int32 Port { get => _local.Port; set => _local.Port = value; }

    /// <summary>协议类型</summary>
    public NetType ProtocolType { get => _local.Type; set => _local.Type = value; }

    /// <summary>地址族</summary>
    public AddressFamily AddressFamily { get; set; }

    /// <summary>底层Socket服务器集合</summary>
    public IList<ISocketServer> Servers { get; private set; } = [];

    /// <summary>默认Socket服务器</summary>
    public ISocketServer? Server
    {
        get => Servers.Count > 0 ? Servers[0] : null;
        set
        {
            if (value == null)
                Servers.Clear();
            else if (!Servers.Contains(value))
                Servers.Insert(0, value);
        }
    }

    /// <summary>是否活动</summary>
    public Boolean Active { get; protected set; }

    /// <summary>会话超时时间（秒）</summary>
    public Int32 SessionTimeout { get; set; }

    /// <summary>消息管道</summary>
    public IPipeline? Pipeline { get; set; }

    /// <summary>是否使用会话集合</summary>
    public Boolean UseSession { get; set; } = true;

    /// <summary>地址重用</summary>
    public Boolean ReuseAddress { get; set; }

    /// <summary>SSL协议版本</summary>
    public SslProtocols SslProtocol { get; set; } = SslProtocols.None;

    /// <summary>X509证书</summary>
    public X509Certificate? Certificate { get; set; }

    /// <summary>APM性能追踪器</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>Socket层性能追踪器</summary>
    public ITracer? SocketTracer { get; set; }

    /// <summary>Socket层日志</summary>
    public ILog? SocketLog { get; set; }

    /// <summary>是否输出发送日志</summary>
    public Boolean LogSend { get; set; }

    /// <summary>是否输出接收日志</summary>
    public Boolean LogReceive { get; set; }

    /// <summary>服务提供者</summary>
    public IServiceProvider? ServiceProvider { get; set; }

    private ConcurrentDictionary<String, Object?>? _items;

    /// <summary>扩展数据字典</summary>
    public IDictionary<String, Object?> Items => _items ??= new();

    /// <summary>获取/设置扩展数据</summary>
    /// <param name="key">数据键名</param>
    /// <returns>数据值，不存在时返回null</returns>
    public Object? this[String key] { get => _items != null && _items.TryGetValue(key, out var obj) ? obj : null; set => Items[key] = value; }

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>会话日志</summary>
    public ILog? SessionLog { get; set; }

    private readonly ConcurrentDictionary<Int32, INetSession> _sessions = new();

    /// <summary>会话集合</summary>
    public IDictionary<Int32, INetSession> Sessions => _sessions;

    private Int32 _sessionCount;

    /// <summary>当前会话数</summary>
    public Int32 SessionCount { get => _sessionCount; set => _sessionCount = value; }

    private Int32 _maxSessionCount;

    /// <summary>最高会话数</summary>
    public Int32 MaxSessionCount => _maxSessionCount;
    #endregion

    #region 构造
    /// <summary>实例化一个网络服务器</summary>
    public NetServer()
    {
        Name = GetType().Name.TrimEnd("Server");

        if (SocketSetting.Current.Debug) Log = XTrace.Log;
    }

    /// <summary>通过指定端口实例化一个网络服务器</summary>
    /// <param name="port">监听端口</param>
    public NetServer(Int32 port) : this(IPAddress.Any, port) { }

    /// <summary>通过指定监听地址和端口实例化一个网络服务器</summary>
    /// <param name="address">监听地址</param>
    /// <param name="port">监听端口</param>
    public NetServer(IPAddress address, Int32 port) : this(address, port, NetType.Unknown) { }

    /// <summary>通过指定监听地址、端口和协议实例化一个网络服务器</summary>
    /// <param name="address">监听地址</param>
    /// <param name="port">监听端口</param>
    /// <param name="protocolType">协议类型</param>
    public NetServer(IPAddress address, Int32 port, NetType protocolType) : this()
    {
        Local.Address = address;
        Port = port;
        Local.Type = protocolType;
    }

    /// <summary>释放资源</summary>
    /// <param name="disposing">是否显式释放</param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        if (Active) Stop(GetType().Name + (disposing ? "Dispose" : "GC"));
    }
    #endregion

    #region 方法
    /// <summary>添加Socket服务器</summary>
    /// <param name="server">要添加的Socket服务器</param>
    /// <returns>是否成功</returns>
    public virtual Boolean AttachServer(ISocketServer server)
    {
        if (Servers.Contains(server)) return false;

        server.Name = $"{Name}{(server.Local.IsTcp ? "Tcp" : "Udp")}{(server.Local.Address.IsIPv4() ? String.Empty : "6")}";
        server.NewSession += Server_NewSession;

        if (SessionTimeout > 0) server.SessionTimeout = SessionTimeout;
        if (Pipeline != null) server.Pipeline = Pipeline;

        server.Log = SocketLog ?? Log;
        if (SocketTracer != null) server.Tracer = SocketTracer;
        server.LogSend = LogSend;
        server.LogReceive = LogReceive;
        server.Error += OnError;

        if (server is TcpServer tcpServer)
        {
            tcpServer.ReuseAddress = ReuseAddress;
            tcpServer.SslProtocol = SslProtocol;
            if (Certificate != null) tcpServer.Certificate = Certificate;
        }
        else if (server is UdpServer udpServer)
        {
            udpServer.ReuseAddress = ReuseAddress;
        }

        Servers.Add(server);
        return true;
    }

    /// <summary>添加服务器监听</summary>
    /// <param name="address">监听地址</param>
    /// <param name="port">监听端口</param>
    /// <param name="protocol">协议类型</param>
    /// <param name="family">地址族</param>
    /// <returns>添加数量</returns>
    public virtual Int32 AddServer(IPAddress address, Int32 port, NetType protocol = NetType.Unknown, AddressFamily family = AddressFamily.Unspecified)
    {
        var list = CreateServer(address, port, protocol, family);
        var count = 0;
        foreach (var item in list)
        {
            AttachServer(item);
            count++;
        }

        return count;
    }

    /// <summary>确保创建服务器</summary>
    public virtual void EnsureCreateServer()
    {
        if (Servers.Count > 0) return;

        var uri = Local;
        var family = AddressFamily;
        if (family <= AddressFamily.Unspecified && uri.Host != "*" && !uri.Address.IsAny())
            family = uri.Address.AddressFamily;

        var list = CreateServer(uri.Address, uri.Port, uri.Type, family);
        foreach (var item in list)
        {
            AttachServer(item);
        }
    }

    /// <summary>添加管道处理器</summary>
    /// <typeparam name="THandler">处理器类型</typeparam>
    public void Add<THandler>() where THandler : IPipelineHandler, new() => GetPipe().Add(new THandler());

    /// <summary>添加管道处理器</summary>
    /// <param name="handler">处理器实例</param>
    public void Add(IPipelineHandler handler) => GetPipe().Add(handler);

    private IPipeline GetPipe() => Pipeline ??= new Pipeline();

    /// <summary>开始服务</summary>
    public void Start()
    {
        if (Active) return;

        OnStart();

        if (Server == null)
        {
            this.WriteLog("没有可用Socket服务器！");
            return;
        }

        Local.Type = Server.Local.Type;
        this.WriteLog("准备就绪！");
    }

    /// <summary>开始时调用的方法</summary>
    protected virtual void OnStart()
    {
        EnsureCreateServer();

        if (Servers.Count == 0) throw new Exception($"Failed to listen to all ports! Port=[{Port}]");

        var snapshot = Servers.ToArray();
        this.WriteLog("准备开始监听{0}个服务器", snapshot.Length);

        foreach (var item in snapshot)
        {
            item.Start();

            if (Port == 0)
            {
                Port = item.Port;

                foreach (var other in Servers)
                {
                    if (!ReferenceEquals(other, item) && other.Port == 0) other.Port = Port;
                }
            }

            this.WriteLog("开始监听 {0}", item);
        }

        if (Pipeline is Pipeline pipe && pipe.Handlers.Count > 0)
        {
            this.WriteLog("初始化管道：");
            foreach (var handler in pipe.Handlers)
            {
                this.WriteLog("    {0}", handler);
            }
        }

        Active = Servers.Any(e => e.Active);
    }

    /// <summary>停止服务</summary>
    /// <param name="reason">关闭原因</param>
    public void Stop(String? reason)
    {
        var activeServers = Servers.Where(e => e.Active).ToArray();
        if (activeServers.Length == 0)
        {
            Active = false;
            return;
        }

        OnStop(reason);
        this.WriteLog("已停止！");
    }

    /// <summary>停止时调用的方法</summary>
    /// <param name="reason">关闭原因</param>
    protected virtual void OnStop(String? reason)
    {
        var activeServers = Servers.Where(e => e.Active).ToArray();
        this.WriteLog("准备停止监听{0}个服务器 {1}", activeServers.Length, reason);

        if (String.IsNullOrEmpty(reason)) reason = GetType().Name + "Stop";
        foreach (var item in activeServers)
        {
            this.WriteLog("停止监听 {0}", item);
            item.Stop(reason);
        }

        if (_sessions.Count > 0)
        {
            this.WriteLog("准备释放网络会话{0}个！", _sessions.Count);
            foreach (var item in _sessions.Values.ToArray())
            {
                item.TryDispose();
            }

            _sessions.Clear();
        }

        if (Servers.Count > 0)
        {
            this.WriteLog("准备释放服务{0}个！", Servers.Count);
            foreach (var item in Servers)
            {
                item.TryDispose();
            }

            Servers.Clear();
        }

        Active = false;
    }

    /// <summary>新会话事件</summary>
    public event EventHandler<NetSessionEventArgs>? NewSession;

    /// <summary>数据接收事件</summary>
    public event EventHandler<ReceivedEventArgs>? Received;

    /// <summary>错误事件</summary>
    public event EventHandler<ExceptionEventArgs>? Error;

    private void Server_NewSession(Object? sender, SessionEventArgs e)
    {
        var session = OnNewSession(e.Session);
        NewSession?.Invoke(sender, new NetSessionEventArgs { Session = session });
    }

    protected virtual INetSession OnNewSession(ISocketSession session)
    {
        var count = Interlocked.Increment(ref _sessionCount);
        session.OnDisposed += (s, e) => Interlocked.Decrement(ref _sessionCount);

        var max = _maxSessionCount;
        while (count > max)
        {
            if (Interlocked.CompareExchange(ref _maxSessionCount, count, max) == max) break;
            max = _maxSessionCount;
        }

        var netSession = CreateSession(session);
        if (netSession is NetSession typedSession)
        {
            typedSession.ID = Interlocked.Increment(ref _sessionID);
            typedSession.Log = SessionLog ?? Log;
        }

        netSession.Host = this;
        netSession.Server = session.Server;
        netSession.Session = session;

        if (UseSession) AddSession(netSession);

        netSession.Received += OnReceived;
        netSession.Start();

        return netSession;
    }

    private Int32 _sessionID;

    protected virtual void OnReceived(Object? sender, ReceivedEventArgs e)
    {
        if (sender is INetSession session)
        {
            if (e.Packet != null) OnReceive(session, e.Packet);
            OnReceive(session, e);
        }

        Received?.Invoke(sender, e);
    }

    /// <summary>收到数据包时的处理</summary>
    /// <param name="session">网络会话</param>
    /// <param name="packet">数据包</param>
    protected virtual void OnReceive(INetSession session, IPacket packet) { }

    /// <summary>收到数据时的处理</summary>
    /// <param name="session">网络会话</param>
    /// <param name="e">接收事件参数</param>
    protected virtual void OnReceive(INetSession session, ReceivedEventArgs e) { }

    protected virtual void OnError(Object? sender, ExceptionEventArgs e)
    {
        if (Log.Enable) Log.Error("{0} Error {1}", sender, e.Exception);

        Error?.Invoke(sender, e);
    }

    protected virtual void AddSession(INetSession session)
    {
        session.Host = this;

        if (_sessions.TryAdd(session.ID, session))
        {
            session.OnDisposed += (s, e) =>
            {
                if (s is INetSession netSession) _sessions.TryRemove(netSession.ID, out _);
            };
        }
        else if (Log.Enable)
        {
            Log.Warn("会话已存在，忽略重复添加。ID={0}", session.ID);
        }
    }

    protected virtual INetSession CreateSession(ISocketSession session)
    {
        var netSession = session.Server.Local.Type == NetType.WebSocket
            ? new WebSocketSession()
            : new NetSession();

        netSession.Server = session.Server;
        netSession.Session = session;

        ((INetSession)netSession).Host = this;

        return netSession;
    }

    /// <summary>根据会话ID查找会话</summary>
    /// <param name="sessionID">会话ID</param>
    /// <returns>网络会话</returns>
    public INetSession? GetSession(Int32 sessionID)
    {
        if (sessionID == 0) return null;

        return _sessions.TryGetValue(sessionID, out var session) ? session : null;
    }

    /// <summary>异步群发数据给所有客户端</summary>
    /// <param name="data">数据包</param>
    /// <param name="predicate">过滤器</param>
    /// <returns>发送数量</returns>
    public virtual Task<Int32> SendAllAsync(IPacket data, Func<INetSession, Boolean>? predicate = null)
    {
        if (!UseSession) throw new ArgumentOutOfRangeException(nameof(UseSession), true, "Mass posting requires the use of session collections");

        var count = 0;
        foreach (var session in _sessions.Values)
        {
            if (predicate != null && !predicate(session)) continue;

            try
            {
                session.Send(data);
                count++;
            }
            catch { }
        }

        return Task.FromResult(count);
    }

    /// <summary>异步群发数据给所有客户端</summary>
    /// <param name="data">数据包</param>
    /// <returns>发送数量</returns>
    public virtual Task<Int32> SendAllAsync(IPacket data) => SendAllAsync(data, null);

    /// <summary>群发消息给所有客户端</summary>
    /// <param name="message">消息</param>
    /// <param name="predicate">过滤器</param>
    /// <returns>发送数量</returns>
    public virtual Int32 SendAllMessage(Object message, Func<INetSession, Boolean>? predicate = null)
    {
        if (!UseSession) throw new ArgumentOutOfRangeException(nameof(UseSession), true, "Mass posting requires the use of session collections");

        var count = 0;
        foreach (var session in _sessions.Values)
        {
            if (predicate != null && !predicate(session)) continue;

            try
            {
                session.SendMessage(message);
                count++;
            }
            catch { }
        }

        return count;
    }

    /// <summary>创建服务器集合</summary>
    /// <param name="address">监听地址</param>
    /// <param name="port">监听端口</param>
    /// <param name="protocol">协议类型</param>
    /// <param name="family">地址族</param>
    /// <returns>服务器数组</returns>
    protected ISocketServer[] CreateServer(IPAddress address, Int32 port, NetType protocol, AddressFamily family)
    {
        switch (protocol)
        {
            case NetType.Tcp:
                return CreateServer<TcpServer>(address, port, family);
            case NetType.Http:
            case NetType.WebSocket:
                var tcpServers = CreateServer<TcpServer>(address, port, family);
                foreach (var item in tcpServers)
                {
                    item.Local.Type = protocol;
                    if (item is TcpServer tcpServer) tcpServer.EnableHttp = true;
                }
                return tcpServers;
            case NetType.Udp:
                return CreateServer<UdpServer>(address, port, family);
            case NetType.Unknown:
            default:
                var list = new List<ISocketServer>();
                list.AddRange(CreateServer<TcpServer>(address, port, family));
                list.AddRange(CreateServer<UdpServer>(address, port, family));
                return list.ToArray();
        }
    }

    private ISocketServer[] CreateServer<TServer>(IPAddress address, Int32 port, AddressFamily family) where TServer : ISocketServer, new()
    {
        var list = new List<ISocketServer>();
        switch (family)
        {
            case AddressFamily.InterNetwork:
            case AddressFamily.InterNetworkV6:
                var actualAddress = address.GetRightAny(family);
                var server = new TServer();
                server.Local.Address = actualAddress;
                server.Local.Port = port;
                list.Add(server);
                break;
            default:
                list.AddRange(CreateServer<TServer>(address, port, AddressFamily.InterNetwork));
                if (Socket.OSSupportsIPv6 && !Runtime.Mono)
                    list.AddRange(CreateServer<TServer>(address, port, AddressFamily.InterNetworkV6));
                break;
        }

        return list.ToArray();
    }

    /// <summary>为会话创建网络数据处理器</summary>
    /// <remarks>可作为业务处理实现，也可以作为前置协议解析。子类可重载返回自定义处理器</remarks>
    /// <param name="session">网络会话</param>
    /// <returns>处理器实例，默认返回null</returns>
    public virtual INetHandler? CreateHandler(INetSession session) => null;
    #endregion
}