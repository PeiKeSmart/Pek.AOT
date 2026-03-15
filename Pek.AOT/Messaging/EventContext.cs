using Pek.Collections;

namespace Pek.Messaging;

/// <summary>默认事件上下文</summary>
public class EventContext : IEventContext
{
    private static readonly Pool<EventContext> _pool = new(factory: static () => new EventContext());
    private readonly NullableDictionary<String, Object?> _items = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>事件名</summary>
    public String Name { get; set; } = String.Empty;

    /// <summary>事件源</summary>
    public Object? Sender { get; set; }

    /// <summary>事件对象</summary>
    public Object? Event { get; set; }

    /// <summary>处理期间异常</summary>
    public Exception? Exception { get; set; }

    /// <summary>是否已处理</summary>
    public Boolean Handled { get; set; }

    /// <summary>取消标记</summary>
    public CancellationToken CancellationToken { get; set; }

    /// <summary>扩展数据</summary>
    public IDictionary<String, Object?> Items => _items;

    /// <summary>获取或设置扩展数据</summary>
    /// <param name="key">键</param>
    /// <returns>值</returns>
    public Object? this[String key]
    {
        get => _items[key];
        set => _items[key] = value;
    }

    /// <summary>借出上下文</summary>
    /// <returns>上下文实例</returns>
    public static EventContext Rent() => _pool.Get();

    /// <summary>归还上下文</summary>
    /// <param name="context">上下文实例</param>
    public static void Return(EventContext? context)
    {
        if (context == null) return;

        context.Reset();
        _pool.Return(context);
    }

    /// <summary>重置状态</summary>
    public virtual void Reset()
    {
        Name = String.Empty;
        Sender = null;
        Event = null;
        Exception = null;
        Handled = false;
        CancellationToken = default;
        _items.Clear();
    }
}