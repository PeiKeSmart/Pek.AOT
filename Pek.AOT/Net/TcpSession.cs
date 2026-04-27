using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using Pek.Buffers;
using Pek.Collections;
using Pek.Data;
using Pek.Extension;
using Pek.IO;
using Pek.Log;

namespace Pek.Net;

/// <summary>增强 TCP 会话</summary>
public class TcpSession : SessionBase, ISocketSession
{
    #region 属性
    /// <summary>实际使用的远程地址</summary>
    public IPAddress? RemoteAddress { get; private set; }

    internal ISocketServer? _server;

    /// <summary>Socket 服务器</summary>
    ISocketServer ISocketSession.Server => _server!;

    /// <summary>是否启用 NoDelay</summary>
    public Boolean NoDelay { get; set; }

    /// <summary>KeepAlive 间隔秒数</summary>
    public Int32 KeepAliveInterval { get; set; }

    /// <summary>SSL 协议版本</summary>
    public SslProtocols SslProtocol { get; set; } = SslProtocols.None;

    /// <summary>X509 证书</summary>
    public X509Certificate? Certificate { get; set; }

    private SslStream? _stream;
    #endregion

    #region 构造
    /// <summary>实例化增强 TCP 会话</summary>
    public TcpSession()
    {
        Name = GetType().Name;
        Local.Type = NetType.Tcp;
        Remote.Type = NetType.Tcp;
    }

    /// <summary>使用监听端口初始化</summary>
    /// <param name="listenPort">监听端口</param>
    public TcpSession(Int32 listenPort) : this() => Port = listenPort;

    /// <summary>使用已连接 Socket 初始化</summary>
    /// <param name="client">Socket</param>
    public TcpSession(Socket client) : this()
    {
        Client = client;

        if (client.LocalEndPoint is IPEndPoint local) Local.EndPoint = local;
        if (client.RemoteEndPoint is IPEndPoint remote) Remote.EndPoint = remote;
    }

    internal TcpSession(ISocketServer server, Socket client) : this(client)
    {
        Active = true;
        _server = server;
        Name = server.Name;
    }
    #endregion

    #region 方法
    internal void Start()
    {
        if (Pipeline != null)
        {
            var context = CreateContext(this);
            Pipeline.Open(context);
            ReturnContext(context);
        }

        var socket = Client;
        var timeout = Timeout;
        if (timeout > 0 && socket != null)
        {
            socket.SendTimeout = timeout;
            socket.ReceiveTimeout = timeout;
        }

        var cert = Certificate;
        if (socket != null && cert != null)
        {
            var sslStream = new SslStream(new NetworkStream(socket), false);

            var protocol = SslProtocol;
            if (protocol == SslProtocols.None) protocol = SslProtocols.Tls12;

            WriteLog("服务端SSL认证，SslProtocol={0}，Issuer: {1}", protocol, cert.Issuer);
            sslStream.AuthenticateAsServer(cert, false, protocol, false);
            _stream = sslStream;
        }

        ReceiveAsync();
    }

    /// <summary>打开连接</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    protected override async Task<Boolean> OnOpenAsync(CancellationToken cancellationToken)
    {
        if (_server != null) return false;

        var span = DefaultSpan.Current;
        var timeout = Timeout;
        var uri = Remote;
        var socket = Client;
        if (socket == null || !socket.IsBound)
        {
            span?.AppendTag($"Local={Local}");

            if (Local.Address.IsAny() && !uri.Address.IsAny())
                Local.Address = Local.Address.GetRightAny(uri.Address.AddressFamily);

            socket = Client = NetHelper.CreateTcp(Local.Address.IsIPv4());
            if (NoDelay) socket.NoDelay = true;
            if (timeout > 0)
            {
                socket.SendTimeout = timeout;
                socket.ReceiveTimeout = timeout;
            }

            socket.Bind(Local.EndPoint);
            if (socket.LocalEndPoint is IPEndPoint local) Local.EndPoint.Port = local.Port;
            span?.AppendTag($"LocalEndPoint={socket.LocalEndPoint}");

            WriteLog("Open {0}", this);
        }

        if (uri.EndPoint.IsAny()) return false;

        try
        {
            var addresses = uri.GetAddresses().Where(ip => ip.AddressFamily == socket.AddressFamily).ToArray();
            span?.AppendTag($"addrs={addresses.Join()} port={uri.Port}");

            if (timeout <= 0)
                socket.Connect(addresses, uri.Port);
            else
            {
                using var timeoutSource = new CancellationTokenSource(timeout);
                using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
                using var _ = linkedSource.Token.Register(() => socket.Close());
                await socket.ConnectAsync(addresses, uri.Port, linkedSource.Token).ConfigureAwait(false);
            }

            if (KeepAliveInterval > 0) socket.SetTcpKeepAlive(true, KeepAliveInterval, KeepAliveInterval);

            RemoteAddress = (socket.RemoteEndPoint as IPEndPoint)?.Address;
            span?.AppendTag($"RemoteEndPoint={socket.RemoteEndPoint}");

            var protocol = SslProtocol;
            if (protocol != SslProtocols.None)
            {
                var host = uri.Host ?? uri.Address.ToString();
                WriteLog("客户端SSL认证，SslProtocol={0}，Host={1}", protocol, host);

                var certificates = new X509CertificateCollection();
                var cert = Certificate;
                if (cert != null) certificates.Add(cert);

                var sslStream = new SslStream(new NetworkStream(socket), false, OnCertificateValidationCallback);
                using var timeoutSource = new CancellationTokenSource(timeout);
                await sslStream.AuthenticateAsClientAsync(
                    new SslClientAuthenticationOptions
                    {
                        TargetHost = host,
                        ClientCertificates = certificates,
                        EnabledSslProtocols = protocol,
                        CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                    },
                    timeoutSource.Token).ConfigureAwait(false);

                _stream = sslStream;
            }
        }
        catch (Exception ex)
        {
            if (ex is SocketException) socket.Close();

            Client = null;
            if (!Disposed && !ex.IsDisposed()) OnError("Connect", ex);
            throw;
        }

        return true;
    }

    private Boolean OnCertificateValidationCallback(Object? sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
    {
        if (Certificate is not X509Certificate2 cert) return true;
        if (chain == null) return false;

        return chain.ChainElements.Cast<X509ChainElement>().Any(element => element.Certificate.Thumbprint == cert.Thumbprint);
    }

    /// <summary>关闭连接</summary>
    /// <param name="reason">关闭原因</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    protected override Task<Boolean> OnCloseAsync(String reason, CancellationToken cancellationToken)
    {
        var socket = Client;
        if (socket != null)
        {
            WriteLog("Close {0} {1}", reason, this);

            Active = false;
            try
            {
                var stream = _stream;
                if (stream != null)
                {
                    _stream = null;
                    try
                    {
                        stream.Close();
                        stream.Dispose();
                    }
                    catch { }
                }

                socket.Shutdown();
                socket.Close();

                if (_server != null) Dispose();
            }
            catch (Exception ex)
            {
                Client = null;
                if (!ex.IsDisposed()) OnError("Close", ex);
                return Task.FromResult(false);
            }

            Client = null;
        }

        return Task.FromResult(true);
    }
    #endregion

    #region 发送
    private Int32 _bufferSize;
    private SpinLock _spinLock = new();

    /// <summary>发送数据包</summary>
    /// <param name="packet">数据包</param>
    /// <returns>发送字节数</returns>
    protected override Int32 OnSend(IPacket packet)
    {
        var count = packet.Total;
        if (Log.Enable && LogSend) WriteLog("Send [{0}]: {1}", count, packet.ToHex(LogDataLength));

        using var span = Tracer?.NewSpan($"net:{Name}:Send", count + String.Empty, count);
        var socket = Client;
        if (socket == null) return -1;

        var result = count;
        var gotLock = false;
        try
        {
            if (_bufferSize == 0) _bufferSize = socket.SendBufferSize;
            if (_bufferSize < count) socket.SendBufferSize = _bufferSize = count;

            _spinLock.Enter(ref gotLock);

            if (_stream == null)
            {
                if (count == 0)
                    result = socket.Send(Pool.Empty);
                else if (packet.Next == null && packet.TryGetArray(out var segment))
                    result = socket.Send(segment.Array!, segment.Offset, segment.Count, SocketFlags.None);
                else if (packet.TryGetSpan(out var data))
                    result = socket.Send(data);
                else
                    result = socket.Send(packet.ToSegments());
            }
            else
            {
                if (count == 0)
                    _stream.Write([]);
                else
                    packet.CopyTo(_stream);
            }
        }
        catch (Exception ex)
        {
            span?.SetError(ex, packet);
            if (!ex.IsDisposed())
            {
                OnError("Send", ex);
                Close("SendError");
            }

            return -1;
        }
        finally
        {
            if (gotLock) _spinLock.Exit();
        }

        LastTime = DateTime.Now;
        return result;
    }

    /// <summary>发送数组段</summary>
    /// <param name="data">数组段</param>
    /// <returns>发送字节数</returns>
    protected override Int32 OnSend(ArraySegment<Byte> data)
    {
        var count = data.Count;
        if (Log.Enable && LogSend)
            WriteLog("Send [{0}]: {1}", count, data.Array?.ToHex(data.Offset, count > LogDataLength ? count : LogDataLength));

        using var span = Tracer?.NewSpan($"net:{Name}:Send", count + String.Empty, count);
        var socket = Client;
        if (socket == null) return -1;

        var result = count;
        var gotLock = false;
        try
        {
            if (_bufferSize == 0) _bufferSize = socket.SendBufferSize;
            if (_bufferSize < count) socket.SendBufferSize = _bufferSize = count;

            _spinLock.Enter(ref gotLock);

            if (_stream == null)
            {
                if (count == 0)
                    result = socket.Send(Pool.Empty);
                else
                    result = socket.Send(data.Array!, data.Offset, data.Count, SocketFlags.None);
            }
            else
            {
                if (count == 0)
                    _stream.Write([]);
                else
                    _stream.Write(data.Array!, data.Offset, data.Count);
            }
        }
        catch (Exception ex)
        {
            span?.SetError(ex, data.Array?.ToHex(data.Offset, data.Count));
            if (!ex.IsDisposed())
            {
                OnError("Send", ex);
                Close("SendError");
            }

            return -1;
        }
        finally
        {
            if (gotLock) _spinLock.Exit();
        }

        LastTime = DateTime.Now;
        return result;
    }

    /// <summary>发送只读内存段</summary>
    /// <param name="data">数据</param>
    /// <returns>发送字节数</returns>
    protected override Int32 OnSend(ReadOnlySpan<Byte> data)
    {
        var count = data.Length;
        if (Log.Enable && LogSend) WriteLog("Send [{0}]: {1}", count, data.ToHex(LogDataLength));

        using var span = Tracer?.NewSpan($"net:{Name}:Send", count + String.Empty, count);
        var socket = Client;
        if (socket == null) return -1;

        var result = count;
        var gotLock = false;
        try
        {
            if (_bufferSize == 0) _bufferSize = socket.SendBufferSize;
            if (_bufferSize < count) socket.SendBufferSize = _bufferSize = count;

            _spinLock.Enter(ref gotLock);

            if (_stream == null)
            {
                result = count == 0 ? socket.Send(Pool.Empty) : socket.Send(data);
            }
            else
            {
                if (count == 0)
                    _stream.Write([]);
                else
                    _stream.Write(data);
            }
        }
        catch (Exception ex)
        {
            span?.SetError(ex, data.ToHex());
            if (!ex.IsDisposed())
            {
                OnError("Send", ex);
                Close("SendError");
            }

            return -1;
        }
        finally
        {
            if (gotLock) _spinLock.Exit();
        }

        LastTime = DateTime.Now;
        return result;
    }
    #endregion

    #region 接收
    /// <summary>异步接收数据</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>数据包</returns>
    public override async Task<IOwnerPacket?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        if (!Open() || Client == null) return null;

        var stream = _stream;
        if (stream != null)
        {
            using var span = Tracer?.NewSpan($"net:{Name}:ReceiveAsync", BufferSize + String.Empty);
            try
            {
                var packet = new OwnerPacket(BufferSize);
                var size = await stream.ReadAsync(packet.Buffer, 0, packet.Length, cancellationToken).ConfigureAwait(false);
                span?.Value = size;
                return packet.Resize(size);
            }
            catch (Exception ex)
            {
                span?.SetError(ex, null);
                throw;
            }
        }

        return await base.ReceiveAsync(cancellationToken).ConfigureAwait(false);
    }

    internal override Boolean OnReceiveAsync(SocketAsyncEventArgs socketEventArgs)
    {
        var socket = Client;
        if (socket == null || !Active || Disposed) throw new ObjectDisposedException(GetType().Name);

        var stream = _stream;
        if (stream != null)
        {
            stream.BeginRead(socketEventArgs.Buffer!, socketEventArgs.Offset, socketEventArgs.Count, OnEndRead, socketEventArgs);
            return true;
        }

        return socket.ReceiveAsync(socketEventArgs);
    }

    private void OnEndRead(IAsyncResult asyncResult)
    {
        Int32 bytes;
        try
        {
            bytes = _stream!.EndRead(asyncResult);
        }
        catch (Exception ex)
        {
            if (ex is IOException)
            {
            }
            else if (ex is SocketException socketException && socketException.SocketErrorCode == SocketError.ConnectionReset)
            {
            }
            else
            {
                XTrace.WriteException(ex);
            }

            return;
        }

        if (asyncResult.AsyncState is SocketAsyncEventArgs socketEventArgs) ProcessEvent(socketEventArgs, bytes, 1);
    }

    /// <summary>预处理收到的数据包</summary>
    /// <param name="packet">数据包</param>
    /// <param name="local">本地地址</param>
    /// <param name="remote">远程地址</param>
    /// <returns>处理会话</returns>
    protected internal override ISocketSession? OnPreReceive(IPacket packet, IPAddress local, IPEndPoint remote)
    {
        if (packet.Length == 0)
        {
            using var span = Tracer?.NewSpan($"net:{Name}:EmptyData", remote?.ToString());
            var reason = CheckClosed();
            if (reason != null)
            {
                Close(reason);
                Dispose();
                return null;
            }
        }

        return this;
    }

    /// <summary>处理收到的数据</summary>
    /// <param name="eventArgs">接收事件参数</param>
    /// <returns>是否成功</returns>
    protected override Boolean OnReceive(ReceivedEventArgs eventArgs)
    {
        RaiseReceive(this, eventArgs);
        return true;
    }
    #endregion

    #region 辅助
    /// <summary>日志前缀</summary>
    public override String? LogPrefix
    {
        get
        {
            var prefix = base.LogPrefix;
            if (prefix == null && _server != null)
                prefix = base.LogPrefix = $"{_server.Name}[{ID}].";

            return prefix;
        }
        set => base.LogPrefix = value;
    }

    /// <summary>返回字符串表示</summary>
    /// <returns>连接信息</returns>
    public override String ToString()
    {
        var local = Local;
        var remote = Remote.EndPoint;
        if (remote == null || remote.IsAny()) return local.ToString();

        return _server == null ? $"{local}=>{remote}" : $"{local}<={remote}";
    }
    #endregion
}