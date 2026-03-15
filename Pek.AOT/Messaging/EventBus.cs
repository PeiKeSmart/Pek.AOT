namespace Pek.Messaging;

/// <summary>全局事件总线</summary>
public static class EventBus
{
    private static IEventHub _default = new EventHub();

    /// <summary>默认事件中心</summary>
    public static IEventHub Default
    {
        get => _default;
        set => _default = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>订阅指定类型事件</summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">同步处理器</param>
    public static void Subscribe<TEvent>(Action<TEvent, IEventContext> handler) => Default.Subscribe(handler);

    /// <summary>订阅指定类型事件</summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">异步处理器</param>
    public static void Subscribe<TEvent>(Func<TEvent, IEventContext, Task> handler) => Default.Subscribe(handler);

    /// <summary>订阅命名事件</summary>
    /// <param name="name">事件名</param>
    /// <param name="handler">同步处理器</param>
    public static void Subscribe(String name, Action<IEventContext> handler) => Default.Subscribe(name, handler);

    /// <summary>订阅命名事件</summary>
    /// <param name="name">事件名</param>
    /// <param name="handler">异步处理器</param>
    public static void Subscribe(String name, Func<IEventContext, Task> handler) => Default.Subscribe(name, handler);

    /// <summary>取消订阅指定类型事件</summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">同步处理器</param>
    /// <returns>是否成功</returns>
    public static Boolean Unsubscribe<TEvent>(Action<TEvent, IEventContext> handler) => Default.Unsubscribe(handler);

    /// <summary>取消订阅指定类型事件</summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">异步处理器</param>
    /// <returns>是否成功</returns>
    public static Boolean Unsubscribe<TEvent>(Func<TEvent, IEventContext, Task> handler) => Default.Unsubscribe(handler);

    /// <summary>取消订阅命名事件</summary>
    /// <param name="name">事件名</param>
    /// <param name="handler">同步处理器</param>
    /// <returns>是否成功</returns>
    public static Boolean Unsubscribe(String name, Action<IEventContext> handler) => Default.Unsubscribe(name, handler);

    /// <summary>取消订阅命名事件</summary>
    /// <param name="name">事件名</param>
    /// <param name="handler">异步处理器</param>
    /// <returns>是否成功</returns>
    public static Boolean Unsubscribe(String name, Func<IEventContext, Task> handler) => Default.Unsubscribe(name, handler);

    /// <summary>发布指定类型事件</summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="event">事件对象</param>
    /// <param name="context">事件上下文</param>
    /// <returns>命中处理器数量</returns>
    public static Int32 Publish<TEvent>(TEvent @event, IEventContext? context = null) => Default.Publish(@event, context);

    /// <summary>异步发布指定类型事件</summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="event">事件对象</param>
    /// <param name="context">事件上下文</param>
    /// <returns>命中处理器数量</returns>
    public static Task<Int32> PublishAsync<TEvent>(TEvent @event, IEventContext? context = null) => Default.PublishAsync(@event, context);

    /// <summary>发布命名事件</summary>
    /// <param name="name">事件名</param>
    /// <param name="event">事件对象</param>
    /// <param name="context">事件上下文</param>
    /// <returns>命中处理器数量</returns>
    public static Int32 Publish(String name, Object? @event = null, IEventContext? context = null) => Default.Publish(name, @event, context);

    /// <summary>异步发布命名事件</summary>
    /// <param name="name">事件名</param>
    /// <param name="event">事件对象</param>
    /// <param name="context">事件上下文</param>
    /// <returns>命中处理器数量</returns>
    public static Task<Int32> PublishAsync(String name, Object? @event = null, IEventContext? context = null) => Default.PublishAsync(name, @event, context);
}