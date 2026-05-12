using System.Net;

using Pek.Collections;
using Pek.Data;
using Pek.Extension;
using Pek.Serialization;

namespace Pek.Http;

/// <summary>Http响应</summary>
public class HttpResponse : HttpBase
{
    #region 属性
    /// <summary>状态码</summary>
    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

    /// <summary>状态描述</summary>
    public String? StatusDescription { get; set; }
    #endregion

    /// <summary>分析第一行</summary>
    /// <param name="firstLine">第一行</param>
    /// <returns>是否成功</returns>
    protected override Boolean OnParse(String firstLine)
    {
        if (firstLine.IsNullOrEmpty()) return false;
        if (!firstLine.StartsWith("HTTP/")) return false;

        var sections = firstLine.Split(' ');
        if (sections.Length < 3) return false;

        Version = sections[0].TrimStart("HTTP/");

        var code = sections[1].ToInt();
        if (code > 0) StatusCode = (HttpStatusCode)code;

        StatusDescription = sections.Skip(2).Join(" ");
        return true;
    }

    /// <summary>创建请求响应包</summary>
    /// <returns>数据包</returns>
    public override IOwnerPacket Build()
    {
        if (StatusCode > HttpStatusCode.OK && Body == null && !StatusDescription.IsNullOrEmpty())
            Body = (ArrayPacket)StatusDescription.GetBytes();

        return base.Build();
    }

    /// <summary>创建头部</summary>
    /// <param name="length">主体长度</param>
    /// <returns>头部文本</returns>
    protected override String BuildHeader(Int32 length)
    {
        var builder = Pool.StringBuilder.Get();
        builder.AppendFormat("HTTP/{2} {0} {1}\r\n", (Int32)StatusCode, StatusDescription ?? StatusCode.ToString(), Version);

        if (length > 0)
            Headers["Content-Length"] = length + String.Empty;
        else if (!Headers.ContainsKey("Transfer-Encoding") && !Headers.ContainsKey("Upgrade"))
            Headers["Content-Length"] = "0";

        if (!ContentType.IsNullOrEmpty()) Headers["Content-Type"] = ContentType;

        foreach (var item in Headers)
        {
            builder.AppendFormat("{0}: {1}\r\n", item.Key, item.Value);
        }

        builder.Append("\r\n");
        return builder.Return(true);
    }

    /// <summary>验证响应</summary>
    public void Valid()
    {
        if (StatusCode != HttpStatusCode.OK) throw new Exception(StatusDescription ?? (StatusCode + String.Empty));
    }

    /// <summary>设置结果</summary>
    /// <param name="result">结果</param>
    /// <param name="contentType">内容类型</param>
    public void SetResult(Object result, String? contentType = null)
    {
        if (result == null) return;

        if (result is Exception exception)
        {
            StatusCode = HttpStatusCode.InternalServerError;

            StatusDescription = exception.Message;
        }
        else if (result is ISpanSerializable spanSerializable)
        {
            if (contentType.IsNullOrEmpty()) contentType = "application/octet-stream";
            Body = spanSerializable.ToPacket();
        }
        else if (result is IAccessor accessor)
        {
            if (contentType.IsNullOrEmpty()) contentType = "application/octet-stream";

            using var memoryStream = new MemoryStream();
            accessor.Write(memoryStream, null);
            memoryStream.Position = 0;
            Body = new ArrayPacket(memoryStream);
        }
        else if (result is IPacket packet)
        {
            if (contentType.IsNullOrEmpty()) contentType = "application/octet-stream";
            Body = packet;
        }
        else if (result is Byte[] buffer)
        {
            if (contentType.IsNullOrEmpty()) contentType = "application/octet-stream";
            Body = (ArrayPacket)buffer;
        }
        else if (result is Stream stream)
        {
            if (contentType.IsNullOrEmpty()) contentType = "application/octet-stream";
            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            Body = (ArrayPacket)memoryStream.ToArray();
        }
        else if (result is String text)
        {
            if (contentType.IsNullOrEmpty()) contentType = "text/html";
            Body = (ArrayPacket)text.GetBytes();
        }
        else
        {
            if (contentType.IsNullOrEmpty()) contentType = "application/json";
            Body = (ArrayPacket)result.ToJson().GetBytes();
        }

        if (ContentType.IsNullOrEmpty()) ContentType = contentType;
    }

    /// <summary>返回字符串表示</summary>
    /// <returns>响应信息</returns>
    public override String ToString() => $"HTTP/{Version} {(Int32)StatusCode} {StatusDescription ?? (StatusCode + String.Empty)}";
}