using System.Net;
using System.Net.Http;

namespace Pek.Log;

/// <summary>追踪器解析器</summary>
public interface ITracerResolver
{
    /// <summary>从 Uri 中解析埋点名称</summary>
    /// <param name="uri">目标地址</param>
    /// <param name="userState">用户状态</param>
    /// <returns>埋点名称</returns>
    String? ResolveName(Uri uri, Object? userState);

    /// <summary>解析埋点名称</summary>
    /// <param name="name">原始名称</param>
    /// <param name="userState">用户状态</param>
    /// <returns>埋点名称</returns>
    String? ResolveName(String name, Object? userState);

    /// <summary>创建 Http 请求埋点</summary>
    /// <param name="tracer">跟踪器</param>
    /// <param name="uri">目标地址</param>
    /// <param name="userState">用户状态</param>
    /// <returns>埋点实例</returns>
    ISpan? CreateSpan(ITracer tracer, Uri uri, Object? userState);
}

/// <summary>默认追踪器解析器</summary>
public class DefaultTracerResolver : ITracerResolver
{
    /// <summary>请求内容是否作为数据标签。默认true</summary>
    public Boolean RequestContentAsTag { get; set; } = true;

    /// <summary>支持作为标签数据的内容类型</summary>
    public String[] TagTypes { get; set; } = [
        "text/plain", "text/xml", "application/json", "application/xml", "application/x-www-form-urlencoded"
    ];

    /// <summary>标签数据中要排除的头部</summary>
    public String[] ExcludeHeaders { get; set; } = ["traceparent", "Cookie"];

    /// <summary>从 Uri 中解析埋点名称</summary>
    /// <param name="uri">目标地址</param>
    /// <param name="userState">用户状态</param>
    /// <returns>埋点名称</returns>
    public virtual String? ResolveName(Uri uri, Object? userState)
    {
        String name;
        if (uri.IsAbsoluteUri)
        {
            var segments = uri.Segments.Skip(1).TakeWhile(e => e.Length <= 16).ToArray();
            name = segments.Length > 0
                ? $"{uri.Scheme}://{uri.Authority}/{String.Concat(segments)}"
                : $"{uri.Scheme}://{uri.Authority}";
        }
        else
        {
            name = uri.ToString();
            var p = name.IndexOf('?');
            if (p > 0) name = name[..p];
        }

        return ResolveName(name, userState);
    }

    /// <summary>解析埋点名称</summary>
    /// <param name="name">原始名称</param>
    /// <param name="userState">用户状态</param>
    /// <returns>埋点名称</returns>
    public virtual String? ResolveName(String name, Object? userState) => name;

    /// <summary>创建 Http 请求埋点</summary>
    /// <param name="tracer">跟踪器</param>
    /// <param name="uri">目标地址</param>
    /// <param name="userState">用户状态</param>
    /// <returns>埋点实例</returns>
    public virtual ISpan? CreateSpan(ITracer tracer, Uri uri, Object? userState)
    {
        var name = ResolveName(uri, userState);
        if (String.IsNullOrWhiteSpace(name)) return null;

        var span = tracer.NewSpan(name);

        var request = userState as HttpRequestMessage;
        var method = request?.Method.Method ?? (userState as WebRequest)?.Method ?? "GET";
        var tag = $"{method} {uri}";

        if (RequestContentAsTag && tag.Length < tracer.MaxTagLength && span is DefaultSpan ds && ds.TraceFlag > 0 && request != null)
        {
            var maxLength = ds.Tracer?.MaxTagLength ?? 1024;
            var content = request.Content;
            var mediaType = content?.Headers.ContentType?.MediaType;
            var contentLength = content?.Headers.ContentLength;

            if (content != null && contentLength != null && contentLength < 1024 * 8 && !String.IsNullOrWhiteSpace(mediaType) &&
                TagTypes.Any(e => mediaType.StartsWith(e, StringComparison.OrdinalIgnoreCase)))
            {
                var body = content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                if (!String.IsNullOrWhiteSpace(body))
                {
                    tag += "\r\n" + (body.Length > maxLength ? body[..maxLength] : body);
                }
            }

            if (tag.Length < 500)
            {
                var headers = request.Headers
                    .Where(e => !ExcludeHeaders.Any(x => String.Equals(x, e.Key, StringComparison.OrdinalIgnoreCase)))
                    .Select(e => $"{e.Key}: {String.Join(";", e.Value)}");
                var headerText = String.Join("\r\n", headers);
                if (!String.IsNullOrWhiteSpace(headerText)) tag += "\r\n" + headerText;
            }
        }

        span.SetTag(tag);
        return span;
    }
}