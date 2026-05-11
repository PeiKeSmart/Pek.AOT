using Pek.Data;
using Pek.Extension;
using Pek.Messaging;
using Pek.Model;
using Pek.Serialization;

namespace Pek.Net.Handlers;

/// <summary>Json编码器。用于把用户对象编码为Json字符串</summary>
public class JsonCodec : Handler
{
    /// <summary>Json序列化主机</summary>
    public IJsonHost JsonHost { get; set; } = JsonHelper.Default;

    /// <summary>JSON序列化选项</summary>
    public JsonOptions? JsonOptions { get; set; }

    /// <summary>发送消息时编码对象</summary>
    /// <param name="context">处理器上下文</param>
    /// <param name="message">消息</param>
    /// <returns>处理后的消息</returns>
    public override Object? Write(IHandlerContext context, Object message)
    {
        var ext = context as IExtend;
        var type = message.GetType();
        if (IsBaseType(type))
        {
            var text = message is DateTime dateTime ? dateTime.ToFullString() : message + String.Empty;
            message = text.GetBytes();
            ext?["Flag"] = DataKinds.String;
        }
        else if (message is not IPacket and not IMessage)
        {
            message = JsonHost.Write(message, JsonOptions ?? JsonHost.Options).GetBytes();
            ext?["Flag"] = DataKinds.Json;
        }

        if (message is Byte[] buffer) message = new ArrayPacket(buffer);

        return base.Write(context, message);
    }

    /// <summary>接收数据后解码Json</summary>
    /// <param name="context">处理器上下文</param>
    /// <param name="message">消息</param>
    /// <returns>处理后的消息</returns>
    public override Object? Read(IHandlerContext context, Object message)
    {
        if (message is IPacket packet)
        {
            var text = packet.ToStr();
            if (!text.IsNullOrEmpty()) message = JsonHost.Parse(text)!;
        }

        return base.Read(context, message);
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