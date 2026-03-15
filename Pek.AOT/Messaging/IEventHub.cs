namespace Pek.Messaging;

/// <summary>事件中心接口</summary>
public interface IEventHub : IDisposable
{
    /// <summary>订阅指定类型事件</summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">同步处理器</param>
    void Subscribe<TEvent>(Action<TEvent, IEventContext> handler);

    /// <summary>订阅指定类型事件</summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">异步处理器</param>
    void Subscribe<TEvent>(Func<TEvent, IEventContext, Task> handler);

    /// <summary>订阅命名事件</summary>
    /// <param name="name">事件名</param>
    /// <param name="handler">同步处理器</param>
    void Subscribe(String name, Action<IEventContext> handler);

    /// <summary>订阅命名事件</summary>
    /// <param name="name">事件名</param>
    /// <param name="handler">异步处理器</param>
    void Subscribe(String name, Func<IEventContext, Task> handler);

    /// <summary>取消订阅指定类型事件</summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">同步处理器</param>
    Boolean Unsubscribe<TEvent>(Action<TEvent, IEventContext> handler);

    /// <summary>取消订阅指定类型事件</summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">异步处理器</param>
    Boolean Unsubscribe<TEvent>(Func<TEvent, IEventContext, Task> handler);

    /// <summary>取消订阅命名事件</summary>
    /// <param name="name">事件名</param>
    /// <param name="handler">同步处理器</param>
    Boolean Unsubscribe(String name, Action<IEventContext> handler);

    /// <summary>取消订阅命名事件</summary>
    /// <param name="name">事件名</param>
    /// <param name="handler">异步处理器</param>
    Boolean Unsubscribe(String name, Func<IEventContext, Task> handler);

    /// <summary>发布指定类型事件</summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="event">事件对象</param>
    /// <param name="context">事件上下文</param>
    /// <returns>命中处理器数量</returns>
    Int32 Publish<TEvent>(TEvent @event, IEventContext? context = null);

    /// <summary>异步发布指定类型事件</summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="event">事件对象</param>
    /// <param name="context">事件上下文</param>
    /// <returns>命中处理器数量</returns>
    Task<Int32> PublishAsync<TEvent>(TEvent @event, IEventContext? context = null);

    /// <summary>发布命名事件</summary>
    /// <param name="name">事件名</param>
    /// <param name="event">事件对象</param>
    /// <param name="context">事件上下文</param>
    /// <returns>命中处理器数量</returns>
    Int32 Publish(String name, Object? @event = null, IEventContext? context = null);

    /// <summary>异步发布命名事件</summary>
    /// <param name="name">事件名</param>
    /// <param name="event">事件对象</param>
    /// <param name="context">事件上下文</param>
    /// <returns>命中处理器数量</returns>
    Task<Int32> PublishAsync(String name, Object? @event = null, IEventContext? context = null);
}