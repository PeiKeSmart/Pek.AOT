using System.Net;

using Pek.Buffers;
using Pek.Data;
using Pek.Extension;
using Pek.Log;
using Pek.Net;
using Pek.Serialization;

namespace Pek.Http;

/// <summary>Http会话</summary>
public class HttpSession : INetHandler
{
    #region 属性
    /// <summary>当前请求</summary>
    public HttpRequest? Request { get; set; }

    /// <summary>Http主机</summary>
    public IHttpHost? Host { get; set; }

    /// <summary>最大请求长度</summary>
    public Int32 MaxRequestLength { get; set; } = 1024 * 1024 * 1024;

    /// <summary>忽略的头部</summary>
    public static String[] ExcludeHeaders { get; set; } = ["traceparent", "Authorization", "Cookie"];

    /// <summary>可作为标签内容的类型</summary>
    public static String[] TagTypes { get; set; } = ["text/plain", "text/xml", "application/json", "application/xml", "application/x-www-form-urlencoded"];

    private INetSession _session = null!;
    private WebSocket? _webSocket;
    private MemoryStream? _cache;
    #endregion

    #region 收发数据
    /// <summary>初始化会话</summary>
    /// <param name="session">网络会话</param>
    public void Init(INetSession session)
    {
        _session = session;
        Host ??= session.Host as IHttpHost;
    }

    /// <summary>处理数据</summary>
    /// <param name="data">数据帧</param>
    public void Process(IData data)
    {
        var packet = data.Packet;
        if (packet == null || packet.Length == 0) return;

        if (_webSocket != null)
        {
            _webSocket.Process(packet);
            return;
        }

        var req = Request;
        var request = new HttpRequest();
        if (request.Parse(packet))
        {
            req = Request = request;
            (_session as NetSession)?.WriteLog("{0} {1}", request.Method, request.RequestUri);

            if (req.ContentLength > MaxRequestLength)
            {
                using var response = new HttpResponse { StatusCode = HttpStatusCode.RequestEntityTooLarge }.Build();
                _session.Send(response);
                _session.Dispose();
                return;
            }

            _webSocket = null;
            OnNewRequest(request, data);

            if (req.IsCompleted)
            {
                _cache = null;
            }
            else
            {
                var len = req.ContentLength;
                if (len <= 0) len = 0;
                _cache = new MemoryStream(len > 0 ? len : 0);

                if (req.Body != null && req.Body.Length > 0)
                {
                    req.Body.CopyTo(_cache);
                    req.Body.TryDispose();
                    req.Body = null;
                }
            }
        }
        else if (req != null)
        {
            if (_cache != null)
            {
                packet.CopyTo(_cache);
                if (_cache.Length >= req.ContentLength)
                {
                    _cache.Position = 0;
                    req.Body = new ArrayPacket(_cache);
                    _cache = null;
                }
            }
        }

        if (req != null)
        {
            data.Message = req;
            data.Packet = req.Body;
        }

        if (req != null && req.IsCompleted)
        {
            var response = ProcessRequest(req, data);
            if (response != null)
            {
                var closing = !req.KeepAlive && _webSocket == null;
                if (closing && !response.Headers.ContainsKey("Connection")) response.Headers["Connection"] = "close";

                using var owner = response.Build();
                _session.Send(owner);

                if (closing) _session.Dispose();
            }
        }

        if (req != null)
        {
            req.Body.TryDispose();
            req.Body = null;
        }
    }

    /// <summary>收到新的Http请求</summary>
    /// <param name="request">请求</param>
    /// <param name="data">数据帧</param>
    protected virtual void OnNewRequest(HttpRequest request, IData data) { }

    /// <summary>处理Http请求</summary>
    /// <param name="request">请求</param>
    /// <param name="data">数据帧</param>
    /// <returns>响应</returns>
    protected virtual HttpResponse ProcessRequest(HttpRequest request, IData data)
    {
        if (request?.RequestUri == null) return new HttpResponse { StatusCode = HttpStatusCode.NotFound };

        var path = request.RequestUri.OriginalString;
        var position = path.IndexOf('?');
        if (position > 0) path = path[..position];

        if (!IsPathSafe(path)) return new HttpResponse { StatusCode = HttpStatusCode.Forbidden };

        using var span = _session.Host.Tracer?.NewSpan(path);
        if (span != null)
        {
            span.Tag = $"{_session.Remote.EndPoint} {request.Method} {request.RequestUri}";
            span.Detach(request.Headers);
            span.Value = request.ContentLength;

            if (span is DefaultSpan defaultSpan && defaultSpan.TraceFlag > 0)
                AppendSpanTag(span, request);
        }

        var handler = Host?.MatchHandler(path, request);
        var context = new DefaultHttpContext(_session, request, path, handler)
        {
            ServiceProvider = _session as IServiceProvider,
        };

        try
        {
            PrepareRequest(context);

            _webSocket ??= WebSocket.Handshake(context);

            if (handler != null)
                handler.ProcessRequest(context);
            else if (_webSocket == null)
                return new HttpResponse { StatusCode = HttpStatusCode.NotFound };

            if (span != null)
            {
                var response = context.Response;
                span.Value += response.ContentLength;
                var code = response.StatusCode;
                if (code == HttpStatusCode.BadRequest || code > HttpStatusCode.NotFound)
                    span.SetError(new HttpRequestException($"Http Error {(Int32)code} {code}"), null);
            }
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            context.Response.SetResult(ex);
        }

        return context.Response;
    }

    private void AppendSpanTag(ISpan span, HttpRequest request)
    {
        var includeBody = false;
        var bodyLength = request.Body?.Length ?? 0;
        if (request.BodyLength > 0 && request.Body != null && bodyLength > 0 && bodyLength < 8 * 1024 && request.ContentType.EqualIgnoreCase(TagTypes))
        {
            var body = request.Body.GetSpan();
            if (body.Length > 1024) body = body[..1024];
            span.AppendTag("\r\n<=\r\n" + body.ToStr());
            includeBody = true;
        }

        if (span.Tag == null || span.Tag.Length < 500)
        {
            if (!includeBody) span.AppendTag("\r\n<=");

            var values = request.Headers
                .Where(item => !item.Key.EqualIgnoreCase(ExcludeHeaders))
                .ToDictionary(item => item.Key, item => item.Value + String.Empty, StringComparer.OrdinalIgnoreCase);
            span.AppendTag("\r\n" + values.Join(Environment.NewLine, item => $"{item.Key}:{item.Value}"));
        }
        else if (!includeBody)
        {
            span.AppendTag("\r\n<=\r\n");
            span.AppendTag($"ContentLength: {request.ContentLength}\r\n");
            span.AppendTag($"ContentType: {request.ContentType}");
        }
    }

    private static Boolean IsPathSafe(String path) => path.IndexOf("..", StringComparison.Ordinal) < 0;

    /// <summary>准备请求参数</summary>
    /// <param name="context">Http上下文</param>
    protected virtual void PrepareRequest(IHttpContext context)
    {
        var req = context.Request;
        var parameters = context.Parameters;

        var uri = req.RequestUri;
        if (uri == null) return;

        var url = uri.OriginalString;
        var position = url.IndexOf('?');
        if (position > 0)
        {
            var values = url[(position + 1)..].SplitAsDictionary("=", "&");
            foreach (var item in values)
            {
                parameters[UrlDecode(item.Key)] = UrlDecode(item.Value);
            }
        }

        if (req.Method == "POST" && req.BodyLength > 0 && req.Body != null)
            ParsePostBody(req, parameters);
    }

    private void ParsePostBody(HttpRequest req, IDictionary<String, Object?> parameters)
    {
        var body = req.Body!.GetSpan();
        if ((req.ContentType ?? String.Empty).StartsWithIgnoreCase("application/x-www-form-urlencoded", "application/x-www-urlencoded"))
        {
            var values = body.ToStr().SplitAsDictionary("=", "&");
            foreach (var item in values)
            {
                parameters[UrlDecode(item.Key)] = UrlDecode(item.Value);
            }
        }
        else if ((req.ContentType ?? String.Empty).StartsWithIgnoreCase("multipart/form-data;"))
        {
            var dic = req.ParseFormData();
            var files = dic.Values.Where(item => item is FormFile).Cast<FormFile>().ToArray();
            if (files.Length > 0) req.Files = files;

            foreach (var item in dic)
            {
                parameters[item.Key] = item.Value;
            }
        }
        else if (body.Length >= 2 && body[0] == (Byte)'{' && body[^1] == (Byte)'}')
        {
            var json = body.ToStr().DecodeJson();
            if (json != null)
            {
                foreach (var item in json)
                {
                    parameters[item.Key] = item.Value;
                }
            }
        }
    }

    private static String UrlDecode(String? value)
    {
        if (value.IsNullOrEmpty()) return String.Empty;

        value = value.Replace('+', ' ');
        return Uri.UnescapeDataString(value);
    }
    #endregion
}