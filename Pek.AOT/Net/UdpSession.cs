using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

using Pek.Data;
using Pek.Log;
using Pek.Model;

namespace Pek.Net;

/// <summary>UDP会话</summary>
public class UdpSession : DisposeBase, ISocketSession, ITransport, ILogFeature
{
    /// <summary>会话编号</summary>
    public Int32 ID { get; set; }

    /// <summary>名称</summary>
    public String Name { get; set; }

    /// <summary>所属服务器</summary>
    public UdpServer Server { get; set; }

    Socket? ISocket.Client => Server?.Client;

    /// <summary>本地地址</summary>
    public NetUri Local { get; set; }

    /// <summary>端口</summary>
    public Int32 Port { get => Local.Port; set => Local.Port = value; }

    /// <summary>远程地址</summary>
    public NetUri Remote { get; set; }

    private Int32 _timeout;

    /// <summary>超时时间</summary>
    public Int32 Timeout
    {
        get => _timeout;
        set
        {
            _timeout = value;
            if (Server?.Client != null)
                Server.Client.ReceiveTimeout = _timeout;
        }
    }

    /// <summary>消息管道</summary>
    public IPipeline? Pipeline { get; set; }

    ISocketServer ISocketSession.Server => Server;

    /// <summary>最后一次通信时间</summary>
    public DateTime LastTime { get; private set; } = DateTime.Now;

    /// <summary>链路追踪器</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>实例化UDP会话</summary>
    /// <param name="server">所属服务器</param>
    /// <param name="local">本地地址</param>
    /// <param name="remote">远程终结点</param>
    public UdpSession(UdpServer server, IPAddress? local, IPEndPoint remote)
    {
        Name = server.Name;

        Server = server;
        Remote = new NetUri(NetType.Udp, remote);
        Tracer = server.Tracer;

        Local = server.Local.Clone();
        if (local != null) Local.Address = local;

        server.Client?.CheckBroadcast(remote.Address);
    }

    /// <summary>开始数据交换</summary>
    public void Start()
    {
        if (Disposed || Server == null) return;

        Pipeline = Server.Pipeline;

        Server.Open();
        WriteLog("New {0}", Remote.EndPoint);

        if (Pipeline != null)
        {
            var context = Server.CreateContext(this);
            Pipeline.Open(context);
            Server.ReturnContext(context);
        }
    }

    private void Stop(String reason)
    {
        if (Server == null) return;

        WriteLog("Close {0} {1}", Remote.EndPoint, reason);

        if (Pipeline != null)
        {
            var context = Server.CreateContext(this);
            Pipeline.Close(context, reason);
            Server.ReturnContext(context);
        }

        Server = null!;
    }

    /// <summary>释放资源</summary>
    /// <param name="disposing">是否显式释放</param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        Stop(disposing ? "Dispose" : "GC");
    }

    /// <summary>发送数据包</summary>
    /// <param name="data">数据包</param>
    /// <returns>已发送字节数</returns>
    public Int32 Send(IPacket data)
    {
        if (Disposed) throw new ObjectDisposedException(GetType().Name);

        return Server.OnSend(data, Remote.EndPoint);
    }

    /// <summary>发送字节数组</summary>
    /// <param name="data">字节数组</param>
    /// <param name="offset">偏移</param>
    /// <param name="count">长度</param>
    /// <returns>已发送字节数</returns>
    public Int32 Send(Byte[] data, Int32 offset = 0, Int32 count = -1)
    {
        if (Disposed) throw new ObjectDisposedException(GetType().Name);

        if (count < 0) count = data.Length - offset;

#if NET6_0_OR_GREATER
        return Server.OnSend(new ReadOnlySpan<Byte>(data, offset, count), Remote.EndPoint);
#else
        return Server.OnSend(new ArraySegment<Byte>(data, offset, count), Remote.EndPoint);
#endif
    }

    /// <summary>发送数组段</summary>
    /// <param name="data">数组段</param>
    /// <returns>已发送字节数</returns>
    public Int32 Send(ArraySegment<Byte> data)
    {
        if (Disposed) throw new ObjectDisposedException(GetType().Name);

        return Server.OnSend(data, Remote.EndPoint);
    }

    /// <summary>发送只读数据</summary>
    /// <param name="data">数据</param>
    /// <returns>已发送字节数</returns>
    public Int32 Send(ReadOnlySpan<Byte> data)
    {
        if (Disposed) throw new ObjectDisposedException(GetType().Name);

        return Server.OnSend(data, Remote.EndPoint);
    }

    /// <summary>发送消息</summary>
    /// <param name="message">消息</param>
    /// <returns>已发送字节数</returns>
    public virtual Int32 SendMessage(Object message)
    {
        if (Pipeline == null) throw new InvalidOperationException(nameof(Pipeline));

        using var span = Tracer?.NewSpan($"net:{Name}:SendMessage", message);
        var context = Server.CreateContext(this);
        try
        {
            return (Int32)(Pipeline.Write(context, message) ?? -1);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, message);
            throw;
        }
        finally
        {
            Server.ReturnContext(context);
        }
    }

    /// <summary>发送消息并等待响应</summary>
    /// <param name="message">消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应消息</returns>
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
    public virtual ValueTask<Object> SendMessageAsync(Object message, CancellationToken cancellationToken = default)
    {
        if (Server == null) throw new InvalidOperationException(nameof(Server));
        if (Pipeline == null) throw new InvalidOperationException(nameof(Pipeline));

        var span = Tracer?.NewSpan($"net:{Name}:SendMessageAsync", message);
        var context = Server.CreateContext(this);
        CancellationTokenRegistration cancellationRegistration = default;
        try
        {
            var source = new TaskCompletionSource<Object>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (cancellationToken.CanBeCanceled)
            {
                cancellationRegistration = cancellationToken.Register(static state =>
                {
                    if (state is TaskCompletionSource<Object> taskSource && !taskSource.Task.IsCompleted)
                        taskSource.TrySetCanceled();
                }, source);
            }

            if (span != null)
            {
                _ = source.Task.ContinueWith(task =>
                {
                    cancellationRegistration.Dispose();

                    if (task.IsCanceled)
                        span.AppendTag("Canceled");
                    else if (task.IsFaulted && task.Exception != null)
                        span.SetError(task.Exception.InnerException ?? task.Exception, null);

                    span.Dispose();
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
            else if (cancellationToken.CanBeCanceled)
            {
                _ = source.Task.ContinueWith(_ => cancellationRegistration.Dispose(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }

            context["TaskSource"] = source;
            context["Span"] = span;

            var result = (Int32)(Pipeline.Write(context, message) ?? -1);
            Server.ReturnContext(context);
            context = null!;

            if (result < 0)
            {
                source.TrySetResult(null!);
                return new ValueTask<Object>(source.Task);
            }

            return new ValueTask<Object>(source.Task);
        }
        catch (Exception ex)
        {
            if (ex is TaskCanceledException)
                span?.AppendTag(ex.Message);
            else
                span?.SetError(ex, message);

            cancellationRegistration.Dispose();
            span?.Dispose();
            if (context != null) Server.ReturnContext(context);
            throw;
        }
    }
#else
    public virtual Task<Object> SendMessageAsync(Object message, CancellationToken cancellationToken = default)
    {
        if (Server == null) throw new InvalidOperationException(nameof(Server));
        if (Pipeline == null) throw new InvalidOperationException(nameof(Pipeline));

        var span = Tracer?.NewSpan($"net:{Name}:SendMessageAsync", message);
        var context = Server.CreateContext(this);
        CancellationTokenRegistration cancellationRegistration = default;
        try
        {
            var source = new TaskCompletionSource<Object>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (cancellationToken.CanBeCanceled)
            {
                cancellationRegistration = cancellationToken.Register(static state =>
                {
                    if (state is TaskCompletionSource<Object> taskSource && !taskSource.Task.IsCompleted)
                        taskSource.TrySetCanceled();
                }, source);
            }

            if (span != null)
            {
                _ = source.Task.ContinueWith(task =>
                {
                    cancellationRegistration.Dispose();

                    if (task.IsCanceled)
                        span.AppendTag("Canceled");
                    else if (task.IsFaulted && task.Exception != null)
                        span.SetError(task.Exception.InnerException ?? task.Exception, null);

                    span.Dispose();
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
            else if (cancellationToken.CanBeCanceled)
            {
                _ = source.Task.ContinueWith(_ => cancellationRegistration.Dispose(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }

            context["TaskSource"] = source;
            context["Span"] = span;

            var result = (Int32)(Pipeline.Write(context, message) ?? -1);
            Server.ReturnContext(context);
            context = null!;

            if (result < 0)
            {
                source.TrySetResult(null!);
                return source.Task;
            }

            return source.Task;
        }
        catch (Exception ex)
        {
            if (ex is TaskCanceledException)
                span?.AppendTag(ex.Message);
            else
                span?.SetError(ex, message);

            cancellationRegistration.Dispose();
            span?.Dispose();
            if (context != null) Server.ReturnContext(context);
            throw;
        }
    }
#endif

    /// <summary>同步接收数据</summary>
    /// <returns>数据包</returns>
    public IOwnerPacket Receive()
    {
        if (Disposed) throw new ObjectDisposedException(GetType().Name);
        if (Server?.Client == null) throw new InvalidOperationException(nameof(Server));

        using var span = Tracer?.NewSpan($"net:{Name}:Receive");
        try
        {
            EndPoint endPoint = Remote.EndPoint;
            var packet = new OwnerPacket(Server.BufferSize);
            var size = Server.Client.ReceiveFrom(packet.Buffer, ref endPoint);
            span?.Value = size;

            return packet.Resize(size);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }

    /// <summary>异步接收数据</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>数据包</returns>
    public virtual async Task<IOwnerPacket?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        if (Disposed) throw new ObjectDisposedException(GetType().Name);
        if (Server?.Client == null) throw new InvalidOperationException(nameof(Server));

        using var span = Tracer?.NewSpan($"net:{Name}:Receive");
        try
        {
            EndPoint endPoint = Remote.EndPoint;
            var packet = new OwnerPacket(Server.BufferSize);
            var socket = Server.Client;
#if NETFRAMEWORK || NETSTANDARD2_0
            var asyncResult = socket.BeginReceiveFrom(packet.Buffer, 0, packet.Length, SocketFlags.None, ref endPoint, null, socket);
            var size = asyncResult.IsCompleted ?
                socket.EndReceive(asyncResult) :
                await Task.Factory.FromAsync(asyncResult, item => socket.EndReceiveFrom(item, ref endPoint)).ConfigureAwait(false);
#elif NET7_0_OR_GREATER
            var result = await socket.ReceiveFromAsync(packet.GetMemory(), endPoint, cancellationToken).ConfigureAwait(false);
            var size = result.ReceivedBytes;
#else
            var result = await socket.ReceiveFromAsync(packet.Buffer, SocketFlags.None, endPoint).ConfigureAwait(false);
            var size = result.ReceivedBytes;
#endif
            span?.Value = size;

            return packet.Resize(size);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }

    /// <summary>接收事件</summary>
    public event EventHandler<ReceivedEventArgs>? Received;

    internal void OnReceive(ReceivedEventArgs e)
    {
        LastTime = DateTime.Now;

        if (e != null) Received?.Invoke(this, e);

        if (e != null && (e.Packet == null || e.Packet.Length == 0))
        {
            Stop("Finish");
            Dispose();
        }
    }

    void ISocketRemote.Process(IData data) => (Server as ISocketRemote)?.Process(data);

    /// <summary>错误事件</summary>
    public event EventHandler<ExceptionEventArgs>? Error;

    /// <summary>触发错误</summary>
    /// <param name="action">动作</param>
    /// <param name="exception">异常</param>
    protected virtual void OnError(String action, Exception exception)
    {
        Log?.Error(LogPrefix + "{0}Error {1} {2}", action, this, exception.Message);
        Error?.Invoke(this, new ExceptionEventArgs(action, exception));
    }

    /// <summary>返回字符串表示</summary>
    /// <returns>会话标识</returns>
    public override String ToString()
    {
        if (Remote != null && !Remote.EndPoint.IsAny())
            return $"{Local}<={Remote.EndPoint}";

        return Local.ToString();
    }

    Boolean ITransport.Open() => true;

    Boolean ITransport.Close() => true;

    Boolean ITransport.Send(IPacket data) => Send(data) >= 0;

    async Task<IPacket?> ITransport.SendAsync(IPacket? data)
    {
        if (data != null && Send(data) < 0) return null;

        return await ReceiveAsync().ConfigureAwait(false);
    }

    IPacket? ITransport.Receive() => Receive();

    private ConcurrentDictionary<String, Object?>? _items;

    /// <summary>扩展数据项</summary>
    public IDictionary<String, Object?> Items => _items ??= new();

    /// <summary>获取或设置扩展数据</summary>
    /// <param name="key">键</param>
    /// <returns>值</returns>
    public Object? this[String key] { get => _items != null && _items.TryGetValue(key, out var obj) ? obj : null; set => Items[key] = value; }

    /// <summary>日志对象</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>是否输出发送日志</summary>
    public Boolean LogSend { get; set; }

    /// <summary>是否输出接收日志</summary>
    public Boolean LogReceive { get; set; }

    private String? _logPrefix;

    /// <summary>日志前缀</summary>
    public virtual String LogPrefix
    {
        get
        {
            if (_logPrefix == null)
            {
                var name = Server == null ? String.Empty : Server.Name;
                _logPrefix = $"{name}[{ID}].";
            }

            return _logPrefix;
        }
        set => _logPrefix = value;
    }

    /// <summary>写日志</summary>
    /// <param name="format">格式</param>
    /// <param name="args">参数</param>
    public void WriteLog(String format, params Object?[] args) => Log.Info(LogPrefix + format, args);
}