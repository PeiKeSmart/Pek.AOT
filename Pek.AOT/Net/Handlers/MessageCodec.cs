using Pek;
using Pek.Buffers;
using Pek.Data;
using Pek.Messaging;
using Pek.Model;

namespace Pek.Net.Handlers;

/// <summary>消息封包编码器</summary>
/// <remarks>
/// 该编码器为基于请求响应模型的协议提供匹配队列，能够根据响应匹配等待中的请求。
/// </remarks>
public class MessageCodec<T> : Handler
{
    /// <summary>消息队列。用于匹配请求响应包</summary>
    public IMatchQueue? Queue { get; set; }

    /// <summary>匹配队列大小</summary>
    public Int32 QueueSize { get; set; } = 256;

    /// <summary>请求消息在匹配队列中等待响应的超时时间，默认30秒</summary>
    public Int32 Timeout { get; set; } = 30_000;

    /// <summary>最大缓存待处理数据，默认10M</summary>
    public Int32 MaxCache { get; set; } = 10 * 1024 * 1024;

    /// <summary>用户数据包。写入时数据包转消息，读取时消息自动解包返回数据负载</summary>
    public Boolean UserPacket { get; set; } = true;

    /// <summary>打开链接</summary>
    /// <param name="context">处理器上下文</param>
    /// <returns>是否成功打开</returns>
    public override Boolean Open(IHandlerContext context)
    {
        if (context.Owner is ISocketClient client) Timeout = client.Timeout;

        return base.Open(context);
    }

    /// <summary>发送消息时，编码并加入匹配队列</summary>
    /// <param name="context">处理器上下文</param>
    /// <param name="message">消息</param>
    /// <returns>处理后的消息</returns>
    public override Object? Write(IHandlerContext context, Object message)
    {
        IPacket? owner = null;
        if (message is T msg)
        {
            var result = Encode(context, msg);
            if (result == null) return null;

            message = result;
            owner = result as IPacket;

            if (message is IMessage protocolMessage)
            {
                if (!protocolMessage.Reply) AddToQueue(context, msg);
            }
            else
                AddToQueue(context, msg);
        }

        try
        {
            return base.Write(context, message);
        }
        finally
        {
            owner.TryDispose();
        }
    }

    /// <summary>编码消息</summary>
    /// <param name="context">处理器上下文</param>
    /// <param name="msg">消息</param>
    /// <returns>编码后的对象</returns>
    protected virtual Object? Encode(IHandlerContext context, T msg)
    {
        if (msg is IMessage message) return message.ToPacket();

        return null;
    }

    /// <summary>把请求加入队列，等待响应到来时建立请求响应匹配</summary>
    /// <param name="context">处理器上下文</param>
    /// <param name="msg">消息</param>
    protected virtual void AddToQueue(IHandlerContext context, T msg)
    {
        if (msg == null || context is not IExtend ext) return;

        var source = ext["TaskSource"];
        if (source == null) return;

        Queue ??= new DefaultMatchQueue(QueueSize);
        Queue.Add(context.Owner, msg, Timeout, source);
    }

    /// <summary>连接关闭时，清空匹配队列</summary>
    /// <param name="context">处理器上下文</param>
    /// <param name="reason">关闭原因</param>
    /// <returns>是否成功关闭</returns>
    public override Boolean Close(IHandlerContext context, String reason)
    {
        Queue?.Clear();

        return base.Close(context, reason);
    }

    /// <summary>接收数据后，解码得到消息</summary>
    /// <param name="context">处理器上下文</param>
    /// <param name="message">消息</param>
    /// <returns>处理后的消息</returns>
    public override Object? Read(IHandlerContext context, Object message)
    {
        if (message is not IPacket packet) return base.Read(context, message);

        var list = Decode(context, packet);
        if (list == null) return null;

        var queue = Queue;
        var userPacket = UserPacket;

        foreach (var item in list)
        {
            if (item == null) continue;

            Object? result;
            if (item is IMessage protocolMessage)
            {
                if (context is IExtend ext) ext["_raw_message"] = protocolMessage;

                result = userPacket ? protocolMessage.Payload : item;

                if (queue != null && protocolMessage.Reply)
                    MatchResponse(queue, context.Owner, protocolMessage, userPacket);
            }
            else
            {
                result = item;
                queue?.Match(context.Owner, item, result, IsMatch);
            }

            if (result != null) base.Read(context, result);

            if (item is DefaultMessage defaultMessage && userPacket)
                DefaultMessage.Return(defaultMessage);
        }

        return null;
    }

    private void MatchResponse(IMatchQueue queue, Object? owner, IMessage message, Boolean userPacket)
    {
        Object result;
        if (userPacket)
        {
            result = message.Payload!.Clone();
        }
        else
        {
            if (message.Payload != null) message.Payload = message.Payload.Clone();
            result = message;
        }

        queue.Match(owner, message, result, IsMatch);
    }

    /// <summary>从上下文中获取原始请求</summary>
    /// <param name="context">处理器上下文</param>
    /// <returns>原始消息</returns>
    protected IMessage? GetRequest(IHandlerContext context)
    {
        if (context is IExtend ext) return ext["_raw_message"] as IMessage;

        return null;
    }

    /// <summary>解码</summary>
    /// <param name="context">处理器上下文</param>
    /// <param name="packet">数据包</param>
    /// <returns>解码后的消息列表</returns>
    protected virtual IEnumerable<T>? Decode(IHandlerContext context, IPacket packet) => null;

    /// <summary>是否匹配响应</summary>
    /// <param name="request">请求消息</param>
    /// <param name="response">响应消息</param>
    /// <returns>是否匹配</returns>
    protected virtual Boolean IsMatch(Object? request, Object? response) => true;

    /// <summary>从数据流中获取整帧数据长度</summary>
    /// <param name="packet">数据包</param>
    /// <param name="offset">长度的偏移量</param>
    /// <param name="size">长度大小。0变长，1/2/4小端字节，-2/-4大端字节</param>
    /// <returns>数据帧长度</returns>
    public static Int32 GetLength(IPacket packet, Int32 offset, Int32 size) => GetLength(packet.GetSpan(), offset, size);

    /// <summary>从数据流中获取整帧数据长度</summary>
    /// <param name="span">数据片段</param>
    /// <param name="offset">长度的偏移量</param>
    /// <param name="size">长度大小。0变长，1/2/4小端字节，-2/-4大端字节</param>
    /// <returns>数据帧长度</returns>
    public static Int32 GetLength(ReadOnlySpan<Byte> span, Int32 offset, Int32 size)
    {
        if (offset < 0) return span.Length;
        if (offset >= span.Length) return 0;

        var reader = new SpanReader(span) { IsLittleEndian = true };
        reader.Advance(offset);

        var length = 0;
        switch (size)
        {
            case 0:
                var position = reader.Position;
                length = reader.ReadEncodedInt() + reader.Position - position;
                break;
            case 1:
                length = reader.ReadByte();
                break;
            case 2:
                length = reader.ReadUInt16();
                break;
            case 4:
                length = reader.ReadInt32();
                break;
            case -2:
                reader.IsLittleEndian = false;
                length = reader.ReadUInt16();
                break;
            case -4:
                reader.IsLittleEndian = false;
                length = reader.ReadInt32();
                break;
            default:
                throw new NotSupportedException();
        }

        if (length > span.Length) return 0;

        length += Math.Abs(size);
        return offset + length;
    }
}