using Pek;
using Pek.Buffers;
using Pek.Collections;
using Pek.Data;
using Pek.Extension;
using Pek.Messaging;
using Pek.Remoting;
using Pek.Serialization;

namespace Pek.Http;

/// <summary>Http编码器</summary>
public class HttpEncoder : EncoderBase, IEncoder
{
    /// <summary>是否使用Http状态。默认false，使用json包装响应码</summary>
    public Boolean UseHttpStatus { get; set; }

    /// <summary>编码</summary>
    /// <param name="action">动作</param>
    /// <param name="code">错误码</param>
    /// <param name="value">数据</param>
    /// <returns>数据包</returns>
    public virtual IPacket Encode(String action, Int32 code, Object? value)
    {
        if (value is IPacket packet) return packet;
        if (value is ISpanSerializable spanSerializable) return spanSerializable.ToPacket();
        if (value is IAccessor accessor) return accessor.ToPacket();

        if (value is Exception exception) value = exception.GetTrue()?.Message;

        String json;
        if (UseHttpStatus)
        {
            json = (value ?? String.Empty).ToJson(false, false, false);
        }
        else
        {
            var envelope = new Dictionary<String, Object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["action"] = action,
                ["code"] = code,
                ["data"] = value,
            };
            json = envelope.ToJson(false, true, false);
        }

        WriteLog("{0}=>{1}", action, json);
        return json.GetBytes().AsPacket();
    }

    /// <summary>解码参数</summary>
    /// <param name="action">动作</param>
    /// <param name="data">数据</param>
    /// <param name="msg">消息</param>
    /// <returns>参数字典</returns>
    public virtual IDictionary<String, Object> DecodeParameters(String action, IPacket data, IMessage msg)
    {
        var text = data.ToStr();
        WriteLog("{0}<={1}", action, text);
        if (text.IsNullOrEmpty()) return new Dictionary<String, Object>(StringComparer.OrdinalIgnoreCase);

        var contentTypes = Array.Empty<String>();
        if (msg is HttpMessage httpMessage && text[0] == '{' && httpMessage.ParseHeaders() && httpMessage.Headers != null)
        {
            if (httpMessage.Headers.TryGetValue("Content-Type", out var contentType) || httpMessage.Headers.TryGetValue("Content-type", out contentType))
                contentTypes = (contentType + String.Empty).Split(';');
        }

        if (contentTypes.Any(e => e.EqualIgnoreCase("application/json")))
        {
            var dictionary = text.DecodeJson();
            var result = new Dictionary<String, Object>(StringComparer.OrdinalIgnoreCase);
            if (dictionary == null) return result;

            foreach (var item in dictionary)
            {
                if (item.Value is String value)
                    result[item.Key] = UrlDecode(value);
                else if (item.Value != null)
                    result[item.Key] = item.Value;
            }

            return result;
        }

        var queryValues = text.SplitAsDictionary("=", "&");
        var queryResult = new Dictionary<String, Object>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in queryValues)
        {
            queryResult[item.Key] = UrlDecode(item.Value);
        }

        return queryResult;
    }

    /// <summary>解码结果</summary>
    /// <param name="action">动作</param>
    /// <param name="data">数据</param>
    /// <param name="msg">消息</param>
    /// <returns>结果</returns>
    public virtual Object? DecodeResult(String action, IPacket data, IMessage msg)
    {
        var json = data.ToStr();
        WriteLog("{0}<={1}", action, json);

        return JsonHelper.Parse(json);
    }

    /// <summary>转换为目标类型</summary>
    /// <param name="obj">对象</param>
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
        var request = new HttpMessage();
        var builder = Pool.StringBuilder.Get();
        builder.Append("GET ");
        builder.Append(action);

        var payload = PreparePayload(args, builder);
        builder.AppendLine(" HTTP/1.1");

        if (payload != null && payload.Total > 0)
        {
            builder.AppendFormat("Content-Length:{0}\r\n", payload.Total);
            builder.AppendLine("Content-Type:application/json");
        }

        builder.Append("Connection:keep-alive");

        request.Header = builder.Return(true).GetBytes().AsPacket();
        request.Payload = payload;
        return request;
    }

    /// <summary>创建响应</summary>
    /// <param name="msg">请求消息</param>
    /// <param name="action">动作</param>
    /// <param name="code">错误码</param>
    /// <param name="value">结果</param>
    /// <returns>响应消息</returns>
    public virtual IMessage CreateResponse(IMessage msg, String action, Int32 code, Object? value)
    {
        if (code <= 0 && UseHttpStatus) code = 200;

        var payload = Encode(action, code, value);
        var response = new HttpMessage { Payload = payload };
        if (code >= 500) response.Error = true;

        var builder = Pool.StringBuilder.Get();
        builder.Append("HTTP/1.1 ");

        if (UseHttpStatus)
        {
            builder.Append(code);
            if (code < 500)
                builder.AppendLine(" OK");
            else
                builder.AppendLine(" Error");
        }
        else
        {
            builder.AppendLine("200 OK");
        }

        builder.AppendFormat("Content-Length:{0}\r\n", payload.Total);
        builder.AppendLine("Content-Type:application/json");
        builder.Append("Connection:keep-alive");

        response.Header = builder.Return(true).GetBytes().AsPacket();
        return response;
    }

    /// <summary>解码请求或响应</summary>
    /// <param name="msg">消息</param>
    /// <param name="action">动作</param>
    /// <param name="code">错误码</param>
    /// <param name="value">参数或结果</param>
    /// <returns>是否成功</returns>
    public override Boolean Decode(IMessage msg, out String action, out Int32 code, out IPacket? value)
    {
        action = String.Empty;
        code = 0;
        value = null;

        if (msg is not HttpMessage httpMessage) return false;

        var header = httpMessage.Header;
        if (header == null) return false;

        var span = header.GetSpan();
        var position = span.IndexOf([(Byte)'\r', (Byte)'\n']);
        if (position <= 0) return false;

        var line = span[..position].ToStr();
        var sections = line.Split(' ');
        if (sections.Length < 3) return false;

        var url = sections[1];
        position = url.IndexOf('?');
        if (position > 0)
        {
            action = url[1..position];
            value = url[(position + 1)..].GetBytes().AsPacket();
        }
        else
        {
            action = url[1..];
            value = httpMessage.Payload;
        }

        return true;
    }

    private static IPacket? PreparePayload(Object? args, System.Text.StringBuilder builder)
    {
        if (args == null) return null;
        if (args is IPacket packet) return packet;
        if (args is ISpanSerializable spanSerializable) return spanSerializable.ToPacket();
        if (args is IAccessor accessor) return accessor.ToPacket();
        if (args is Byte[] buffer) return buffer.AsPacket();

        var type = args.GetType();
        if (Type.GetTypeCode(type) != TypeCode.Object)
        {
            builder.Append('?');
            builder.Append(args);
            return null;
        }

        var values = GetValues(args);
        if (values != null)
        {
            builder.Append('?');
            var first = true;
            foreach (var item in values)
            {
                if (!first) builder.Append('&');
                builder.Append(item.Key);
                builder.Append('=');
                builder.Append(item.Value);
                first = false;
            }
            return null;
        }

        return args.ToJson(false, false, false).GetBytes().AsPacket();
    }

    private static IEnumerable<KeyValuePair<String, Object?>>? GetValues(Object args)
    {
        if (args is IDictionarySource source) return source.ToDictionary();
        if (args is IDictionary<String, Object?> dictionary) return dictionary;
        if (args is IDictionary<String, String> stringDictionary) return stringDictionary.Select(e => new KeyValuePair<String, Object?>(e.Key, e.Value));
        if (args is IEnumerable<KeyValuePair<String, Object?>> pairs) return pairs;

        var json = args.ToJson(false, false, false);
        var decoded = json.DecodeJson();
        return decoded;
    }

    private static String UrlDecode(String? value)
    {
        if (value.IsNullOrEmpty()) return String.Empty;

        value = value.Replace('+', ' ');
        return Uri.UnescapeDataString(value);
    }
}