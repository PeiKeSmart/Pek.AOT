using System.Net;
using System.Net.Sockets;

using Pek.Buffers;
using Pek.Collections;
using Pek.Data;
using Pek.Extension;
using Pek.IO;
using Pek.Log;
using Pek.Model;

namespace Pek.Net;

/// <summary>增强UDP服务器</summary>
public class UdpServer : SessionBase, ISocketServer, ILogFeature
{
    /// <summary>会话超时时间</summary>
    public Int32 SessionTimeout { get; set; }

    /// <summary>是否接收自己广播的环回数据</summary>
    public Boolean Loopback { get; set; }

    /// <summary>地址重用</summary>
    public Boolean ReuseAddress { get; set; }

    /// <summary>实例化增强UDP服务器</summary>
    public UdpServer()
    {
        Local.Type = NetType.Udp;
        Remote.Type = NetType.Udp;
        _sessions = new SessionCollection(this);

        SessionTimeout = SocketSetting.Current.SessionTimeout;
        MaxAsync = Environment.ProcessorCount * 16 / 10;

        if (SocketSetting.Current.Debug) Log = XTrace.Log;
    }

    /// <summary>使用监听端口初始化</summary>
    /// <param name="listenPort">监听端口</param>
    public UdpServer(Int32 listenPort) : this() => Port = listenPort;

    /// <summary>释放资源</summary>
    /// <param name="disposing">是否显式释放</param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        if (Active) Close(GetType().Name + (disposing ? "Dispose" : "GC"));

        _sessions.Dispose();
    }

    /// <summary>打开</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    protected override Task<Boolean> OnOpenAsync(CancellationToken cancellationToken)
    {
        var socket = Client;
        if (socket == null || !socket.IsBound)
        {
            var uri = Remote;
            if (Local.Address.IsAny() && uri != null && !uri.Address.IsAny())
                Local.Address = Local.Address.GetRightAny(uri.Address.AddressFamily);

            Client = socket = NetHelper.CreateUdp(Local.Address.IsIPv4());

            try
            {
                if (ReuseAddress) socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            }
            catch (Exception ex)
            {
                XTrace.WriteLine(ex.Message);
            }

            socket.Bind(Local.EndPoint);
            if (Local.Port == 0 && socket.LocalEndPoint is IPEndPoint endPoint)
                Local.Port = endPoint.Port;

            WriteLog("Open {0}", this);
        }

        return Task.FromResult(true);
    }

    /// <summary>关闭</summary>
    /// <param name="reason">关闭原因</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    protected override Task<Boolean> OnCloseAsync(String reason, CancellationToken cancellationToken)
    {
        var socket = Client;
        if (socket != null)
        {
            WriteLog("Close {0} {1}", reason, this);

            try
            {
                var remote = Remote;
                if (remote != null && !remote.Address.IsAny() && remote.Port != 0)
                    Send(Pool.Empty);

                Client = null;
                CloseAllSession();
                socket.Shutdown();
            }
            catch (Exception ex)
            {
                if (!ex.IsDisposed()) OnError("Close", ex);
                return Task.FromResult(false);
            }
        }

        return Task.FromResult(true);
    }

    /// <summary>发送数据包</summary>
    /// <param name="data">数据包</param>
    /// <returns>发送字节数</returns>
    protected override Int32 OnSend(IPacket data) => OnSend(data, Remote.EndPoint);

    /// <summary>发送数组段</summary>
    /// <param name="data">数组段</param>
    /// <returns>发送字节数</returns>
    protected override Int32 OnSend(ArraySegment<Byte> data) => OnSend(data, Remote.EndPoint);

    /// <summary>发送只读数据</summary>
    /// <param name="data">数据</param>
    /// <returns>发送字节数</returns>
    protected override Int32 OnSend(ReadOnlySpan<Byte> data) => OnSend(data, Remote.EndPoint);

    internal Int32 OnSend(IPacket packet, IPEndPoint remote)
    {
        var count = packet.Total;

        using var span = Tracer?.NewSpan($"net:{Name}:Send", count + String.Empty, count);
        try
        {
            var result = 0;
            var socket = Client ?? throw new InvalidOperationException(nameof(OnSend));
            lock (socket)
            {
                var connected = socket.Connected;
                if (Runtime.Mono && Runtime.Linux)
                {
                    try
                    {
                        var endPoint = socket.RemoteEndPoint;
                        connected = endPoint is IPEndPoint ipEndPoint && !ipEndPoint.Address.IsAny() && ipEndPoint.Port > 0;
                    }
                    catch
                    {
                        connected = false;
                    }
                }

                if (connected && !socket.EnableBroadcast)
                {
                    if (Log.Enable && LogSend) WriteLog("Send [{0}]: {1}", count, packet.ToHex(LogDataLength));

                    if (count == 0)
                        result = socket.Send(Pool.Empty);
                    else if (packet.Next == null && packet.TryGetArray(out var segment))
                        result = socket.Send(segment.Array!, segment.Offset, segment.Count, SocketFlags.None);
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
                    else if (packet.TryGetSpan(out var data))
                        result = socket.Send(data);
#endif
                    else
                        result = socket.Send(packet.ToSegments(), SocketFlags.None);
                }
                else
                {
                    socket.CheckBroadcast(remote.Address);
                    if (Log.Enable && LogSend) WriteLog("Send {2} [{0}]: {1}", count, packet.ToHex(LogDataLength), remote);

                    if (count == 0)
                        result = socket.SendTo(Pool.Empty, remote);
                    else if (packet.Next == null && packet.TryGetArray(out var segment))
                        result = socket.SendTo(segment.Array!, segment.Offset, segment.Count, SocketFlags.None, remote);
#if NET6_0_OR_GREATER
                    else if (packet.TryGetSpan(out var data))
                        result = socket.SendTo(data, remote);
#endif
                    else
                        result = socket.SendTo(packet.ToArray(), 0, count, SocketFlags.None, remote);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, packet);
            if (!ex.IsDisposed()) OnError("Send", ex);
            return -1;
        }
    }

    internal Int32 OnSend(Byte[] data, Int32 offset, Int32 count, IPEndPoint remote)
    {
#if NET6_0_OR_GREATER
        return OnSend(new ReadOnlySpan<Byte>(data, offset, count), remote);
#else
        return OnSend(new ArraySegment<Byte>(data, offset, count), remote);
#endif
    }

    internal Int32 OnSend(ArraySegment<Byte> data, IPEndPoint remote)
    {
        var count = data.Count;
        var logCount = count > LogDataLength ? count : LogDataLength;

        using var span = Tracer?.NewSpan($"net:{Name}:Send", count + String.Empty, count);
        try
        {
            var result = 0;
            var socket = Client ?? throw new InvalidOperationException(nameof(OnSend));
            lock (socket)
            {
                if (socket.Connected && !socket.EnableBroadcast)
                {
                    if (Log.Enable && LogSend) WriteLog("Send [{0}]: {1}", count, data.Array?.ToHex(data.Offset, logCount));

                    result = socket.Send(data.Array!, data.Offset, data.Count, SocketFlags.None);
                }
                else
                {
                    socket.CheckBroadcast(remote.Address);
                    if (Log.Enable && LogSend) WriteLog("Send {2} [{0}]: {1}", count, data.Array?.ToHex(data.Offset, logCount), remote);

                    result = socket.SendTo(data.Array!, data.Offset, data.Count, SocketFlags.None, remote);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, data.Array?.ToHex(data.Offset, data.Count));
            if (!ex.IsDisposed()) OnError("Send", ex);
            return -1;
        }
    }

    internal Int32 OnSend(ReadOnlySpan<Byte> data, IPEndPoint remote)
    {
        var count = data.Length;

        using var span = Tracer?.NewSpan($"net:{Name}:Send", count + String.Empty, count);
        try
        {
            var result = 0;
            var socket = Client ?? throw new InvalidOperationException(nameof(OnSend));
            lock (socket)
            {
                if (socket.Connected && !socket.EnableBroadcast)
                {
                    if (Log.Enable && LogSend) WriteLog("Send [{0}]: {1}", count, data.ToHex(LogDataLength));

#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
                    result = socket.Send(data, SocketFlags.None);
#else
                    result = socket.Send(data.ToArray(), SocketFlags.None);
#endif
                }
                else
                {
                    socket.CheckBroadcast(remote.Address);
                    if (Log.Enable && LogSend) WriteLog("Send {2} [{0}]: {1}", count, data.ToHex(LogDataLength), remote);

#if NET6_0_OR_GREATER
                    result = socket.SendTo(data, SocketFlags.None, remote);
#else
                    result = socket.SendTo(data.ToArray(), SocketFlags.None, remote);
#endif
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, data.ToHex());
            if (!ex.IsDisposed()) OnError("Send", ex);
            return -1;
        }
    }

#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
    /// <summary>发送消息并等待响应。必须调用会话发送，否则配对会失败</summary>
    /// <param name="message">消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应消息</returns>
    public override ValueTask<Object> SendMessageAsync(Object message, CancellationToken cancellationToken = default) => CreateSession(null, Remote.EndPoint).SendMessageAsync(message, cancellationToken);
#else
    /// <summary>发送消息并等待响应。必须调用会话发送，否则配对会失败</summary>
    /// <param name="message">消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应消息</returns>
    public override Task<Object> SendMessageAsync(Object message, CancellationToken cancellationToken = default) => CreateSession(null, Remote.EndPoint).SendMessageAsync(message, cancellationToken);
#endif

    internal override Boolean OnReceiveAsync(SocketAsyncEventArgs socketEventArgs)
    {
        if (!Active || Client == null) return false;

        socketEventArgs.RemoteEndPoint = new IPEndPoint(IPAddress.Any.GetRightAny(Local.EndPoint.AddressFamily), 0);
        socketEventArgs.SocketFlags = SocketFlags.None;

        if (Runtime.Mono)
            return Client.ReceiveFromAsync(socketEventArgs);

        return Client.ReceiveMessageFromAsync(socketEventArgs);
    }

    protected internal override ISocketSession? OnPreReceive(IPacket packet, IPAddress local, IPEndPoint remote)
    {
        if (!Loopback && remote.Port == Port)
        {
            if (!Local.Address.IsAny())
            {
                if (remote.Address.Equals(Local.Address)) return null;
            }
            else
            {
                foreach (var item in GetLocalAddresses())
                {
                    if (remote.Address.Equals(item)) return null;
                }
            }
        }

        return CreateSession(local, remote);
    }

    protected override Boolean OnReceive(ReceivedEventArgs e)
    {
        var packet = e.Packet;
        var remote = e.Remote;

        var session = remote == null ? null : CreateSession(e.Local, remote);
        if (session is UdpSession udpSession)
            udpSession.OnReceive(e);
        else if (Log.Enable && LogReceive && packet != null)
            WriteLog("Recv [{0}]: {1}", packet.Length, packet.ToHex(LogDataLength));

        if (session != null) RaiseReceive(session, e);

        return true;
    }

    internal override Boolean OnReceiveError(SocketAsyncEventArgs socketEventArgs)
    {
        if (socketEventArgs.SocketError == SocketError.MessageSize && BufferSize < 1024 * 1024) BufferSize *= 2;

        if (socketEventArgs.SocketError is not SocketError.ConnectionReset and not SocketError.ConnectionAborted)
            return true;

        var endPoint = socketEventArgs.RemoteEndPoint as IPEndPoint;
        var session = endPoint == null ? null : _sessions.Get(endPoint + String.Empty);
        session?.Dispose();

        return false;
    }

    /// <summary>新会话事件</summary>
    public event EventHandler<SessionEventArgs>? NewSession;

    private readonly SessionCollection _sessions;

    /// <summary>会话集合</summary>
    public IDictionary<String, ISocketSession> Sessions => _sessions;

    private readonly Dictionary<Int32, ISocketSession> _broadcasts = [];
    private Int32 _sessionID;

    /// <summary>创建会话</summary>
    /// <param name="local">本地地址</param>
    /// <param name="remoteEndPoint">远程终结点</param>
    /// <returns>会话</returns>
    public virtual ISocketSession CreateSession(IPAddress? local, IPEndPoint remoteEndPoint)
    {
        if (Disposed) throw new ObjectDisposedException(GetType().Name);

        if (!Active)
        {
            Local.Address = Local.Address.GetRightAny(remoteEndPoint.AddressFamily);
            if (!Open()) throw new InvalidOperationException($"Open {Local} error");
        }

        var session = _sessions.Get(remoteEndPoint + String.Empty);
        var port = remoteEndPoint.Port;
        if (session != null || _broadcasts.TryGetValue(port, out session)) return session;

        lock (_sessions)
        {
            session = _sessions.Get(remoteEndPoint + String.Empty);
            if (session != null || _broadcasts.TryGetValue(port, out session)) return session;

            var udpSession = new UdpSession(this, local, remoteEndPoint)
            {
                Log = Log,
                LogSend = LogSend,
                LogReceive = LogReceive,
                Tracer = Tracer,
            };

            session = udpSession;
            if (_sessions.Add(session))
            {
                if (Equals(remoteEndPoint.Address, IPAddress.Broadcast))
                {
                    _broadcasts[port] = session;
                    session.OnDisposed += (s, e) =>
                    {
                        lock (_broadcasts)
                        {
                            if (s is UdpSession removedSession)
                                _broadcasts.Remove(removedSession.Remote.Port);
                        }
                    };
                }

                udpSession.ID = Interlocked.Increment(ref _sessionID);
                udpSession.Tracer = Tracer;
                udpSession.Start();

                NewSession?.Invoke(this, new SessionEventArgs(session));
            }
        }

        return session;
    }

    private void CloseAllSession()
    {
        if (_sessions.Count <= 0) return;

        WriteLog("准备释放会话{0}个！", _sessions.Count);
        _sessions.CloseAll(nameof(CloseAllSession));
        _sessions.Clear();
    }

    void IServer.Start() => Open();

    void IServer.Stop(String? reason) => Close(reason ?? "Stop");

    /// <summary>返回字符串表示</summary>
    /// <returns>服务器标识</returns>
    public override String ToString() => _sessions.Count > 0 ? $"{Local} [{_sessions.Count}]" : Local.ToString();

    private static IPAddress[] GetLocalAddresses()
    {
        try
        {
            return Dns.GetHostAddresses(Dns.GetHostName());
        }
        catch
        {
            return [];
        }
    }
}