using Pek.Collections;
using Pek.Data;

namespace Pek.Messaging;

/// <summary>消息工厂接口</summary>
/// <typeparam name="TMessage">消息类型</typeparam>
public interface IMessageFactory<TMessage> where TMessage : IMessage
{
    /// <summary>创建或获取消息实例</summary>
    /// <returns>消息实例</returns>
    TMessage Create();

    /// <summary>从数据包解析消息</summary>
    /// <param name="packet">数据包</param>
    /// <returns>解析后的消息，解析失败返回空</returns>
    TMessage? Parse(IPacket packet);

    /// <summary>回收消息实例到对象池</summary>
    /// <param name="message">待回收的消息</param>
    void Return(TMessage message);
}

/// <summary>默认消息工厂。支持对象池化</summary>
/// <typeparam name="TMessage">消息类型，必须有无参构造函数</typeparam>
public class DefaultMessageFactory<TMessage> : IMessageFactory<TMessage> where TMessage : Message, new()
{
    private readonly Pool<TMessage> _pool = new();

    /// <summary>是否启用池化。默认true</summary>
    public Boolean EnablePooling { get; set; } = true;

    /// <summary>对象池最大容量。默认256</summary>
    public Int32 MaxPoolSize
    {
        get => _pool.Max;
        set => _pool.Max = value;
    }

    /// <summary>创建或获取消息实例</summary>
    /// <returns>消息实例</returns>
    public virtual TMessage Create()
    {
        if (!EnablePooling) return new TMessage();

        return _pool.Get();
    }

    /// <summary>从数据包解析消息</summary>
    /// <param name="packet">数据包</param>
    /// <returns>解析后的消息，解析失败返回空</returns>
    public virtual TMessage? Parse(IPacket packet)
    {
        if (packet == null || packet.Total == 0) return default;

        var message = Create();
        try
        {
            if (message.Read(packet)) return message;

            Return(message);
            return default;
        }
        catch
        {
            Return(message);
            throw;
        }
    }

    /// <summary>回收消息实例到对象池</summary>
    /// <param name="message">待回收的消息</param>
    public virtual void Return(TMessage message)
    {
        if (message == null || !EnablePooling) return;

        message.Reset();
        _pool.Return(message);
    }
}

/// <summary>DefaultMessage 专用工厂</summary>
public class DefaultMessageFactory : DefaultMessageFactory<DefaultMessage>
{
    /// <summary>默认实例</summary>
    public static DefaultMessageFactory Instance { get; } = new();

    /// <summary>从数据包解析消息</summary>
    /// <param name="packet">数据包</param>
    /// <returns>解析后的消息，解析失败返回空</returns>
    public static DefaultMessage? ParseMessage(IPacket packet) => Instance.Parse(packet);
}