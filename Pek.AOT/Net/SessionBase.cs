using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

using Pek;
using Pek.Data;
using Pek.Extension;
using Pek.Log;
using Pek.Model;

namespace Pek.Net;

/// <summary>会话基类</summary>
public abstract class SessionBase : DisposeBase, ISocketClient, ITransport, ILogFeature
{
    private TaskCompletionSource<IPacket?>? _responseSource;
    private Int32 _recvCount;
    private ConcurrentDictionary<String, Object?>? _items;

    /// <summary>会话标识</summary>
    public Int32 ID { get; internal set; }

    /// <summary>名称</summary>
    public String Name { get; set; }

    /// <summary>本地绑定信息</summary>
    public NetUri Local { get; set; } = new();

    /// <summary>端口</summary>
    public Int32 Port { get => Local.Port; set => Local.Port = value; }

    /// <summary>远程结点地址</summary>
    public NetUri Remote { get; set; } = new();

    /// <summary>超时时间（毫秒）</summary>
    public Int32 Timeout { get; set; } = 3_000;

    private volatile Boolean _active;

    /// <summary>是否活动</summary>
    public Boolean Active { get => _active; set => _active = value; }

    /// <summary>底层 Socket</summary>
    public Socket? Client { get; protected set; }

    /// <summary>最后一次通信时间</summary>
    public DateTime LastTime { get; internal protected set; } = DateTime.Now;

    /// <summary>最大并行接收数</summary>
    public Int32 MaxAsync { get; set; } = 1;

    /// <summary>缓冲区大小</summary>
    public Int32 BufferSize { get; set; }

    /// <summary>连接关闭原因</summary>
    public String? CloseReason { get; set; }

    /// <summary>链路追踪器</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>消息管道</summary>
    public IPipeline? Pipeline { get; set; }

    /// <summary>数据项</summary>
    public IDictionary<String, Object?> Items => _items ??= new();

    /// <summary>设置或获取扩展数据</summary>
    /// <param name="key">键</param>
    /// <returns>值</returns>
    public Object? this[String key] { get => _items != null && _items.TryGetValue(key, out var obj) ? obj : null; set => Items[key] = value; }

    /// <summary>日志前缀</summary>
    public virtual String? LogPrefix { get; set; }

    /// <summary>日志对象</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>是否输出发送日志</summary>
    public Boolean LogSend { get; set; }

    /// <summary>是否输出接收日志</summary>
    public Boolean LogReceive { get; set; }

    /// <summary>收发日志数据体长度</summary>
    public Int32 LogDataLength { get; set; } = 64;

    /// <summary>实例化会话基类</summary>
    protected SessionBase()
    {
        Name = GetType().Name;
        BufferSize = SocketSetting.Current.BufferSize;
        LogDataLength = SocketSetting.Current.LogDataLength;
    }

    /// <summary>释放资源</summary>
    /// <param name="disposing">是否显式释放</param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        var reason = GetType().Name + (disposing ? "Dispose" : "GC");
        try
        {
            Close(reason);
        }
        catch (Exception ex)
        {
            OnError("Dispose", ex);
        }
    }

    /// <summary>返回文本表示</summary>
    /// <returns>本地地址</returns>
    public override String ToString() => Local + String.Empty;

    /// <summary>打开连接</summary>
    /// <returns>是否成功</returns>
    public virtual Boolean Open()
    {
        if (Active) return true;
        if (!Monitor.TryEnter(this, Timeout + 100)) return false;

        try
        {
            using var cancellationTokenSource = new CancellationTokenSource(Timeout);
            return OpenAsync(cancellationTokenSource.Token).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        finally
        {
            Monitor.Exit(this);
        }
    }

    /// <summary>异步打开连接</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    public virtual async Task<Boolean> OpenAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (Active) return true;
        if (cancellationToken.IsCancellationRequested) return false;

        using var span = Tracer?.NewSpan($"net:{Name}:Open", Remote?.ToString());
        try
        {
            _recvCount = 0;

            var result = await OnOpenAsync(cancellationToken).ConfigureAwait(false);
            if (!result) return false;

            var timeout = Timeout;
            if (timeout > 0 && Client != null)
            {
                Client.SendTimeout = timeout;
                Client.ReceiveTimeout = timeout;
            }

            Active = true;

            if (Pipeline is Pipeline pipe && pipe.Handlers.Count > 0)
            {
                WriteLog("初始化管道：");
                foreach (var handler in pipe.Handlers)
                {
                    WriteLog("    {0}", handler);
                }
            }

            if (Pipeline != null)
            {
                var context = CreateContext(this);
                Pipeline.Open(context);
                ReturnContext(context);
            }

            Opened?.Invoke(this, EventArgs.Empty);
            ReceiveAsync();

            return true;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }

    /// <summary>执行具体打开动作</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    [MemberNotNullWhen(true, nameof(Client))]
    protected abstract Task<Boolean> OnOpenAsync(CancellationToken cancellationToken);

    /// <summary>关闭连接</summary>
    /// <param name="reason">关闭原因</param>
    /// <returns>是否成功</returns>
    public virtual Boolean Close(String reason)
    {
        if (!Active) return true;
        if (!Monitor.TryEnter(this, Timeout + 100)) return false;

        try
        {
            using var cancellationTokenSource = new CancellationTokenSource(Timeout);
            return CloseAsync(reason, cancellationTokenSource.Token).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        finally
        {
            Monitor.Exit(this);
        }
    }

    /// <summary>异步关闭连接</summary>
    /// <param name="reason">关闭原因</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    public virtual async Task<Boolean> CloseAsync(String reason, CancellationToken cancellationToken = default)
    {
        if (!Active) return true;
        if (cancellationToken.IsCancellationRequested) return false;

        using var span = Tracer?.NewSpan($"net:{Name}:Close", Remote?.ToString());
        try
        {
            CloseReason = reason;

            if (Pipeline != null)
            {
                var context = CreateContext(this);
                Pipeline.Close(context, reason);
                ReturnContext(context);
            }

            var result = await OnCloseAsync(reason ?? (GetType().Name + "Close"), cancellationToken).ConfigureAwait(false);
            _recvCount = 0;
            Active = !result;
            Closed?.Invoke(this, EventArgs.Empty);

            return result;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }

    /// <summary>执行具体关闭动作</summary>
    /// <param name="reason">关闭原因</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    protected abstract Task<Boolean> OnCloseAsync(String reason, CancellationToken cancellationToken);

    Boolean ITransport.Close() => Close("TransportClose");

    /// <summary>检查连接是否已关闭</summary>
    /// <returns>关闭原因</returns>
    protected String? CheckClosed()
    {
        var socket = Client;
        if (socket == null || !socket.Connected) return "Disconnected";

        try
        {
            if (socket.Poll(10, SelectMode.SelectRead))
            {
                Span<Byte> buffer = stackalloc Byte[1];
                try
                {
                    if (socket.Receive(buffer, SocketFlags.Peek) == 0) return "Finish";
                }
                catch (SocketException ex)
                {
                    return ex.SocketErrorCode.ToString();
                }
            }
        }
        catch (SocketException ex)
        {
            return ex.SocketErrorCode.ToString();
        }
        catch
        {
        }

        return null;
    }

    /// <summary>打开后触发</summary>
    public event EventHandler? Opened;

    /// <summary>关闭后触发</summary>
    public event EventHandler? Closed;

    /// <summary>发送数据包</summary>
    /// <param name="data">数据包</param>
    /// <returns>发送字节数</returns>
    public Int32 Send(IPacket data)
    {
        ThrowIfDisposed();
        if (!Open()) return -1;

        return OnSend(data);
    }

    /// <summary>执行具体发送动作</summary>
    /// <param name="data">数据包</param>
    /// <returns>发送字节数</returns>
    protected abstract Int32 OnSend(IPacket data);

    /// <summary>发送字节数组</summary>
    /// <param name="data">字节数组</param>
    /// <param name="offset">偏移</param>
    /// <param name="count">字节数</param>
    /// <returns>发送字节数</returns>
    public Int32 Send(Byte[] data, Int32 offset = 0, Int32 count = -1)
    {
        ThrowIfDisposed();
        if (!Open()) return -1;

        if (count < 0) count = data.Length - offset;
        return OnSend(new ReadOnlySpan<Byte>(data, offset, count));
    }

    /// <summary>发送数组段</summary>
    /// <param name="data">数组段</param>
    /// <returns>发送字节数</returns>
    public Int32 Send(ArraySegment<Byte> data)
    {
        ThrowIfDisposed();
        if (!Open()) return -1;

        return OnSend(data);
    }

    /// <summary>执行具体发送动作</summary>
    /// <param name="data">数组段</param>
    /// <returns>发送字节数</returns>
    protected abstract Int32 OnSend(ArraySegment<Byte> data);

    /// <summary>发送只读数据块</summary>
    /// <param name="data">数据</param>
    /// <returns>发送字节数</returns>
    public Int32 Send(ReadOnlySpan<Byte> data)
    {
        ThrowIfDisposed();
        if (!Open()) return -1;

        return OnSend(data);
    }

    /// <summary>执行具体发送动作</summary>
    /// <param name="data">数据</param>
    /// <returns>发送字节数</returns>
    protected abstract Int32 OnSend(ReadOnlySpan<Byte> data);

    Boolean ITransport.Send(IPacket data) => Send(data) >= 0;

    /// <summary>异步发送并等待下一帧原始响应</summary>
    /// <param name="data">请求数据</param>
    /// <returns>响应数据包</returns>
    public virtual Task<IPacket?> SendAsync(IPacket? data)
    {
        ThrowIfDisposed();
        if (!Open()) return Task.FromResult<IPacket?>(null);
        if (_responseSource != null) throw new InvalidOperationException("SessionBase 目前不支持并发等待多个响应。");

        var source = _responseSource = new TaskCompletionSource<IPacket?>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (data != null)
        {
            var count = Send(data);
            if (count < 0)
            {
                _responseSource = null;
                return Task.FromResult<IPacket?>(null);
            }
        }

        return WaitResponseAsync(source);
    }

    /// <summary>同步接收数据包</summary>
    /// <returns>数据包</returns>
    public virtual IOwnerPacket? Receive()
    {
        ThrowIfDisposed();
        if (!Open() || Client == null) return null;

        using var span = Tracer?.NewSpan($"net:{Name}:Receive");
        try
        {
            var packet = new OwnerPacket(BufferSize);
            var size = Client.Receive(packet.Buffer, SocketFlags.None);
            span?.Value = size;
            return packet.Resize(size);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }

    IPacket? ITransport.Receive() => Receive();

    /// <summary>异步接收数据包</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>数据包</returns>
    public virtual async Task<IOwnerPacket?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (!Open() || Client == null) return null;

        using var span = Tracer?.NewSpan($"net:{Name}:ReceiveAsync", BufferSize + String.Empty);
        try
        {
            var packet = new OwnerPacket(BufferSize);
            var size = await Client.ReceiveAsync(packet.GetMemory(), SocketFlags.None, cancellationToken).ConfigureAwait(false);
            span?.Value = size;
            return packet.Resize(size);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }

    /// <summary>开始异步接收</summary>
    /// <returns>是否成功</returns>
    public virtual Boolean ReceiveAsync()
    {
        ThrowIfDisposed();
        if (!Open()) return false;

        var count = _recvCount;
        var max = MaxAsync;
        if (count >= max) return false;

        for (var i = count; i < max; i++)
        {
            count = Interlocked.Increment(ref _recvCount);
            if (count > max)
            {
                Interlocked.Decrement(ref _recvCount);
                return false;
            }

            var buffer = new Byte[BufferSize];
            var socketEventArgs = new SocketAsyncEventArgs();
            socketEventArgs.SetBuffer(buffer, 0, buffer.Length);
            socketEventArgs.Completed += (_, e) => ProcessEvent(e, -1, _intoThreadCount);
            socketEventArgs.UserToken = count;

            if (Log.Level <= LogLevel.Debug) WriteLog("创建RecvSA {0}", count);
            StartReceive(socketEventArgs, 0);
        }

        return true;
    }

    /// <summary>处理收到的数据。默认匹配同步接收委托</summary>
    /// <param name="eventArgs">接收事件参数</param>
    /// <returns>是否已处理</returns>
    protected abstract Boolean OnReceive(ReceivedEventArgs eventArgs);

    /// <summary>预处理收到的数据包</summary>
    /// <param name="packet">数据包</param>
    /// <param name="local">本地地址</param>
    /// <param name="remote">远程地址</param>
    /// <returns>处理该数据包的会话</returns>
    protected internal abstract ISocketSession? OnPreReceive(IPacket packet, IPAddress local, IPEndPoint remote);

    /// <summary>执行具体异步接收动作</summary>
    /// <param name="socketEventArgs">事件参数</param>
    /// <returns>是否异步完成</returns>
    internal abstract Boolean OnReceiveAsync(SocketAsyncEventArgs socketEventArgs);

    /// <summary>数据到达事件</summary>
    public event EventHandler<ReceivedEventArgs>? Received;

    /// <summary>触发数据到达事件</summary>
    /// <param name="sender">发送者</param>
    /// <param name="eventArgs">事件参数</param>
    protected virtual void RaiseReceive(Object sender, ReceivedEventArgs eventArgs) => Received?.Invoke(sender, eventArgs);

    private static readonly Int32 _intoThreadCount = 10;

    private Boolean StartReceive(SocketAsyncEventArgs socketEventArgs, Int32 ioThread)
    {
        if (Disposed)
        {
            ReleaseRecv(socketEventArgs, "Disposed " + socketEventArgs.SocketError);
            throw new ObjectDisposedException(GetType().Name);
        }

        Boolean result;
        try
        {
            result = OnReceiveAsync(socketEventArgs);
        }
        catch (Exception ex)
        {
            ReleaseRecv(socketEventArgs, "ReceiveAsyncError " + ex.Message);
            if (!ex.IsDisposed()) OnError("ReceiveAsync", ex);
            return false;
        }

        if (!result && socketEventArgs.BytesTransferred == 0 && socketEventArgs.SocketError == SocketError.Success)
        {
            var reason = CheckClosed() ?? "EmptyData";
            Close(reason);
            Dispose();
            return false;
        }

        if (!result)
        {
            if (ioThread-- > 0)
            {
                ProcessEvent(socketEventArgs, -1, ioThread);
            }
            else
            {
                ThreadPool.UnsafeQueueUserWorkItem(static state =>
                {
                    try
                    {
                        if (state is Tuple<SessionBase, SocketAsyncEventArgs> tuple)
                            tuple.Item1.ProcessEvent(tuple.Item2, -1, _intoThreadCount);
                    }
                    catch (Exception ex)
                    {
                        XTrace.WriteException(ex);
                    }
                }, Tuple.Create(this, socketEventArgs));
            }
        }

        return true;
    }

    /// <summary>同步或异步收到数据</summary>
    /// <param name="socketEventArgs">事件参数</param>
    /// <param name="bytes">字节数</param>
    /// <param name="ioThread">是否在 IO 线程中</param>
    protected internal void ProcessEvent(SocketAsyncEventArgs socketEventArgs, Int32 bytes, Int32 ioThread)
    {
        try
        {
            if (!Active)
            {
                ReleaseRecv(socketEventArgs, "!Active " + socketEventArgs.SocketError);
                return;
            }

            if (socketEventArgs.SocketError != SocketError.Success)
            {
                if (OnReceiveError(socketEventArgs))
                {
                    var ex = socketEventArgs.GetException();
                    if (ex != null) OnError("ReceiveAsync", ex);

                    ReleaseRecv(socketEventArgs, "SocketError " + socketEventArgs.SocketError);
                    return;
                }
            }
            else
            {
                var remote = socketEventArgs.RemoteEndPoint as IPEndPoint ?? Remote.EndPoint;
                if (bytes < 0) bytes = socketEventArgs.BytesTransferred;
                if (socketEventArgs.Buffer != null)
                {
                    var packet = new ArrayPacket(socketEventArgs.Buffer, socketEventArgs.Offset, bytes);
                    ProcessReceive(packet, socketEventArgs, remote);
                }
            }

            if (Active && !Disposed)
                StartReceive(socketEventArgs, ioThread);
            else
                ReleaseRecv(socketEventArgs, "!Active || Disposed");
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);

            try
            {
                ReleaseRecv(socketEventArgs, "ProcessEventError " + ex.Message);
                Close("ProcessEventError");
            }
            catch
            {
            }

            Dispose();
        }
    }

    private void ProcessReceive(IPacket packet, SocketAsyncEventArgs socketEventArgs, IPEndPoint remote)
    {
        DefaultSpan.Current = null;

        var total = packet.Length;
        var local = socketEventArgs.ReceiveMessageFromPacketInfo.Address;
        using var span = Tracer?.NewSpan($"net:{Name}:ProcessReceive", new { total, local, remote }, total);
        try
        {
            LastTime = DateTime.Now;

            var session = OnPreReceive(packet, local, remote);
            if (session == null) return;

            if (LogReceive && Log.Enable) WriteLog("Recv [{0}]: {1}", total, packet.ToHex(LogDataLength));
            if (TrySetResponse(packet)) return;

            if (Local.IsTcp) remote = Remote.EndPoint;

            var eventArgs = ReceivedEventArgs.Rent();
            eventArgs.Packet = packet;
            eventArgs.Local = local;
            eventArgs.Remote = remote;

            var pipeline = Pipeline;
            if (pipeline == null)
            {
                OnReceive(eventArgs);
            }
            else
            {
                var context = CreateContext(session);
                context.Data = eventArgs;
                context.EventArgs = socketEventArgs;
                pipeline.Read(context, packet);
                ReturnContext(context);
            }

            ReceivedEventArgs.Return(eventArgs);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, packet.ToHex());
            if (!ex.IsDisposed()) OnError("OnReceive", ex);
        }
    }

    /// <summary>收到异常时如何处理</summary>
    /// <param name="socketEventArgs">事件参数</param>
    /// <returns>是否按异常处理</returns>
    internal virtual Boolean OnReceiveError(SocketAsyncEventArgs socketEventArgs)
    {
        if (socketEventArgs.SocketError == SocketError.ConnectionReset) Close("ConnectionReset");
        return true;
    }

    /// <summary>创建管道上下文</summary>
    /// <param name="session">远程会话</param>
    /// <returns>上下文</returns>
    protected internal virtual NetHandlerContext CreateContext(ISocketRemote session)
    {
        var context = NetHandlerContext.Rent();
        context.Pipeline = Pipeline;
        context.Session = session;
        context.Owner = session;
        return context;
    }

    /// <summary>归还管道上下文</summary>
    /// <param name="context">上下文</param>
    protected internal virtual void ReturnContext(IHandlerContext? context)
    {
        if (context is NetHandlerContext netHandlerContext)
            NetHandlerContext.Return(netHandlerContext);
    }

    /// <summary>通过管道发送消息并复用外部上下文</summary>
    /// <param name="message">消息</param>
    /// <param name="context">上下文</param>
    /// <returns>发送字节数</returns>
    public virtual Int32 SendMessage(Object message, IHandlerContext? context)
    {
        if (context == null) return SendMessage(message);
        if (Pipeline == null) throw new ArgumentNullException(nameof(Pipeline), "No pipes are set");

        using var span = Tracer?.NewSpan($"net:{Name}:SendMessage", message);
        try
        {
            if (span != null && message is ITraceMessage traceMessage && traceMessage.TraceId.IsNullOrEmpty()) traceMessage.TraceId = span.ToString();
            context.Pipeline ??= Pipeline;
            return (Int32)(Pipeline.Write(context, message) ?? 0);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, message);
            throw;
        }
    }

    /// <summary>通过管道发送消息</summary>
    /// <param name="message">消息</param>
    /// <returns>发送字节数</returns>
    public virtual Int32 SendMessage(Object message)
    {
        if (Pipeline == null) throw new ArgumentNullException(nameof(Pipeline), "No pipes are set");

        using var span = Tracer?.NewSpan($"net:{Name}:SendMessage", message);
        var context = CreateContext(this);
        try
        {
            if (span != null && message is ITraceMessage traceMessage && traceMessage.TraceId.IsNullOrEmpty()) traceMessage.TraceId = span.ToString();
            return (Int32)(Pipeline.Write(context, message) ?? 0);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, message);
            throw;
        }
        finally
        {
            ReturnContext(context);
        }
    }

    /// <summary>通过管道发送消息并等待响应</summary>
    /// <param name="message">消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应消息</returns>
    public virtual ValueTask<Object> SendMessageAsync(Object message, CancellationToken cancellationToken = default)
    {
        if (Pipeline == null) throw new ArgumentNullException(nameof(Pipeline), "No pipes are set");

        var span = Tracer?.NewSpan($"net:{Name}:SendMessageAsync", message);
        var context = CreateContext(this);
        try
        {
            if (span != null && message is ITraceMessage traceMessage && traceMessage.TraceId.IsNullOrEmpty()) traceMessage.TraceId = span.ToString();

            var source = new TaskCompletionSource<Object>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (span != null)
            {
                _ = source.Task.ContinueWith(task =>
                {
                    if (task.IsCanceled)
                        span.AppendTag("Canceled");
                    else if (task.IsFaulted && task.Exception != null)
                        span.SetError(task.Exception.InnerException ?? task.Exception, null);

                    span.Dispose();
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }

            context["TaskSource"] = source;
            context["Span"] = span;

            var result = (Int32)(Pipeline.Write(context, message) ?? 0);
            ReturnContext(context);
            context = null;

            if (result < 0)
            {
                source.TrySetResult(null!);
                return new ValueTask<Object>(source.Task);
            }

            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(static state =>
                {
                    if (state is TaskCompletionSource<Object> taskSource && !taskSource.Task.IsCompleted)
                        taskSource.TrySetCanceled();
                }, source);
            }

            return new ValueTask<Object>(source.Task);
        }
        catch (Exception ex)
        {
            if (ex is TaskCanceledException)
                span?.AppendTag(ex.Message);
            else
                span?.SetError(ex, null);

            span?.Dispose();
            if (context != null) ReturnContext(context);
            throw;
        }
    }

    void ISocketRemote.Process(IData data)
    {
        var span = DefaultSpan.Current;
        if (span != null && data.Message is ITraceMessage traceMessage) span.Detach(traceMessage.TraceId);

        if (data is ReceivedEventArgs eventArgs) OnReceive(eventArgs);
    }

    /// <summary>错误发生或断开连接时</summary>
    public event EventHandler<ExceptionEventArgs>? Error;

    /// <summary>触发错误</summary>
    /// <param name="action">动作</param>
    /// <param name="exception">异常</param>
    protected internal virtual void OnError(String action, Exception exception)
    {
        if (Pipeline != null)
        {
            var context = CreateContext(this);
            Pipeline.Error(context, exception);
            ReturnContext(context);
        }

        Log.Error("{0}{1}Error {2} {3}", LogPrefix, action, this, exception.Message);
        Error?.Invoke(this, new ExceptionEventArgs(action, exception));
    }

    /// <summary>输出日志</summary>
    /// <param name="format">格式模板</param>
    /// <param name="args">参数</param>
    public void WriteLog(String format, params Object?[] args)
    {
        LogPrefix ??= Name.TrimEnd("Server", "Session", "Client");
        if (Log.Enable) Log.Info($"[{LogPrefix}]{format}", args);
    }

    private Boolean TrySetResponse(IPacket packet)
    {
        var source = _responseSource;
        if (source == null) return false;

        _responseSource = null;
        source.TrySetResult(packet);
        return true;
    }

    private async Task<IPacket?> WaitResponseAsync(TaskCompletionSource<IPacket?> source)
    {
        if (Timeout <= 0) return await source.Task.ConfigureAwait(false);

        using var cancellationTokenSource = new CancellationTokenSource(Timeout);
        using var registration = cancellationTokenSource.Token.Register(() => source.TrySetResult(null));
        return await source.Task.ConfigureAwait(false);
    }

    private void ReleaseRecv(SocketAsyncEventArgs socketEventArgs, String reason)
    {
        var index = socketEventArgs.UserToken.ToInt();
        if (Log.Level <= LogLevel.Debug) WriteLog("释放RecvSA {0} {1}", index, reason);

        if (_recvCount > 0) Interlocked.Decrement(ref _recvCount);
        try
        {
            socketEventArgs.SetBuffer(null, 0, 0);
        }
        catch
        {
        }

        socketEventArgs.Dispose();
    }
}