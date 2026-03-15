using Pek.Data;

namespace Pek.Messaging;

/// <summary>收到消息时的事件参数</summary>
public class MessageEventArgs : EventArgs
{
    /// <summary>数据包</summary>
    public IPacket? Packet { get; set; }

    /// <summary>消息</summary>
    public IMessage? Message { get; set; }

    /// <summary>用户数据</summary>
    public Object? UserState { get; set; }
}