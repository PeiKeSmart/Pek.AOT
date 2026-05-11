using Pek;
using Pek.Data;
using Pek.Extension;
using Pek.Messaging;
using Pek.Model;
using Pek.Serialization;

namespace Pek.Net.Handlers;

/// <summary>标准网络封包。头部4字节定长</summary>
public class StandardCodec : MessageCodec<IMessage>
{
    private Int32 _gid;

    /// <summary>写入数据</summary>
    /// <param name="context">处理器上下文</param>
    /// <param name="message">消息</param>
    /// <returns>处理后的消息</returns>
    public override Object? Write(IHandlerContext context, Object message)
    {
        DataKinds? kind = null;
        var origin = message;

        var type = message.GetType();
        if (IsBaseType(type))
        {
            kind = DataKinds.String;
            message = (ArrayPacket)(message + String.Empty).GetBytes();
        }
        else if (message is Byte[] buffer)
        {
            message = new ArrayPacket(buffer);
        }
        else if (message is ISpanSerializable spanSerializable)
        {
            message = spanSerializable.ToPacket();
        }
        else if (message is IAccessor accessor)
        {
            message = accessor.ToPacket();
        }

        if (message is IPacket packet)
        {
            var request = GetRequest(context);
            var response = request != null && !request.Reply
                ? request.CreateReply() as DefaultMessage ?? DefaultMessage.Rent()
                : DefaultMessage.Rent();

            response.Flag = (Byte)(kind ?? DataKinds.Packet);
            response.Payload = packet;
            message = response;

            if (context is IExtend ext && ext["Flag"] is DataKinds dataKind)
                response.Flag = (Byte)dataKind;
        }

        if (message is DefaultMessage protocolMessage && !protocolMessage.Reply && protocolMessage.Sequence == 0)
            protocolMessage.Sequence = (Byte)Interlocked.Increment(ref _gid);

        try
        {
            return base.Write(context, message);
        }
        finally
        {
            if (!ReferenceEquals(message, origin)) message.TryDispose();
        }
    }

    /// <summary>加入队列</summary>
    /// <param name="context">处理器上下文</param>
    /// <param name="message">消息</param>
    protected override void AddToQueue(IHandlerContext context, IMessage message)
    {
        if (!message.Reply) base.AddToQueue(context, message);
    }

    /// <summary>解码</summary>
    /// <param name="context">处理器上下文</param>
    /// <param name="packet">数据包</param>
    /// <returns>解码后的消息列表</returns>
    protected override IEnumerable<IMessage>? Decode(IHandlerContext context, IPacket packet)
    {
        if (context.Owner is not IExtend extend) yield break;

        if (extend["Codec"] is not PacketCodec codec)
        {
#pragma warning disable CS0618
            extend["Codec"] = codec = new PacketCodec
            {
                GetLength = DefaultMessage.GetLength,
                GetLength2 = DefaultMessage.GetLength,
                MaxCache = MaxCache,
                Tracer = (context.Owner as ISocket)?.Tracer
            };
#pragma warning restore CS0618
        }

        var packets = codec.Parse(packet);
        foreach (var item in packets)
        {
            var message = DefaultMessage.Rent();
            if (message.Read(item)) yield return message;
        }
    }

    /// <summary>是否匹配响应</summary>
    /// <param name="request">请求消息</param>
    /// <param name="response">响应消息</param>
    /// <returns>是否匹配</returns>
    protected override Boolean IsMatch(Object? request, Object? response) =>
        request is DefaultMessage req &&
        response is DefaultMessage res &&
        req.Sequence == res.Sequence;

    /// <summary>关闭连接时清理编码器缓存</summary>
    /// <param name="context">处理器上下文</param>
    /// <param name="reason">关闭原因</param>
    /// <returns>是否成功关闭</returns>
    public override Boolean Close(IHandlerContext context, String reason)
    {
        if (context.Owner is IExtend extend) extend["Codec"] = null;

        return base.Close(context, reason);
    }

    private static Boolean IsBaseType(Type type)
    {
        if (type.IsEnum || type.IsPrimitive) return true;

        return type == typeof(String) ||
               type == typeof(Decimal) ||
               type == typeof(DateTime) ||
               type == typeof(DateTimeOffset) ||
               type == typeof(TimeSpan) ||
               type == typeof(Guid);
    }
}