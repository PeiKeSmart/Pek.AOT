using System.Diagnostics.CodeAnalysis;

using Pek.Log;
using Pek.Messaging;

namespace Pek.Caching;

/// <summary>消息队列事件总线。通过消息队列发布和订阅消息</summary>
/// <typeparam name="TEvent">事件类型</typeparam>
/// <param name="cache">缓存实例</param>
/// <param name="topic">主题</param>
public class QueueEventBus<TEvent>(ICache cache, String topic) : EventBus<TEvent>, ITracerFeature
{
    /// <summary>链路追踪</summary>
    public ITracer? Tracer { get; set; }

    private IProducerConsumer<TEvent>? _queue;
    private CancellationTokenSource? _source;
    private Task? _consumerTask;

    /// <summary>释放资源</summary>
    /// <param name="disposing">是否由 Dispose 调用</param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        var source = Interlocked.Exchange(ref _source, null);
        if (source != null)
        {
            try
            {
                if (!source.IsCancellationRequested) source.Cancel();
            }
            catch (ObjectDisposedException) { }
        }

        var task = Interlocked.Exchange(ref _consumerTask, null);
        if (task != null && !task.IsCompleted)
        {
            try
            {
                task.Wait(3_000);
            }
            catch (AggregateException) { }
        }

        source?.Dispose();
    }

    /// <summary>初始化</summary>
    [MemberNotNull(nameof(_queue))]
    protected virtual void Init()
    {
        Tracer ??= (cache as ITracerFeature)?.Tracer;
        _queue ??= cache.GetQueue<TEvent>(topic);
    }

    /// <summary>发布消息</summary>
    /// <param name="event">事件</param>
    /// <param name="context">上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发布数量</returns>
    public override Task<Int32> PublishAsync(TEvent @event, IEventContext? context = null, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return Task.FromCanceled<Int32>(cancellationToken);

        Init();
        var rs = _queue.Add(@event);
        return Task.FromResult(rs);
    }

    /// <summary>订阅消息</summary>
    /// <param name="handler">处理器</param>
    /// <param name="clientId">客户标识</param>
    /// <returns>是否成功</returns>
    public override Boolean Subscribe(IEventHandler<TEvent> handler, String clientId = "")
    {
        if (_source == null)
        {
            var source = new CancellationTokenSource();
            if (Interlocked.CompareExchange(ref _source, source, null) == null)
            {
                Init();
                var task = Task.Factory.StartNew(() => ConsumeMessage(source), source.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
                Interlocked.Exchange(ref _consumerTask, task);
            }
            else
            {
                source.Dispose();
            }
        }

        return base.Subscribe(handler, clientId);
    }

    /// <summary>消费并分发消息</summary>
    /// <param name="source">取消令牌源</param>
    protected virtual async Task ConsumeMessage(CancellationTokenSource source)
    {
        DefaultSpan.Current = null;
        var cancellationToken = source.Token;

        while (!cancellationToken.IsCancellationRequested)
        {
            ISpan? span = null;
            try
            {
                var message = await _queue!.TakeOneAsync(15, cancellationToken).ConfigureAwait(false);
                if (message is not null)
                {
                    span = Tracer?.NewSpan($"event:{topic}", message);
                    if (span != null && message is ITraceMessage traceMessage) span.Detach(traceMessage.TraceId);

                    await DispatchAsync(message, null, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await Task.Delay(1_000, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                if (cancellationToken.IsCancellationRequested) break;
            }
            catch (ObjectDisposedException)
            {
                if (cancellationToken.IsCancellationRequested) break;
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested) break;
                span?.SetError(ex);
            }
            finally
            {
                span?.Dispose();
            }
        }
    }
}