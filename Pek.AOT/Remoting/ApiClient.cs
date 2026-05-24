using Pek.Collections;
using Pek.Data;
using Pek.Extension;
using Pek.Log;
using Pek.Messaging;
using Pek.Net;
using Pek.Serialization;
using Pek.Threading;

using NewLife;

namespace Pek.Remoting;

/// <summary>应用接口客户端</summary>
public class ApiClient : ApiHost, IApiClient
{
    private readonly Object _syncRoot = new();
    private TimerX? _timer;
    private String? _last;

    #region 属性
    /// <summary>是否已打开</summary>
    public Boolean Active { get; protected set; }

    /// <summary>服务端地址集合。负载均衡</summary>
    public String[] Servers { get; set; } = [];

    /// <summary>客户端连接集群</summary>
    public ICluster<String, ISocketClient>? Cluster { get; set; }

    /// <summary>是否使用连接池。true时建立多个到服务端的连接（高吞吐），默认false使用单一连接（低延迟）</summary>
    public Boolean UsePool { get; set; }

    /// <summary>令牌。每次请求携带</summary>
    public String? Token { get; set; }

    /// <summary>最后活跃时间</summary>
    public DateTime LastActive { get; set; }

    /// <summary>调用统计</summary>
    public ICounter? StatInvoke { get; set; }

    /// <summary>性能跟踪器</summary>
    public ITracer? Tracer { get; set; } = DefaultTracer.Instance;

    /// <summary>显示统计信息的周期。默认600秒，0表示不显示统计信息</summary>
    public Int32 StatPeriod { get; set; } = 600;
    #endregion

    #region 构造
    /// <summary>实例化应用接口客户端</summary>
    public ApiClient()
    {
        var type = GetType();
        Name = type.GetDisplayName() ?? type.Name.TrimEnd("Client");
    }

    /// <summary>实例化应用接口客户端</summary>
    /// <param name="uris">服务端地址集合，逗号分隔</param>
    public ApiClient(String uris) : this()
    {
        if (!uris.IsNullOrEmpty()) Servers = uris.Split(",", ";");
    }

    /// <summary>销毁</summary>
    /// <param name="disposing">是否显式销毁</param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        _timer.TryDispose();
        Close(Name + (disposing ? "Dispose" : "GC"));
    }
    #endregion

    #region 打开关闭
    /// <summary>打开客户端</summary>
    /// <returns>是否成功</returns>
    public virtual Boolean Open()
    {
        if (Active) return true;

        lock (_syncRoot)
        {
            if (Active) return true;

            if (Servers == null || Servers.Length == 0) throw new ArgumentNullException(nameof(Servers), "未指定服务端地址");

            Encoder ??= new JsonEncoder();
            Encoder.Log = EncoderLog;

            Cluster = InitCluster();
            WriteLog("集群：{0}", Cluster);

            var period = StatPeriod * 1000;
            if (period > 0)
            {
                StatInvoke ??= new PerfCounter();
                _timer ??= new TimerX(DoWork, null, period, period) { Async = true };
            }

            Active = true;
            return true;
        }
    }

    /// <summary>关闭</summary>
    /// <param name="reason">关闭原因。便于日志分析</param>
    /// <returns>是否成功</returns>
    public virtual Boolean Close(String reason)
    {
        if (!Active) return true;

        _timer.TryDispose();
        _timer = null;

        var cluster = Cluster;
        cluster?.Close(reason ?? (GetType().Name + "Close"));
        Active = false;
        return true;
    }

    /// <summary>初始化集群</summary>
    /// <returns>集群实例</returns>
    protected virtual ICluster<String, ISocketClient> InitCluster()
    {
        var cluster = Cluster;
        if (cluster == null)
            cluster = UsePool ? new ClientPoolCluster { Log = Log } : new ClientSingleCluster { Log = Log };

        if (cluster is ClientSingleCluster singleCluster && singleCluster.OnCreate == null) singleCluster.OnCreate = OnCreate;
        if (cluster is ClientPoolCluster poolCluster && poolCluster.OnCreate == null) poolCluster.OnCreate = OnCreate;

        cluster.GetItems ??= () => Servers;
        cluster.Open();

        return cluster;
    }

    private ICluster<String, ISocketClient> EnsureCluster()
    {
        Open();
        return Cluster ?? throw new InvalidOperationException("集群尚未初始化");
    }
    #endregion

    #region 远程调用
    /// <summary>异步调用，等待返回结果</summary>
    /// <typeparam name="TResult">返回类型</typeparam>
    /// <param name="action">服务操作</param>
    /// <param name="args">参数</param>
    /// <param name="cancellationToken">取消通知</param>
    /// <returns>调用结果</returns>
    public virtual async Task<TResult?> InvokeAsync<TResult>(String action, Object? args = null, CancellationToken cancellationToken = default)
    {
        var act = action;

        try
        {
            return await InvokeWithClientAsync<TResult>(null, act, args, 0, cancellationToken).ConfigureAwait(false);
        }
        catch (ApiException ex) when (ex.Code == 401)
        {
            await EnsureCluster().InvokeAsync(client => OnLoginAsync(client, true)).ConfigureAwait(false);
            return await InvokeWithClientAsync<TResult>(null, act, args, 0, cancellationToken).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            throw new TaskCanceledException($"[{act}]超时[{Timeout:n0}ms]取消");
        }
    }

    /// <summary>同步调用，阻塞等待</summary>
    /// <typeparam name="TResult">返回类型</typeparam>
    /// <param name="action">服务操作</param>
    /// <param name="args">参数</param>
    /// <returns>调用结果</returns>
    public virtual TResult? Invoke<TResult>(String action, Object? args = null) => InvokeAsync<TResult>(action, args).ConfigureAwait(false).GetAwaiter().GetResult();

    /// <summary>单向发送。同步调用，不等待返回</summary>
    /// <param name="action">服务操作</param>
    /// <param name="args">参数</param>
    /// <param name="flag">标识</param>
    /// <returns>发送字节数</returns>
    public virtual Int32 InvokeOneWay(String action, Object? args = null, Byte flag = 0)
    {
        var cluster = EnsureCluster();
        return cluster.Invoke(client => SendOneWay(client, action, args, flag));
    }

    /// <summary>指定客户端的异步调用，等待返回结果</summary>
    /// <typeparam name="TResult">返回类型</typeparam>
    /// <param name="client">客户端</param>
    /// <param name="action">服务操作</param>
    /// <param name="args">参数</param>
    /// <param name="flag">标识</param>
    /// <param name="cancellationToken">取消通知</param>
    /// <returns>调用结果</returns>
    public virtual async Task<TResult?> InvokeWithClientAsync<TResult>(ISocketClient? client, String action, Object? args = null, Byte flag = 0, CancellationToken cancellationToken = default)
    {
        var counter = StatInvoke;
        var startTicks = counter.StartCount();

        LastActive = DateTime.Now;
        var mergedArgs = MergeToken(args);

        using var span = Tracer?.NewSpan("rpc:" + action, mergedArgs);
        if (span != null) mergedArgs = span.Attach(mergedArgs);

        var encoder = Encoder;
        var msg = encoder.CreateRequest(action, mergedArgs);
        if (flag > 0 && msg is DefaultMessage defaultMessage) defaultMessage.Flag = flag;

        var invoker = client != null ? client + String.Empty : ToString();
        IMessage? response = null;
        try
        {
            if (client != null)
                response = await ToMessageAsync(client.SendMessageAsync(msg, cancellationToken)).ConfigureAwait(false);
            else
                response = await EnsureCluster().InvokeAsync(socket => ToMessageAsync(socket.SendMessageAsync(msg, cancellationToken))).ConfigureAwait(false);

            if (response == null) return default;
        }
        catch (AggregateException aggregateException)
        {
            var ex = aggregateException.GetTrue();
            span?.SetError(ex, mergedArgs);

            if (ex is TaskCanceledException) throw new TimeoutException($"请求[{action}]超时({msg})！", ex);
            throw;
        }
        catch (TaskCanceledException ex)
        {
            throw new TimeoutException($"请求[{action}]超时({msg})！", ex);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, mergedArgs);
            throw;
        }
        finally
        {
            var elapsed = counter.StopCount(startTicks) / 1000;
            if (SlowTrace > 0 && elapsed >= SlowTrace) WriteLog($"慢调用[{action}]({msg})，耗时{elapsed:n0}ms");
        }

        var resultType = typeof(TResult);
        if (resultType == typeof(IMessage)) return (TResult?)(Object?)response;

        if (!encoder.Decode(response, out _, out var code, out var data)) throw new InvalidOperationException("无法解码远程响应");

        if (code is not 0 and not 200)
            throw new ApiException(code, data?.ToStr()?.Trim('"') ?? String.Empty) { Source = invoker + "/" + action };

        if (data == null) return default;
        if (resultType == typeof(IPacket)) return (TResult?)(Object?)data;

#pragma warning disable CS0618
        if (resultType == typeof(Packet)) return (TResult?)(Object?)new Packet(data.ReadBytes());
#pragma warning restore CS0618

        var result = encoder.DecodeResult(action, data, response);
        if (resultType == typeof(Object)) return (TResult?)result;
        return (TResult?)encoder.Convert(result, resultType);
    }

    private Int32 SendOneWay(ISocketRemote session, String action, Object? args = null, Byte flag = 0)
    {
        var counter = StatInvoke;
        var mergedArgs = MergeToken(args);

        using var span = Tracer?.NewSpan("rpc:" + action, mergedArgs);
        if (span != null) mergedArgs = span.Attach(mergedArgs);

        var msg = Encoder.CreateRequest(action, mergedArgs);
        if (msg is DefaultMessage defaultMessage)
        {
            defaultMessage.OneWay = true;
            if (flag > 0) defaultMessage.Flag = flag;
        }

        var startTicks = counter.StartCount();
        try
        {
            return session.SendMessage(msg);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, mergedArgs);
            throw;
        }
        finally
        {
            var elapsed = counter.StopCount(startTicks) / 1000;
            if (SlowTrace > 0 && elapsed >= SlowTrace) WriteLog($"慢调用[{action}]，耗时{elapsed:n0}ms");
        }
    }
    #endregion

    #region 异步接收
    /// <summary>客户端收到服务端主动下发消息</summary>
    /// <param name="message">消息</param>
    protected virtual void OnReceive(IMessage message) { }

    private void ClientReceived(Object? sender, ReceivedEventArgs e)
    {
        LastActive = DateTime.Now;
        if (e.Message is not IMessage msg || msg.Reply) return;

        OnReceive(msg);
    }
    #endregion

    #region 登录
    /// <summary>新会话。客户端每次连接或断线重连后，可用 InvokeWithClientAsync 做登录</summary>
    /// <param name="client">会话</param>
    public virtual void OnNewSession(ISocketClient client) => OnLoginAsync(client, true).ConfigureAwait(false).GetAwaiter().GetResult();

    /// <summary>连接后自动登录</summary>
    /// <param name="client">客户端</param>
    /// <param name="force">强制登录</param>
    /// <returns>登录结果</returns>
    protected virtual Task<Object?> OnLoginAsync(ISocketClient client, Boolean force) => Task.FromResult<Object?>(null);

    /// <summary>登录</summary>
    /// <returns>登录结果</returns>
    public virtual Task<Object?> LoginAsync() => EnsureCluster().InvokeAsync(client => OnLoginAsync(client, false));
    #endregion

    #region 连接池
    /// <summary>创建客户端之后，打开连接之前</summary>
    /// <param name="server">服务端地址</param>
    /// <returns>客户端</returns>
    protected virtual ISocketClient OnCreate(String server)
    {
        var client = new NetUri(server).CreateRemote();
        client.Timeout = Timeout;
        client.Tracer = Tracer;
        client.Log = Log;

        client.Add((Pek.Model.IPipelineHandler)GetMessageCodec());
        client.Opened += ClientOpened;
        client.Received += ClientReceived;

        return client;
    }

    private void ClientOpened(Object? sender, EventArgs e)
    {
        if (sender is ISocketClient client) OnNewSession(client);
    }
    #endregion

    #region 统计
    private void DoWork(Object? state)
    {
        var builder = Pool.StringBuilder.Get();
        var counter = StatInvoke;
        if (counter != null && counter.Value > 0) builder.AppendFormat("请求：{0} ", counter);

        var message = builder.Return(true);
        if (message.IsNullOrEmpty() || message == _last) return;
        _last = message;

        WriteLog(message);
    }
    #endregion

    #region 辅助
    protected virtual Object? MergeToken(Object? args)
    {
        if (Token.IsNullOrEmpty()) return args;
        if (args == null)
            return new Dictionary<String, Object?>(StringComparer.OrdinalIgnoreCase) { ["Token"] = Token };

        IDictionary<String, Object?>? dictionary = null;
        if (args is IDictionarySource source)
            dictionary = new Dictionary<String, Object?>(source.ToDictionary(), StringComparer.OrdinalIgnoreCase);
        else if (args is IDictionary<String, Object?> objectDictionary)
            dictionary = new Dictionary<String, Object?>(objectDictionary, StringComparer.OrdinalIgnoreCase);
        else if (args is IDictionary<String, String> stringDictionary)
            dictionary = stringDictionary.ToDictionary(item => item.Key, item => (Object?)item.Value, StringComparer.OrdinalIgnoreCase);
        else if (args is IEnumerable<KeyValuePair<String, Object?>> pairs)
            dictionary = pairs.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
        else
        {
            var json = JsonHelper.Default.Write(args, false, true, false);
            dictionary = JsonHelper.Default.Decode(json) == null ? null : new Dictionary<String, Object?>(JsonHelper.Default.Decode(json)!, StringComparer.OrdinalIgnoreCase);
        }

        if (dictionary == null || dictionary.ContainsKey("Token")) return args;

        dictionary["Token"] = Token;
        return dictionary;
    }

#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
    private static async ValueTask<IMessage?> ToMessageAsync(ValueTask<Object> task)
    {
        var obj = await task.ConfigureAwait(false);
        return obj as IMessage;
    }
#else
    private static async Task<IMessage?> ToMessageAsync(Task<Object> task)
    {
        var obj = await task.ConfigureAwait(false);
        return obj as IMessage;
    }
#endif
    #endregion
}