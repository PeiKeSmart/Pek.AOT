using Pek.Data;
using System.Collections.Concurrent;

namespace Pek.Messaging;

/// <summary>消息接口</summary>
public interface IMessage : IDisposable
{
    /// <summary>是否响应</summary>
    Boolean Reply { get; set; }

    /// <summary>是否有错</summary>
    Boolean Error { get; set; }

    /// <summary>是否单向</summary>
    Boolean OneWay { get; set; }

    /// <summary>消息负载</summary>
    IPacket? Payload { get; set; }

    /// <summary>根据请求创建响应消息</summary>
    /// <returns>响应消息</returns>
    IMessage CreateReply();

    /// <summary>从数据包中读取消息</summary>
    /// <param name="packet">原始数据包</param>
    /// <returns>是否成功解析</returns>
    Boolean Read(IPacket packet);

    /// <summary>把消息转为数据包</summary>
    /// <returns>序列化后的数据包</returns>
    IPacket? ToPacket();
}

/// <summary>消息基类</summary>
public class Message : IMessage
{
    private static readonly ConcurrentDictionary<Type, Func<Message>> _messageFactories = new();

    /// <summary>是否响应</summary>
    public Boolean Reply { get; set; }

    /// <summary>是否有错</summary>
    public Boolean Error { get; set; }

    /// <summary>是否单向</summary>
    public Boolean OneWay { get; set; }

    /// <summary>消息负载</summary>
    public IPacket? Payload { get; set; }

    /// <summary>释放资源</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>释放资源</summary>
    /// <param name="disposing">是否释放托管资源</param>
    protected virtual void Dispose(Boolean disposing)
    {
        if (!disposing) return;

        Payload.TryDispose();
        Payload = null;
    }

    /// <summary>根据请求创建响应消息</summary>
    /// <returns>响应消息</returns>
    public virtual IMessage CreateReply()
    {
        if (Reply) throw new InvalidOperationException("Cannot create response message based on response message");

        var message = CreateInstance();
        message.Reply = true;

        return message;
    }

    /// <summary>注册消息工厂</summary>
    /// <typeparam name="TMessage">消息类型</typeparam>
    public static void Register<TMessage>() where TMessage : Message, new() => Register(() => new TMessage());

    /// <summary>注册消息工厂</summary>
    /// <typeparam name="TMessage">消息类型</typeparam>
    /// <param name="factory">消息工厂</param>
    public static void Register<TMessage>(Func<TMessage> factory) where TMessage : Message
    {
        if (factory == null) throw new ArgumentNullException(nameof(factory));

        _messageFactories[typeof(TMessage)] = () => factory();
    }

    /// <summary>注销消息工厂</summary>
    /// <typeparam name="TMessage">消息类型</typeparam>
    /// <returns>是否成功移除</returns>
    public static Boolean Unregister<TMessage>() where TMessage : Message => _messageFactories.TryRemove(typeof(TMessage), out _);

    /// <summary>尝试创建指定类型的消息实例</summary>
    /// <param name="type">消息类型</param>
    /// <param name="message">消息实例</param>
    /// <returns>是否成功</returns>
    protected static Boolean TryCreate(Type type, out Message? message)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));

        if (_messageFactories.TryGetValue(type, out var factory))
        {
            message = factory();
            return true;
        }

        message = null;
        return false;
    }

    /// <summary>创建当前类型的新实例</summary>
    /// <returns>新的消息实例</returns>
    protected virtual Message CreateInstance()
    {
        if (GetType() == typeof(Message)) return new Message();

        if (TryCreate(GetType(), out var message) && message != null) return message;

        throw new NotSupportedException($"Type [{GetType().FullName}] must override CreateInstance() or call Message.Register<TMessage>() in AOT mode.");
    }

    /// <summary>从数据包中读取消息</summary>
    /// <param name="packet">原始数据包</param>
    /// <returns>是否成功解析</returns>
    public virtual Boolean Read(IPacket packet)
    {
        Payload = packet;
        return true;
    }

    /// <summary>把消息转为数据包</summary>
    /// <returns>序列化后的数据包</returns>
    public virtual IPacket? ToPacket() => Payload;

    /// <summary>重置消息状态</summary>
    public virtual void Reset()
    {
        Reply = false;
        Error = false;
        OneWay = false;
        Payload = null;
    }
}

/// <summary>支持自动注册工厂的消息基类</summary>
/// <typeparam name="TMessage">消息类型</typeparam>
public abstract class Message<TMessage> : Message where TMessage : Message<TMessage>, new()
{
    static Message() => Register<TMessage>();
}