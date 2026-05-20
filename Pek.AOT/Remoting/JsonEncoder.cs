using System.IO;
using System.Text;

using Pek.Data;
using Pek.Extension;
using Pek.Messaging;
using Pek.Serialization;

namespace Pek.Remoting;

/// <summary>Json编码器</summary>
public class JsonEncoder : EncoderBase, IEncoder
{
    /// <summary>编码。请求/响应</summary>
    /// <param name="action">动作</param>
    /// <param name="code">错误码</param>
    /// <param name="value">参数或结果</param>
    /// <returns>编码后的数据包</returns>
    public virtual IPacket Encode(String action, Int32 code, IPacket? value)
    {
        using var stream = new MemoryStream();
        stream.Position = 8;

        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write(action);
            if (code != 0) writer.Write(code);
            if (value != null) writer.Write(value.Total);
        }

        return new ArrayPacket(stream.GetBuffer(), 8, (Int32)stream.Length - 8) { Next = value };
    }

    /// <summary>解码参数</summary>
    /// <param name="action">动作</param>
    /// <param name="data">数据</param>
    /// <param name="msg">消息</param>
    /// <returns>参数字典</returns>
    public virtual IDictionary<String, Object> DecodeParameters(String action, IPacket data, IMessage msg)
    {
        var json = data.ToStr();
        WriteLog("{0}[{2:X2}]<={1}", action, json, msg is DefaultMessage defaultMessage ? defaultMessage.Sequence : 0);

        var values = JsonHelper.Default.Decode(json);
        if (values == null || values.Count == 0) return new Dictionary<String, Object>(StringComparer.OrdinalIgnoreCase);

        var result = new Dictionary<String, Object>(values.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var item in values)
        {
            result[item.Key] = item.Value!;
        }

        return result;
    }

    /// <summary>解码结果</summary>
    /// <param name="action">动作</param>
    /// <param name="data">数据</param>
    /// <param name="msg">消息</param>
    /// <returns>结果对象</returns>
    public virtual Object? DecodeResult(String action, IPacket data, IMessage msg)
    {
        var json = data.ToStr();
        WriteLog("{0}[{2:X2}]<={1}", action, json, msg is DefaultMessage defaultMessage ? defaultMessage.Sequence : 0);

        return JsonHelper.Default.Parse(json);
    }

    /// <summary>转换为目标类型</summary>
    /// <param name="obj">源对象</param>
    /// <param name="targetType">目标类型</param>
    /// <returns>转换结果</returns>
    public virtual Object? Convert(Object? obj, Type targetType)
    {
        if (obj == null) return null;

        return JsonHelper.Default.Convert(obj, targetType);
    }

    /// <summary>创建请求</summary>
    /// <param name="action">动作</param>
    /// <param name="args">参数</param>
    /// <returns>请求消息</returns>
    public virtual IMessage CreateRequest(String action, Object? args)
    {
        var payload = EncodeValue(args, out var text);
        if (Log != null && text.IsNullOrEmpty() && payload != null) text = $"[{payload.Total}]";

        WriteLog("{0}=>{1}", action, text);

        var body = Encode(action, 0, payload);
        return new DefaultMessage { Payload = body };
    }

    /// <summary>创建响应</summary>
    /// <param name="msg">请求消息</param>
    /// <param name="action">动作</param>
    /// <param name="code">错误码</param>
    /// <param name="value">结果</param>
    /// <returns>响应消息</returns>
    public virtual IMessage CreateResponse(IMessage msg, String action, Int32 code, Object? value)
    {
        var payload = EncodeValue(value, out var text);
        if (Log != null && text.IsNullOrEmpty() && payload != null) text = $"[{payload.Total}]";

        WriteLog("{0}[{2:X2}]=>{1}", action, text, msg is DefaultMessage defaultMessage ? defaultMessage.Sequence : 0);

        var body = Encode(action, code, payload);
        var response = msg.CreateReply();
        response.Payload = body;
        if (code > 0) response.Error = true;

        return response;
    }

    /// <summary>将参数或结果编码为数据包</summary>
    /// <param name="value">源对象</param>
    /// <param name="text">日志文本</param>
    /// <returns>数据包</returns>
    protected virtual IPacket? EncodeValue(Object? value, out String text)
    {
        text = String.Empty;

        if (value is IPacket packet) return packet;
        if (value is ISpanSerializable spanSerializable) return spanSerializable.ToPacket();
        if (value is IAccessor accessor) return accessor.ToPacket();
        if (value is Byte[] buffer) return new ArrayPacket(buffer);

        if (value is String stringValue)
        {
            text = stringValue;
            return new ArrayPacket(stringValue.GetBytes());
        }

        if (value is Exception exception) value = exception.GetTrue()?.Message;
        if (value == null) return null;

        text = JsonHelper.Default.Write(value, false, false, false);
        return new ArrayPacket(text.GetBytes());
    }
}