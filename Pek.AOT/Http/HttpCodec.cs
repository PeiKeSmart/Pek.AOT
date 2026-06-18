using Pek;
using Pek.Data;
using Pek.Messaging;
using Pek.Model;
using Pek.Net;

namespace Pek.Http;

/// <summary>Http编解码器</summary>
public class HttpCodec : Handler
{
    private const String EncoderKey = "Encoder";
    private const String MessageKey = "Message";

    /// <summary>允许分析头部。默认false</summary>
    /// <remarks>分析头部对性能有一定损耗。</remarks>
    public Boolean AllowParseHeader { get; set; }

    /// <summary>写入数据</summary>
    /// <param name="context">处理器上下文</param>
    /// <param name="message">消息</param>
    /// <returns>处理后的消息</returns>
    public override Object? Write(IHandlerContext context, Object message)
    {
        if (context.Owner is ISocket sock && sock.Local.Type != NetType.Tcp)
            return base.Write(context, message);

        if (message is HttpMessage http) message = http.ToPacket() ?? message;

        return base.Write(context, message);
    }

    /// <summary>读取数据</summary>
    /// <param name="context">处理器上下文</param>
    /// <param name="message">消息</param>
    /// <returns>处理后的消息</returns>
    public override Object? Read(IHandlerContext context, Object message)
    {
        if (context.Owner is ISocket sock && sock.Local.Type != NetType.Tcp)
            return base.Read(context, message);

        if (message is not IPacket packet) return base.Read(context, message);
        if (context.Owner is not IExtend ext) return base.Read(context, message);

        var isGet = packet.Total >= 4 && packet[0] == 'G' && packet[1] == 'E' && packet[2] == 'T' && packet[3] == ' ';
        var isPost = packet.Total >= 5 && packet[0] == 'P' && packet[1] == 'O' && packet[2] == 'S' && packet[3] == 'T' && packet[4] == ' ';

        if (ext[EncoderKey] is not HttpEncoder)
        {
            if (!isGet && !isPost) return base.Read(context, message);

            ext[EncoderKey] = new HttpEncoder();
        }

        if (ext[MessageKey] is HttpMessage pending)
        {
            if (pending.Payload == null)
                pending.Payload = packet.Clone();
            else
                pending.Payload.Append(packet.Clone());

            if (pending.ContentLength == 0 || pending.ContentLength > 0 && pending.Payload != null && pending.Payload.Total >= pending.ContentLength)
            {
                ext[MessageKey] = null;
                return base.Read(context, pending);
            }

            return null;
        }

        var current = new HttpMessage();
        if (!current.Read(packet)) throw new XException("Http请求头不完整");
        if (AllowParseHeader && !current.ParseHeaders()) throw new XException("Http头部解码失败");

        if (isGet) return base.Read(context, current);

        if (current.ContentLength == 0 || current.ContentLength > 0 && current.Payload != null && current.Payload.Total >= current.ContentLength)
            return base.Read(context, current);

        if (current.Header != null) current.Header = current.Header.Clone();
        if (current.Payload != null)
        {
            if (current.Payload.Total > 0)
                current.Payload = current.Payload.Clone();
            else
                current.Payload = null;
        }

        ext[MessageKey] = current;
        return null;
    }
}

/// <summary>Http消息</summary>
public class HttpMessage : Message<HttpMessage>
{
    private static readonly Byte[] NewLine = [(Byte)'\r', (Byte)'\n', (Byte)'\r', (Byte)'\n'];

    /// <summary>头部数据</summary>
    public IPacket? Header { get; set; }

    /// <summary>请求方法</summary>
    public String Method { get; set; } = String.Empty;

    /// <summary>请求资源</summary>
    public String Uri { get; set; } = String.Empty;

    /// <summary>内容长度</summary>
    public Int32 ContentLength { get; set; } = -1;

    /// <summary>头部集合</summary>
    public IDictionary<String, String>? Headers { get; set; }

    /// <summary>根据请求创建配对的响应消息</summary>
    /// <returns>响应消息</returns>
    public override IMessage CreateReply()
    {
        if (Reply) throw new InvalidOperationException("不能根据响应消息创建响应消息");

        var msg = new HttpMessage
        {
            Reply = true
        };

        return msg;
    }

    /// <summary>释放资源</summary>
    /// <param name="disposing">是否释放托管资源</param>
    protected override void Dispose(Boolean disposing)
    {
        if (disposing)
        {
            Header.TryDispose();
            Header = null;
        }

        base.Dispose(disposing);
    }

    /// <summary>从数据包中读取消息</summary>
    /// <param name="packet">原始数据包</param>
    /// <returns>是否成功</returns>
    public override Boolean Read(IPacket packet)
    {
        var position = packet.GetSpan().IndexOf(NewLine);
        if (position < 0) return false;

        Header = packet.Slice(0, position);
        Payload = packet.Slice(position + NewLine.Length);
        return true;
    }

    /// <summary>解码头部</summary>
    /// <returns>是否成功</returns>
    public virtual Boolean ParseHeaders()
    {
        var header = Header;
        if (header == null || header.Total == 0) return false;

        var dictionary = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
        var sections = header.ToStr().Split("\r\n");
        if (sections.Length <= 0) return false;

        var firstLine = sections[0].Split(' ');
        if (firstLine.Length >= 3)
        {
            Method = firstLine[0].Trim();
            Uri = firstLine[1].Trim();
        }

        for (var i = 1; i < sections.Length; i++)
        {
            var separator = sections[i].IndexOf(':');
            if (separator <= 0) continue;

            var key = sections[i][..separator].Trim();
            var value = sections[i][(separator + 1)..].Trim();
            dictionary[key] = value;
        }

        Headers = dictionary;
        if (dictionary.TryGetValue("Content-Length", out var contentLength)) ContentLength = contentLength.ToInt();

        return true;
    }

    /// <summary>把消息转为数据包</summary>
    /// <returns>序列化后的数据包</returns>
    public override IPacket? ToPacket()
    {
        var header = Header;
        if (header == null) return Payload;

        var packet = header.Slice(0, -1);
        packet.Next = new ArrayPacket(NewLine);

        var payload = Payload;
        if (payload != null && payload.Total > 0) packet.Append(payload);

        return packet;
    }
}