using Pek.Data;

namespace Pek.Messaging;

/// <summary>事件上下文</summary>
public interface IEventContext : IExtend
{
    /// <summary>事件名</summary>
    String Name { get; set; }

    /// <summary>事件源</summary>
    Object? Sender { get; set; }

    /// <summary>事件对象</summary>
    Object? Event { get; set; }

    /// <summary>处理期间异常</summary>
    Exception? Exception { get; set; }

    /// <summary>是否已处理</summary>
    Boolean Handled { get; set; }

    /// <summary>取消标记</summary>
    CancellationToken CancellationToken { get; set; }
}