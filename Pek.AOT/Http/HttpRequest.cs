using Pek.Collections;
using Pek.Buffers;
using Pek.Data;
using Pek.Extension;

namespace Pek.Http;

/// <summary>Http请求</summary>
public class HttpRequest : HttpBase
{
    #region 属性
    /// <summary>Http方法</summary>
    public String? Method { get; set; }

    /// <summary>资源路径</summary>
    public Uri? RequestUri { get; set; }

    /// <summary>目标主机</summary>
    public String? Host { get; set; }

    /// <summary>是否保持连接</summary>
    public Boolean KeepAlive { get; set; }

    /// <summary>文件集合</summary>
    public FormFile[]? Files { get; set; }
    #endregion

    /// <summary>分析第一行</summary>
    /// <param name="firstLine">第一行</param>
    /// <returns>是否成功</returns>
    protected override Boolean OnParse(String firstLine)
    {
        if (firstLine.IsNullOrEmpty()) return false;

        var sections = firstLine.Split(' ');
        if (sections.Length < 3) return false;

        if (sections.Length >= 3 && sections[2].StartsWithIgnoreCase("HTTP/"))
        {
            Method = sections[0];
            RequestUri = new Uri(sections[1], UriKind.RelativeOrAbsolute);
            Version = sections[2].TrimStart("HTTP/");
        }

        Host = Headers["Host"];

        var connection = Headers["Connection"];
        if (Version == "1.1")
            KeepAlive = !connection.EqualIgnoreCase("close");
        else
            KeepAlive = connection.EqualIgnoreCase("keep-alive");

        return true;
    }

    private static readonly Byte[] NewLine = [(Byte)'\r', (Byte)'\n'];
    private static readonly Byte[] NewLine2 = [(Byte)'\r', (Byte)'\n', (Byte)'\r', (Byte)'\n'];

    /// <summary>快速分析请求头</summary>
    /// <param name="packet">数据包</param>
    /// <returns>是否成功</returns>
    public Boolean FastParse(IPacket packet)
    {
        var data = packet.GetSpan();
        if (!FastValidHeader(data)) return false;

        var position = data.IndexOf(NewLine);
        if (position < 0) return false;

        var line = data.Slice(0, position).ToStr();
        Body = packet.Slice(position + 2, -1, true);

        return OnParse(line);
    }

    /// <summary>创建头部</summary>
    /// <param name="length">主体长度</param>
    /// <returns>头部文本</returns>
    protected override String BuildHeader(Int32 length)
    {
        if (Method.IsNullOrEmpty()) Method = length > 0 ? "POST" : "GET";

        var uri = RequestUri ?? new Uri("/", UriKind.Relative);

        if (Host.IsNullOrEmpty())
        {
            var host = String.Empty;
            if (uri.Host.IsNullOrEmpty())
            {
            }
            else if (uri.Scheme.EqualIgnoreCase("http", "ws"))
            {
                host = uri.Port == 80 ? uri.Host : $"{uri.Host}:{uri.Port}";
            }
            else if (uri.Scheme.EqualIgnoreCase("https", "wss"))
            {
                host = uri.Port == 443 ? uri.Host : $"{uri.Host}:{uri.Port}";
            }

            Host = host;
        }

        var builder = Pool.StringBuilder.Get();
        builder.AppendFormat("{0} {1} HTTP/{2}\r\n", Method, uri.PathAndQuery, Version);
        if (!Host.IsNullOrEmpty()) builder.AppendFormat("Host: {0}\r\n", Host);

        if (length > 0) Headers["Content-Length"] = length + String.Empty;
        if (!ContentType.IsNullOrEmpty()) Headers["Content-Type"] = ContentType;
        if (KeepAlive) Headers["Connection"] = "keep-alive";

        foreach (var item in Headers)
        {
            if (!item.Key.EqualIgnoreCase("Host")) builder.AppendFormat("{0}: {1}\r\n", item.Key, item.Value);
        }

        builder.Append("\r\n");
        return builder.Return(true);
    }

    /// <summary>分析表单数据</summary>
    /// <returns>表单字典</returns>
    public virtual IDictionary<String, Object> ParseFormData()
    {
        var dic = new Dictionary<String, Object>();
        if (ContentType.IsNullOrEmpty()) return dic;

        var boundary = ContentType.Substring("boundary=", null);
        if (boundary.IsNullOrEmpty()) return dic;

        var body = Body;
        if (body == null || body.Length == 0) return dic;

        var data = body.GetSpan();
        var index = 0;

        var boundary1 = ("--" + boundary + "\r\n").GetBytes();
        var boundary2 = ("\r\n--" + boundary).GetBytes();
        do
        {
            var (start, end) = ((ReadOnlySpan<Byte>)data).IndexOf(boundary1, boundary2);
            if (end < 0) break;

            var part = data.Slice(start, end);
            data = data[(start + end)..];

            var headerPosition = part.IndexOf(NewLine2);
            if (headerPosition < 0) break;
            var lines = part[..headerPosition].ToStr().SplitAsDictionary(":", "\r\n");
            if (lines.TryGetValue("Content-Disposition", out var disposition))
            {
                var items = disposition.SplitAsDictionary("=", ";", true);
                var file = new FormFile
                {
                    Name = items["name"],
                    FileName = items["filename"],
                    ContentDisposition = items["[0]"],
                };

                if (lines.TryGetValue("Content-Type", out var contentType)) file.ContentType = contentType;

                var fileData = part[(headerPosition + NewLine2.Length)..];
                file.Data = body.Slice(index + start + headerPosition + NewLine2.Length, fileData.Length, false);

                if (!file.Name.IsNullOrEmpty()) dic[file.Name] = file.FileName.IsNullOrEmpty() ? fileData.ToStr() : file;
            }

            if (data.Length >= boundary2.Length + 2 && data.Slice(boundary2.Length, 2).ToStr() == "--") break;
            index += start + end;

        } while (data.Length > 0);

        return dic;
    }

    /// <summary>返回字符串表示</summary>
    /// <returns>请求信息</returns>
    public override String ToString() => $"{Method} {RequestUri}";
}