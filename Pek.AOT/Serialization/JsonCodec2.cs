using Pek.Data;
using Pek.Extension;
using Pek.Model;

namespace Pek.Serialization;

/// <summary>Json 编码解码器</summary>
public class JsonCodec2 : Handler
{
    /// <summary>对象转 Json</summary>
    /// <param name="context">处理器上下文</param>
    /// <param name="message">消息</param>
    /// <returns>编码后的消息</returns>
    public override Object? Write(IHandlerContext context, Object message)
    {
        if (message is IDictionary<String, Object> dictionary)
            return new ArrayPacket(dictionary.ToJson().GetBytes());

        if (message is IDictionary<String, Object?> nullableDictionary)
            return new ArrayPacket(nullableDictionary.ToJson().GetBytes());

        return message;
    }

    /// <summary>Json 转对象</summary>
    /// <param name="context">处理器上下文</param>
    /// <param name="message">消息</param>
    /// <returns>解码后的消息</returns>
    public override Object? Read(IHandlerContext context, Object message)
    {
        if (message is IPacket packet) message = packet.ToStr();
        if (message is String text) return JsonHelper.Default.Parse(text);

        return message;
    }
}