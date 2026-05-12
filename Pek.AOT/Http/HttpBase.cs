using System.Text;

using Pek.Buffers;
using Pek.Collections;
using Pek.Data;
using Pek.Extension;

namespace Pek.Http;

/// <summary>Http请求响应基类</summary>
public abstract class HttpBase : IDisposable
{
    #region 属性
    /// <summary>协议版本</summary>
    public String Version { get; set; } = "1.1";

    /// <summary>内容长度</summary>
    public Int32 ContentLength { get; set; } = -1;

    /// <summary>内容类型</summary>
    public String? ContentType { get; set; }

    /// <summary>主体</summary>
    public IPacket? Body { get; set; }

    /// <summary>主体长度</summary>
    public Int32 BodyLength => Body == null ? 0 : Body.Total;

    /// <summary>是否已完整</summary>
    public Boolean IsCompleted => ContentLength < 0 || ContentLength <= BodyLength;

    /// <summary>头部集合</summary>
    public IDictionary<String, String> Headers { get; set; } = new NullableDictionary<String, String>(StringComparer.OrdinalIgnoreCase);

    /// <summary>获取或设置头部</summary>
    /// <param name="key">键名</param>
    /// <returns>值</returns>
    public String this[String key] { get => Headers[key] + String.Empty; set => Headers[key] = value; }
    #endregion

    #region 构造
    /// <summary>释放</summary>
    public void Dispose() => Body.TryDispose();
    #endregion

    #region 解析
    /// <summary>快速验证协议头</summary>
    /// <param name="data">数据</param>
    /// <returns>是否可能是Http头</returns>
    public static Boolean FastValidHeader(ReadOnlySpan<Byte> data)
    {
        if (data.Length > 10) data = data[..10];
        var position = data.IndexOf((Byte)' ');
        return position >= 0;
    }

    private static readonly Byte[] NewLine = [(Byte)'\r', (Byte)'\n'];
    private static readonly Byte[] NewLine2 = [(Byte)'\r', (Byte)'\n', (Byte)'\r', (Byte)'\n'];

    /// <summary>分析请求头</summary>
    /// <param name="packet">数据包</param>
    /// <returns>是否成功</returns>
    public Boolean Parse(IPacket packet)
    {
        var data = packet.GetSpan();
        if (!FastValidHeader(data)) return false;

        var position = data.IndexOf(NewLine2);
        if (position < 0) return false;

        var header = data[..position];
        var firstLine = String.Empty;
        while (header.Length > 0)
        {
            var next = header.IndexOf(NewLine);
            var line = next < 0 ? header : header[..next];
            if (firstLine.IsNullOrEmpty())
                firstLine = line.ToStr();
            else
            {
                var separator = line.IndexOf((Byte)':');
                if (separator > 0)
                {
                    var name = line[..separator].ToStr().Trim();
                    var value = line[(separator + 1)..].ToStr().Trim();
                    Headers[name] = value;
                }
            }

            if (next < 0 || next + 2 >= header.Length) break;
            header = header[(next + 2)..];
        }

        Body = packet.Slice(position + 4, -1, true);

        ContentLength = Headers["Content-Length"].ToInt(-1);
        ContentType = Headers["Content-Type"];

        return OnParse(firstLine);
    }

    /// <summary>分析第一行</summary>
    /// <param name="firstLine">第一行</param>
    /// <returns>是否成功</returns>
    protected abstract Boolean OnParse(String firstLine);
    #endregion

    #region 读写
    /// <summary>创建请求响应包</summary>
    /// <returns>数据包</returns>
    public virtual IOwnerPacket Build()
    {
        var body = Body;
        var length = body != null ? body.Total : 0;

        var header = BuildHeader(length);

        length += Encoding.UTF8.GetByteCount(header);
        var packet = new OwnerPacket(length);
        var writer = new SpanWriter(packet.GetSpan());

        writer.Write(header, -1);
        if (body != null) writer.Write(body.GetSpan());

        return packet.Resize(writer.Position);
    }

    /// <summary>创建头部</summary>
    /// <param name="length">主体长度</param>
    /// <returns>头部文本</returns>
    protected abstract String BuildHeader(Int32 length);
    #endregion
}