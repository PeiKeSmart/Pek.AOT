using System.Collections.Concurrent;

using Pek.Collections;
using Pek.Data;
using Pek.Log;

namespace Pek.Messaging;

/// <summary>事件总线基接口</summary>
public interface IEventBus
{
    /// <summary>发布事件</summary>
    /// <param name="event">事件</param>
    /// <param name="context">事件上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功处理数量</returns>
    Task<Int32> PublishAsync(Object @event, IEventContext? context = null, CancellationToken cancellationToken = default);
}

/// <summary>事件总线</summary>
/// <typeparam name="TEvent">事件类型</typeparam>
public interface IEventBus<TEvent>
{
    /// <summary>发布事件</summary>
    /// <param name="event">事件</param>
    /// <param name="context">事件上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功处理数量</returns>
    Task<Int32> PublishAsync(TEvent @event, IEventContext? context = null, CancellationToken cancellationToken = default);

    /// <summary>订阅事件</summary>
    /// <param name="handler">处理器</param>
    /// <param name="clientId">客户标识</param>
    /// <returns>是否成功</returns>
    Boolean Subscribe(IEventHandler<TEvent> handler, String clientId = "");

    /// <summary>取消订阅</summary>
    /// <param name="clientId">客户标识</param>
    /// <returns>是否成功</returns>
    Boolean Unsubscribe(String clientId = "");
}

/// <summary>异步事件总线</summary>
/// <typeparam name="TEvent">事件类型</typeparam>
public interface IAsyncEventBus<TEvent> : IEventBus<TEvent>
{
    /// <summary>异步订阅</summary>
    /// <param name="handler">处理器</param>
    /// <param name="clientId">客户标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<Boolean> SubscribeAsync(IEventHandler<TEvent> handler, String clientId = "", CancellationToken cancellationToken = default);

    /// <summary>异步取消订阅</summary>
    /// <param name="clientId">客户标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<Boolean> UnsubscribeAsync(String clientId = "", CancellationToken cancellationToken = default);
}

/// <summary>事件上下文接口</summary>
public interface IEventContext
{
    /// <summary>事件总线</summary>
    IEventBus EventBus { get; }
}

/// <summary>事件处理器</summary>
/// <typeparam name="TEvent">事件类型</typeparam>
public interface IEventHandler<TEvent>
{
    /// <summary>处理事件</summary>
    /// <param name="event">事件</param>
    /// <param name="context">上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    Task HandleAsync(TEvent @event, IEventContext? context, CancellationToken cancellationToken);
}

/// <summary>事件总线工厂</summary>
public interface IEventBusFactory
{
    /// <summary>创建事件总线</summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="topic">主题</param>
    /// <param name="clientId">客户标识</param>
    /// <returns>事件总线</returns>
    IEventBus<TEvent> CreateEventBus<TEvent>(String topic, String clientId = "");
}

/// <summary>默认事件总线</summary>
/// <typeparam name="TEvent">事件类型</typeparam>
public class EventBus<TEvent> : DisposeBase, IEventBus, IEventBus<TEvent>, IAsyncEventBus<TEvent>, ILogFeature
{
    private readonly ConcurrentDictionary<String, IEventHandler<TEvent>> _handlers = [];
    private readonly Pool<EventContext> _pool = new();

    /// <summary>已订阅处理器</summary>
    public IDictionary<String, IEventHandler<TEvent>> Handlers => _handlers;

    /// <summary>处理器异常时是否抛出</summary>
    public Boolean ThrowOnHandlerError { get; set; }

    /// <summary>发布事件</summary>
    public virtual Task<Int32> PublishAsync(TEvent @event, IEventContext? context = null, CancellationToken cancellationToken = default)
    {
        if (@event is ITraceMessage traceMessage && String.IsNullOrWhiteSpace(traceMessage.TraceId)) traceMessage.TraceId = DefaultSpan.Current?.ToString();
        return DispatchAsync(@event, context, cancellationToken);
    }

    Task<Int32> IEventBus.PublishAsync(Object @event, IEventContext? context, CancellationToken cancellationToken) => PublishAsync((TEvent)@event, context, cancellationToken);

    /// <summary>分发事件</summary>
    protected virtual async Task<Int32> DispatchAsync(TEvent @event, IEventContext? context, CancellationToken cancellationToken)
    {
        var rs = 0;

        EventContext? ownContext = null;
        if (context == null)
        {
            ownContext = _pool.Get();
            ownContext.EventBus = this;
            context = ownContext;
        }

        var clientId = (context as EventContext)?.ClientId;
        foreach (var item in _handlers)
        {
            if (clientId != null && clientId == item.Key) continue;

            try
            {
                await item.Value.HandleAsync(@event, context, cancellationToken).ConfigureAwait(false);
                rs++;
            }
            catch (Exception ex)
            {
                Log?.Error("事件处理器 [{0}] 处理事件时发生异常: {1}", item.Key, ex.Message);
                if (ThrowOnHandlerError) throw;
            }
        }

        if (ownContext != null)
        {
            ownContext.Reset();
            _pool.Return(ownContext);
        }

        return rs;
    }

    /// <summary>订阅事件</summary>
    public virtual Boolean Subscribe(IEventHandler<TEvent> handler, String clientId = "")
    {
        _handlers[clientId] = handler;
        return true;
    }

    /// <summary>取消订阅</summary>
    public virtual Boolean Unsubscribe(String clientId = "") => _handlers.TryRemove(clientId, out _);

    /// <summary>异步订阅</summary>
    public virtual Task<Boolean> SubscribeAsync(IEventHandler<TEvent> handler, String clientId = "", CancellationToken cancellationToken = default) => Task.FromResult(Subscribe(handler, clientId));

    /// <summary>异步取消订阅</summary>
    public virtual Task<Boolean> UnsubscribeAsync(String clientId = "", CancellationToken cancellationToken = default) => Task.FromResult(Unsubscribe(clientId));

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>写日志</summary>
    public void WriteLog(String format, params Object[] args)
    {
        var span = DefaultSpan.Current as DefaultSpan;
        span?.AppendTag(String.Format(format, args));
        Log?.Info(format, args);
    }
}

/// <summary>事件总线扩展</summary>
public static class EventBusExtensions
{
    /// <summary>订阅事件</summary>
    public static void Subscribe<TEvent>(this IEventBus<TEvent> bus, Action<TEvent> action, String clientId = "") => bus.Subscribe(new DelegateEventHandler<TEvent>(action), clientId);

    /// <summary>订阅事件</summary>
    public static void Subscribe<TEvent>(this IEventBus<TEvent> bus, Action<TEvent, IEventContext> action, String clientId = "") => bus.Subscribe(new DelegateEventHandler<TEvent>(action), clientId);

    /// <summary>订阅事件</summary>
    public static void Subscribe<TEvent>(this IEventBus<TEvent> bus, Func<TEvent, Task> action, String clientId = "") => bus.Subscribe(new DelegateEventHandler<TEvent>(action), clientId);

    /// <summary>订阅事件</summary>
    public static void Subscribe<TEvent>(this IEventBus<TEvent> bus, Func<TEvent, IEventContext, CancellationToken, Task> action, String clientId = "") => bus.Subscribe(new DelegateEventHandler<TEvent>(action), clientId);

    /// <summary>异步订阅事件</summary>
    public static Task<Boolean> SubscribeAsync<TEvent>(this IAsyncEventBus<TEvent> bus, Action<TEvent> action, String clientId = "", CancellationToken cancellationToken = default) => bus.SubscribeAsync(new DelegateEventHandler<TEvent>(action), clientId, cancellationToken);

    /// <summary>异步订阅事件</summary>
    public static Task<Boolean> SubscribeAsync<TEvent>(this IAsyncEventBus<TEvent> bus, Action<TEvent, IEventContext> action, String clientId = "", CancellationToken cancellationToken = default) => bus.SubscribeAsync(new DelegateEventHandler<TEvent>(action), clientId, cancellationToken);

    /// <summary>异步订阅事件</summary>
    public static Task<Boolean> SubscribeAsync<TEvent>(this IAsyncEventBus<TEvent> bus, Func<TEvent, Task> action, String clientId = "", CancellationToken cancellationToken = default) => bus.SubscribeAsync(new DelegateEventHandler<TEvent>(action), clientId, cancellationToken);

    /// <summary>异步订阅事件</summary>
    public static Task<Boolean> SubscribeAsync<TEvent>(this IAsyncEventBus<TEvent> bus, Func<TEvent, IEventContext, CancellationToken, Task> action, String clientId = "", CancellationToken cancellationToken = default) => bus.SubscribeAsync(new DelegateEventHandler<TEvent>(action), clientId, cancellationToken);
}

/// <summary>事件上下文</summary>
public class EventContext : IEventContext, IExtend
{
    /// <summary>事件总线</summary>
    public IEventBus EventBus { get; set; } = null!;

    /// <summary>消息主题</summary>
    public String? Topic { get; set; }

    /// <summary>客户标识</summary>
    public String? ClientId { get; set; }

    /// <summary>数据项</summary>
    public IDictionary<String, Object?> Items { get; } = new Dictionary<String, Object?>();

    /// <summary>索引器</summary>
    public Object? this[String key]
    {
        get => Items.TryGetValue(key, out var obj) ? obj : null;
        set => Items[key] = value;
    }

    /// <summary>重置</summary>
    public void Reset()
    {
        EventBus = null!;
        Topic = null;
        ClientId = null;
        Items.Clear();
    }
}

/// <summary>委托事件处理器</summary>
/// <typeparam name="TEvent">事件类型</typeparam>
public class DelegateEventHandler<TEvent>(Delegate method) : IEventHandler<TEvent>
{
    private readonly Delegate _method = method ?? throw new ArgumentNullException(nameof(method));

    /// <summary>处理事件</summary>
    public Task HandleAsync(TEvent @event, IEventContext? context, CancellationToken cancellationToken = default)
    {
        if (_method is Func<TEvent, Task> func) return func(@event);
        if (_method is Func<TEvent, IEventContext, CancellationToken, Task> func2) return func2(@event, context!, cancellationToken);

        if (_method is Action<TEvent> act)
            act(@event);
        else if (_method is Action<TEvent, IEventContext> act2)
            act2(@event, context!);
        else
            throw new NotSupportedException($"不支持的委托类型: {_method.GetType().FullName}");

        return Task.CompletedTask;
    }
}