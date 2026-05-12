using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using Pek;
using Pek.Extension;
using Pek.Log;
using Pek.Model;

namespace Pek.Net;

/// <summary>TCP服务器</summary>
public class TcpServer : DisposeBase, ISocketServer, ILogFeature
{
    /// <summary>名称</summary>
    public String Name { get; set; }

    /// <summary>本地绑定信息</summary>
    public NetUri Local { get; set; }

    /// <summary>端口</summary>
    public Int32 Port { get => Local.Port; set => Local.Port = value; }

    /// <summary>会话超时时间</summary>
    public Int32 SessionTimeout { get; set; }

    /// <summary>底层Socket</summary>
    public Socket? Client { get; private set; }

    /// <summary>是否活动</summary>
    public Boolean Active { get; set; }

    /// <summary>最大并行接受连接数</summary>
    public Int32 MaxAsync { get; set; }

    /// <summary>不延迟直接发送</summary>
    public Boolean NoDelay { get; set; } = true;

    /// <summary>地址重用</summary>
    public Boolean ReuseAddress { get; set; }

    /// <summary>KeepAlive间隔</summary>
    public Int32 KeepAliveInterval { get; set; }

    /// <summary>启用Http</summary>
    public Boolean EnableHttp { get; set; }

    /// <summary>消息管道</summary>
    public IPipeline? Pipeline { get; set; }

    /// <summary>SSL协议版本</summary>
    public SslProtocols SslProtocol { get; set; } = SslProtocols.None;

    /// <summary>X509证书</summary>
    public X509Certificate? Certificate { get; set; }

    /// <summary>链路追踪器</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>实例化TCP服务器</summary>
    public TcpServer()
    {
        Name = GetType().Name;

        Local = new NetUri(NetType.Tcp, IPAddress.Any, 0);
        SessionTimeout = SocketSetting.Current.SessionTimeout;
        MaxAsync = Environment.ProcessorCount * 16 / 10;
        _sessions = new SessionCollection(this);

        if (SocketSetting.Current.Debug) Log = XTrace.Log;
    }

    /// <summary>使用端口实例化TCP服务器</summary>
    /// <param name="port">监听端口</param>
    public TcpServer(Int32 port) : this() => Port = port;

    /// <summary>释放资源</summary>
    /// <param name="disposing">是否显式释放</param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        if (Active) Stop(GetType().Name + (disposing ? "Dispose" : "GC"));

        _sessions.Dispose();
    }

    /// <summary>开始监听</summary>
    public virtual void Start()
    {
        if (Disposed) throw new ObjectDisposedException(GetType().Name);
        if (Active || Disposed) return;

        using var span = Tracer?.NewSpan($"net:{Name}:Start");
        try
        {
            var socket = Client;
            if (socket == null) Client = socket = NetHelper.CreateTcp(Local.Address.IsIPv4());

            try
            {
                if (ReuseAddress) socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            }
            catch (Exception ex)
            {
                XTrace.WriteLine(ex.Message);
            }

            WriteLog("Start {0}", this);

            socket.Bind(Local.EndPoint);
            socket.Listen(65535);

            if (Local.Port == 0 && socket.LocalEndPoint is IPEndPoint endPoint)
                Local.Port = endPoint.Port;

            if (Runtime.Windows)
            {
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);
            }

            Active = true;

            for (var i = 0; i < MaxAsync; i++)
            {
                var socketEventArgs = new SocketAsyncEventArgs();
                socketEventArgs.Completed += (s, e) => ProcessAccept(e);

                StartAccept(socketEventArgs, false);
            }
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }

    /// <summary>停止监听</summary>
    /// <param name="reason">关闭原因</param>
    public virtual void Stop(String? reason)
    {
        if (!Active) return;

        WriteLog("Stop {0} {1}", reason, this);

        using var span = Tracer?.NewSpan($"net:{Name}:Stop");
        try
        {
            Active = false;

            CloseAllSession();

            Client?.Shutdown();
            Client = null;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }

    /// <summary>新会话事件</summary>
    public event EventHandler<SessionEventArgs>? NewSession;

    private Boolean StartAccept(SocketAsyncEventArgs socketEventArgs, Boolean ioThread)
    {
        if (!Active || Client == null)
        {
            socketEventArgs.Dispose();
            return false;
        }

        using var span = Tracer?.NewSpan($"net:{Name}:StartAccept");
        Boolean result;
        try
        {
            socketEventArgs.AcceptSocket = null;
            result = Client.AcceptAsync(socketEventArgs);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);

            if (!ex.IsDisposed()) OnError("AcceptAsync", ex);

            if (!ioThread) throw;

            return false;
        }

        if (!result)
        {
            if (ioThread)
                ProcessAccept(socketEventArgs);
            else
                Task.Factory.StartNew(() => ProcessAccept(socketEventArgs), TaskCreationOptions.LongRunning);
        }

        return true;
    }

    private void ProcessAccept(SocketAsyncEventArgs socketEventArgs)
    {
        if (!Active || Client == null)
        {
            socketEventArgs.Dispose();
            return;
        }

        using var span = Tracer?.NewSpan($"net:{Name}:ProcessAccept");

        if (socketEventArgs.SocketError != SocketError.Success)
        {
            var ex = socketEventArgs.GetException();
            if (ex != null) OnError("AcceptAsync", ex);

            socketEventArgs.Dispose();
            return;
        }

        if (socketEventArgs.AcceptSocket != null)
        {
            try
            {
                OnAccept(socketEventArgs.AcceptSocket);
            }
            catch (Exception ex)
            {
                span?.SetError(ex, null);

                if (!ex.IsDisposed()) OnError("EndAccept", ex);
            }
        }

        StartAccept(socketEventArgs, true);
    }

    private Int32 _sessionID;

    /// <summary>收到新连接时处理</summary>
    /// <param name="client">客户端Socket</param>
    protected virtual void OnAccept(Socket client)
    {
        var session = CreateSession(client);

        if (KeepAliveInterval > 0) client.SetTcpKeepAlive(true, KeepAliveInterval, KeepAliveInterval);

        if (_sessions.Add(session))
        {
            session.ID = Interlocked.Increment(ref _sessionID);
            session.WriteLog("New {0}", session.Remote.EndPoint);

            NewSession?.Invoke(this, new SessionEventArgs(session));

            session.SslProtocol = SslProtocol;
            session.Certificate = Certificate;
            session.Tracer = Tracer;
            session.Start();
        }
    }

    private readonly SessionCollection _sessions;

    /// <summary>会话集合</summary>
    public IDictionary<String, ISocketSession> Sessions => _sessions;

    /// <summary>创建会话</summary>
    /// <param name="client">客户端Socket</param>
    /// <returns>会话</returns>
    protected virtual TcpSession CreateSession(Socket client)
    {
        var session = new TcpSession(this, client)
        {
            NoDelay = NoDelay,
            KeepAliveInterval = KeepAliveInterval,
            Pipeline = Pipeline,

            Log = Log,
            LogSend = LogSend,
            LogReceive = LogReceive,
            Tracer = Tracer,
        };

        client.NoDelay = NoDelay;

        return session;
    }

    private void CloseAllSession()
    {
        if (_sessions.Count <= 0) return;

        WriteLog("准备释放会话{0}个！", _sessions.Count);
        _sessions.CloseAll(nameof(CloseAllSession));
        _sessions.Clear();
    }

    /// <summary>错误事件</summary>
    public event EventHandler<ExceptionEventArgs>? Error;

    /// <summary>触发异常</summary>
    /// <param name="action">动作</param>
    /// <param name="exception">异常</param>
    protected virtual void OnError(String action, Exception exception)
    {
        Log?.Error("{0}{1}Error {2} {3}", LogPrefix, action, this, exception.Message);
        Error?.Invoke(this, new ExceptionEventArgs(action, exception));
    }

    private String? _logPrefix;

    /// <summary>日志前缀</summary>
    public virtual String LogPrefix
    {
        get
        {
            if (_logPrefix == null)
            {
                var name = Name == null ? String.Empty : Name.TrimEnd("Server", "Session", "Client");
                _logPrefix = $"{name}.";
            }

            return _logPrefix;
        }
        set => _logPrefix = value;
    }

    /// <summary>日志对象</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>是否输出发送日志</summary>
    public Boolean LogSend { get; set; }

    /// <summary>是否输出接收日志</summary>
    public Boolean LogReceive { get; set; }

    /// <summary>输出日志</summary>
    /// <param name="format">格式</param>
    /// <param name="args">参数</param>
    public void WriteLog(String format, params Object?[] args)
    {
        if (Log != null && Log.Enable) Log.Info(LogPrefix + format, args);
    }

    /// <summary>返回字符串表示</summary>
    /// <returns>服务器标识</returns>
    public override String ToString() => _sessions.Count > 0 ? $"{Local} [{_sessions.Count}]" : Local.ToString();
}