using System.Collections.Concurrent;
using Pek;
using Pek.Log;

namespace Pek.Messaging;

/// <summary>默认事件中心</summary>
public class EventHub : DisposeBase, IEventHub, ILogFeature
{
    private readonly ConcurrentDictionary<Type, SubscriptionCollection> _typedSubscriptions = new();
    private readonly ConcurrentDictionary<String, SubscriptionCollection> _namedSubscriptions = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>跟踪器</summary>
    public ITracer? Tracer { get; set; } = DefaultTracer.Instance;

    /// <summary>处理器抛出异常时是否继续</summary>
    public Boolean IgnoreHandlerException { get; set; }

    /// <summary>订阅指定类型事件</summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">同步处理器</param>
    public virtual void Subscribe<TEvent>(Action<TEvent, IEventContext> handler)
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        ThrowIfDisposed();
        GetTypedCollection(typeof(TEvent)).Add(new Subscription(handler, context =>
        {
            handler((TEvent)context.Event!, context);
            return default;
        }));
    }

    /// <summary>订阅指定类型事件</summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">异步处理器</param>
    public virtual void Subscribe<TEvent>(Func<TEvent, IEventContext, Task> handler)
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        ThrowIfDisposed();
        GetTypedCollection(typeof(TEvent)).Add(new Subscription(handler, async context =>
        {
            await handler((TEvent)context.Event!, context).ConfigureAwait(false);
        }));
    }

    /// <summary>订阅命名事件</summary>
    /// <param name="name">事件名</param>
    /// <param name="handler">同步处理器</param>
    public virtual void Subscribe(String name, Action<IEventContext> handler)
    {
        if (String.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        ThrowIfDisposed();
        GetNamedCollection(name).Add(new Subscription(handler, context =>
        {
            handler(context);
            return default;
        }));
    }

    /// <summary>订阅命名事件</summary>
    /// <param name="name">事件名</param>
    /// <param name="handler">异步处理器</param>
    public virtual void Subscribe(String name, Func<IEventContext, Task> handler)
    {
        if (String.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        ThrowIfDisposed();
        GetNamedCollection(name).Add(new Subscription(handler, async context => await handler(context).ConfigureAwait(false)));
    }

    /// <summary>取消订阅指定类型事件</summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">同步处理器</param>
    /// <returns>是否成功</returns>
    public virtual Boolean Unsubscribe<TEvent>(Action<TEvent, IEventContext> handler) => handler != null && _typedSubscriptions.TryGetValue(typeof(TEvent), out var collection) && collection.Remove(handler);

    /// <summary>取消订阅指定类型事件</summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">异步处理器</param>
    /// <returns>是否成功</returns>
    public virtual Boolean Unsubscribe<TEvent>(Func<TEvent, IEventContext, Task> handler) => handler != null && _typedSubscriptions.TryGetValue(typeof(TEvent), out var collection) && collection.Remove(handler);

    /// <summary>取消订阅命名事件</summary>
    /// <param name="name">事件名</param>
    /// <param name="handler">同步处理器</param>
    /// <returns>是否成功</returns>
    public virtual Boolean Unsubscribe(String name, Action<IEventContext> handler) => !String.IsNullOrWhiteSpace(name) && handler != null && _namedSubscriptions.TryGetValue(name, out var collection) && collection.Remove(handler);

    /// <summary>取消订阅命名事件</summary>
    /// <param name="name">事件名</param>
    /// <param name="handler">异步处理器</param>
    /// <returns>是否成功</returns>
    public virtual Boolean Unsubscribe(String name, Func<IEventContext, Task> handler) => !String.IsNullOrWhiteSpace(name) && handler != null && _namedSubscriptions.TryGetValue(name, out var collection) && collection.Remove(handler);

    /// <summary>发布指定类型事件</summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="event">事件对象</param>
    /// <param name="context">事件上下文</param>
    /// <returns>命中处理器数量</returns>
    public virtual Int32 Publish<TEvent>(TEvent @event, IEventContext? context = null) => PublishAsync(@event, context).GetAwaiter().GetResult();

    /// <summary>异步发布指定类型事件</summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="event">事件对象</param>
    /// <param name="context">事件上下文</param>
    /// <returns>命中处理器数量</returns>
    public virtual async Task<Int32> PublishAsync<TEvent>(TEvent @event, IEventContext? context = null)
    {
        var type = typeof(TEvent);
        var name = type.FullName ?? type.Name;
        var handlers = _typedSubscriptions.TryGetValue(type, out var collection) ? collection.Snapshot() : [];
        return await PublishCoreAsync(name, @event, context, handlers).ConfigureAwait(false);
    }

    /// <summary>发布命名事件</summary>
    /// <param name="name">事件名</param>
    /// <param name="event">事件对象</param>
    /// <param name="context">事件上下文</param>
    /// <returns>命中处理器数量</returns>
    public virtual Int32 Publish(String name, Object? @event = null, IEventContext? context = null) => PublishAsync(name, @event, context).GetAwaiter().GetResult();

    /// <summary>异步发布命名事件</summary>
    /// <param name="name">事件名</param>
    /// <param name="event">事件对象</param>
    /// <param name="context">事件上下文</param>
    /// <returns>命中处理器数量</returns>
    public virtual async Task<Int32> PublishAsync(String name, Object? @event = null, IEventContext? context = null)
    {
        if (String.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));

        var handlers = _namedSubscriptions.TryGetValue(name, out var collection) ? collection.Snapshot() : [];
        return await PublishCoreAsync(name, @event, context, handlers).ConfigureAwait(false);
    }

    /// <summary>清空所有订阅</summary>
    public virtual void Clear()
    {
        _typedSubscriptions.Clear();
        _namedSubscriptions.Clear();
    }

    /// <summary>释放资源</summary>
    /// <param name="disposing">是否显式释放</param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        if (!disposing) return;
        Clear();
    }

    private SubscriptionCollection GetTypedCollection(Type type) => _typedSubscriptions.GetOrAdd(type, static _ => new SubscriptionCollection());

    private SubscriptionCollection GetNamedCollection(String name) => _namedSubscriptions.GetOrAdd(name, static _ => new SubscriptionCollection());

    private async Task<Int32> PublishCoreAsync(String name, Object? @event, IEventContext? context, Subscription[] handlers)
    {
        ThrowIfDisposed();

        if (handlers.Length == 0) return 0;

        var ownContext = false;
        if (context == null)
        {
            context = EventContext.Rent();
            ownContext = true;
        }

        context.Name = String.IsNullOrEmpty(context.Name) ? name : context.Name;
        if (context.Event == null) context.Event = @event;

        using var span = Tracer?.NewSpan("EventHub.Publish", context.Name);
        try
        {
            for (var i = 0; i < handlers.Length; i++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                await handlers[i].Invoke(context).ConfigureAwait(false);
                context.Handled = true;
            }

            this.WriteLog("EventHub Publish Name={0} Handlers={1}", context.Name, handlers.Length);
            return handlers.Length;
        }
        catch (Exception ex)
        {
            context.Exception = ex;
            span?.SetError(ex, context.Name);
            this.WriteLog("EventHub Publish Error Name={0} Error={1}", context.Name, ex.Message);
            if (!IgnoreHandlerException) throw;
            return handlers.Length;
        }
        finally
        {
            if (ownContext && context is EventContext eventContext) EventContext.Return(eventContext);
        }
    }

    private sealed class Subscription
    {
        public Subscription(Delegate source, Func<IEventContext, ValueTask> invoke)
        {
            Source = source;
            Invoke = invoke;
        }

        public Delegate Source { get; }

        public Func<IEventContext, ValueTask> Invoke { get; }
    }

    private sealed class SubscriptionCollection
    {
        private readonly List<Subscription> _items = [];
        private readonly Object _lock = new();

        public void Add(Subscription subscription)
        {
            lock (_lock)
            {
                _items.Add(subscription);
            }
        }

        public Boolean Remove(Delegate source)
        {
            lock (_lock)
            {
                var index = _items.FindIndex(e => e.Source == source);
                if (index < 0) return false;

                _items.RemoveAt(index);
                return true;
            }
        }

        public Subscription[] Snapshot()
        {
            lock (_lock)
            {
                return [.. _items];
            }
        }
    }
}