using System.Collections.Specialized;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;

using Pek.Collections;
using Pek.Data;
using Pek.IO;
using Pek.Serialization;

namespace Pek.Log;

/// <summary>性能跟踪片段</summary>
public interface ISpan : IDisposable
{
    /// <summary>唯一标识</summary>
    String Id { get; set; }

    /// <summary>埋点名</summary>
    String Name { get; set; }

    /// <summary>父级片段标识</summary>
    String? ParentId { get; set; }

    /// <summary>跟踪标识</summary>
    String TraceId { get; set; }

    /// <summary>开始时间。Unix 毫秒</summary>
    Int64 StartTime { get; set; }

    /// <summary>结束时间。Unix 毫秒</summary>
    Int64 EndTime { get; set; }

    /// <summary>用户数值</summary>
    Int64 Value { get; set; }

    /// <summary>数据标签</summary>
    String? Tag { get; set; }

    /// <summary>错误信息</summary>
    String? Error { get; set; }

    /// <summary>设置错误信息</summary>
    /// <param name="exception">异常</param>
    /// <param name="tag">标签</param>
    void SetError(Exception exception, Object? tag = null);

    /// <summary>设置数据标签</summary>
    /// <param name="tag">标签</param>
    void SetTag(Object tag);

    /// <summary>抛弃埋点</summary>
    void Abandon();
}

/// <summary>默认跟踪片段</summary>
public class DefaultSpan : ISpan
{
#if NET45
    private static readonly ThreadLocal<ISpan?> _current = new();
#else
    private static readonly AsyncLocal<ISpan?> _current = new();
#endif
    private static readonly String _myIp;
    private static readonly String _pid;
    private static Int32 _traceSequence;
    private static Int32 _spanSequence;
    private ISpan? _parent;
    private Int32 _finished;

    static DefaultSpan()
    {
        IPAddress? address = null;
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            address = host.AddressList.FirstOrDefault(e => e.AddressFamily == AddressFamily.InterNetwork);
        }
        catch
        {
        }

        address ??= IPAddress.Loopback;
        _myIp = address.GetAddressBytes().ToHex().ToLowerInvariant().PadLeft(8, '0');

        var processId = Process.GetCurrentProcess().Id;
        _pid = (processId & 0xFFFF).ToString("x4").PadLeft(4, '0');
    }

    /// <summary>当前埋点</summary>
    public static ISpan? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }

    /// <summary>跟踪器</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>唯一标识</summary>
    public String Id { get; set; } = String.Empty;

    /// <summary>埋点名</summary>
    public String Name { get; set; } = String.Empty;

    /// <summary>父级片段标识</summary>
    public String? ParentId { get; set; }

    /// <summary>跟踪标识</summary>
    public String TraceId { get; set; } = String.Empty;

    /// <summary>开始时间</summary>
    public Int64 StartTime { get; set; }

    /// <summary>结束时间</summary>
    public Int64 EndTime { get; set; }

    /// <summary>用户数值</summary>
    public Int64 Value { get; set; }

    /// <summary>标签</summary>
    public String? Tag { get; set; }

    /// <summary>错误</summary>
    public String? Error { get; set; }

    /// <summary>强制采样标记</summary>
    public Byte TraceFlag { get; set; }

    /// <summary>实例化</summary>
    public DefaultSpan() { }

    /// <summary>实例化</summary>
    /// <param name="tracer">跟踪器</param>
    public DefaultSpan(ITracer tracer) => Tracer = tracer;

    /// <summary>释放资源</summary>
    public void Dispose() => Finish();

    /// <summary>开始埋点</summary>
    public virtual void Start()
    {
        StartTime = Runtime.UtcNow.ToUnixTimeMilliseconds();

        if (String.IsNullOrEmpty(Id)) Id = CreateId();

        var span = Current;
        _parent = span;
        if (span != null && span != this)
        {
            ParentId = span.Id;
            TraceId = span.TraceId;
            if (span is DefaultSpan ds) TraceFlag = ds.TraceFlag;
        }

        if (String.IsNullOrEmpty(TraceId)) TraceId = CreateTraceId();
        Current = this;
    }

    /// <summary>设置错误信息</summary>
    /// <param name="exception">异常</param>
    /// <param name="tag">标签</param>
    public virtual void SetError(Exception exception, Object? tag = null)
    {
        if (exception == null) return;

        if (tag != null) SetTag(tag);
        Error = exception.Message;

        using var span = Tracer?.NewSpan($"ex:{exception.GetType().Name}", tag);
        if (span != null)
        {
            span.AppendTag(exception.ToString());
            span.StartTime = StartTime;
        }
    }

    /// <summary>设置数据标签</summary>
    /// <param name="tag">标签</param>
    public virtual void SetTag(Object tag)
    {
        if (tag == null) return;

        if (Tracer is DefaultTracer defaultTracer)
        {
            defaultTracer.BuildTag(this, tag, true);
            return;
        }

        var value = tag as String ?? tag.ToString();
        if (String.IsNullOrEmpty(value)) return;

        var maxTagLength = Tracer?.MaxTagLength ?? 1024;
        Tag = value.Length > maxTagLength ? value[..maxTagLength] : value;
    }

    /// <summary>附加标签</summary>
    /// <param name="tag">标签</param>
    public virtual void AppendTag(String? tag)
    {
        if (String.IsNullOrWhiteSpace(tag)) return;

        var builder = Pool.StringBuilder.Get();
        try
        {
            if (!String.IsNullOrEmpty(Tag))
            {
                builder.Append(Tag);
                builder.AppendLine();
            }
            builder.Append(tag);
            SetTag(builder.ToString());
        }
        finally
        {
            Pool.StringBuilder.Return(builder);
        }
    }

    /// <summary>附加标签</summary>
    /// <param name="tag">标签</param>
    public virtual void AppendTag(Object? tag)
    {
        if (tag == null) return;
        AppendTag(tag.ToString());
    }

    /// <summary>切换跟踪标识</summary>
    /// <param name="traceId">跟踪标识</param>
    public virtual void Detach(String? traceId)
    {
        if (!String.IsNullOrWhiteSpace(traceId)) TraceId = traceId;
        TraceFlag = 1;
    }

    /// <summary>抛弃埋点</summary>
    public virtual void Abandon() => _finished = 1;

    /// <summary>清空状态</summary>
    public virtual void Clear()
    {
        Tracer = null;
        Id = String.Empty;
        Name = String.Empty;
        ParentId = null;
        TraceId = String.Empty;
        StartTime = 0;
        EndTime = 0;
        Value = 0;
        Tag = null;
        Error = null;
        TraceFlag = 0;
        _parent = null;
        _finished = 0;
    }

    private void Finish()
    {
        if (Interlocked.CompareExchange(ref _finished, 1, 0) != 0) return;

        EndTime = Runtime.UtcNow.ToUnixTimeMilliseconds();
        Current = _parent;

        var tracer = Tracer;
        if (tracer == null || String.IsNullOrEmpty(Name)) return;

        var builder = tracer.BuildSpan(Name);
        builder.Finish(this);
    }

    private static String CreateId()
    {
        var value = Interlocked.Increment(ref _spanSequence) & 0xFFFF;
        return _myIp + _pid + value.ToString("x4").PadLeft(4, '0');
    }

    private static String CreateTraceId()
    {
        var builder = Pool.StringBuilder.Get();
        builder.Append(_myIp);
        builder.Append(Runtime.UtcNow.ToUnixTimeMilliseconds());
        var value = Interlocked.Increment(ref _traceSequence) & 0xFFFF;
        builder.Append(value.ToString("x4").PadLeft(4, '0'));
        builder.Append('e');
        builder.Append(_pid);

        return builder.Return(true);
    }

    /// <summary>返回文本表示</summary>
    /// <returns>TraceParent</returns>
    public override String ToString() => $"00-{TraceId}-{Id}-{TraceFlag:x2}";
}

/// <summary>跟踪片段扩展</summary>
public static class SpanExtension
{
    private static String? GetAttachParameter(ISpan span) => (span as DefaultSpan)?.Tracer?.AttachParameter;

    /// <summary>附加到 Http 请求</summary>
    /// <param name="span">埋点</param>
    /// <param name="request">请求</param>
    /// <returns>请求</returns>
    public static HttpRequestMessage Attach(this ISpan span, HttpRequestMessage request)
    {
        var name = GetAttachParameter(span);
        if (String.IsNullOrWhiteSpace(name)) return request;

        if (!request.Headers.Contains(name)) request.Headers.Add(name, span.ToString());
        return request;
    }

    /// <summary>附加到请求头字典</summary>
    /// <param name="span">埋点</param>
    /// <param name="headers">请求头</param>
    /// <returns>请求头</returns>
    public static IDictionary<String, String> Attach(this ISpan span, IDictionary<String, String> headers)
    {
        var name = GetAttachParameter(span);
        if (String.IsNullOrWhiteSpace(name)) return headers;
        var value = span.ToString() ?? String.Empty;

        if (!headers.ContainsKey(name)) headers.Add(name, value);

        return headers;
    }

    /// <summary>附加到 WebRequest</summary>
    /// <param name="span">埋点</param>
    /// <param name="request">请求</param>
    /// <returns>请求</returns>
    public static WebRequest Attach(this ISpan span, WebRequest request)
    {
        var name = GetAttachParameter(span);
        if (String.IsNullOrWhiteSpace(name)) return request;

        if (!request.Headers.AllKeys.Contains(name)) request.Headers.Add(name, span.ToString());
        return request;
    }

    /// <summary>附加到参数对象</summary>
    /// <param name="span">埋点</param>
    /// <param name="args">参数对象</param>
    /// <returns>参数对象</returns>
    [return: NotNullIfNotNull(nameof(args))]
    public static Object? Attach(this ISpan span, Object? args)
    {
        if (span == null || args == null || args is IPacket || args is Byte[] || args is IAccessor) return args;

        if (args is ITraceMessage traceMessage)
        {
            if (String.IsNullOrWhiteSpace(traceMessage.TraceId)) traceMessage.TraceId = span.ToString();
            return args;
        }

        var type = args.GetType();
        if (type.IsArray || type.IsValueType || type == typeof(String)) return args;

        var name = GetAttachParameter(span);
        if (String.IsNullOrWhiteSpace(name)) return args;
        var value = span.ToString() ?? String.Empty;

        if (args is IDictionary<String, String> headers)
        {
            if (!headers.ContainsKey(name)) headers.Add(name, value);
            return headers;
        }

        if (args is IDictionary<String, Object?> values)
        {
            if (!values.ContainsKey(name)) values.Add(name, value);
            return values;
        }

        return args;
    }

    /// <summary>从请求头字典续接埋点</summary>
    /// <param name="span">埋点</param>
    /// <param name="headers">请求头</param>
    public static void Detach(this ISpan span, IDictionary<String, String> headers)
    {
        if (span == null || headers == null || headers.Count == 0) return;

        var values = new Dictionary<String, String>(headers, StringComparer.OrdinalIgnoreCase);
        DetachCore(span, values);
    }

    /// <summary>从请求头集合续接埋点</summary>
    /// <param name="span">埋点</param>
    /// <param name="headers">请求头</param>
    public static void Detach(this ISpan span, NameValueCollection headers)
    {
        if (span == null || headers == null || headers.Count == 0) return;

        var values = new Dictionary<String, String?>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in headers.AllKeys)
        {
            if (item != null) values[item] = headers[item];
        }

        DetachCore(span, values);
    }

    /// <summary>从请求头字典续接埋点</summary>
    /// <param name="span">埋点</param>
    /// <param name="headers">请求头</param>
    public static void Detach(this ISpan span, IDictionary<String, Object?> headers)
    {
        if (span == null || headers == null || headers.Count == 0) return;

        var values = headers.ToDictionary(e => e.Key, e => e.Value, StringComparer.OrdinalIgnoreCase);
        DetachCore(span, values);
    }

    /// <summary>从数据流traceId中续接埋点</summary>
    /// <param name="span">埋点</param>
    /// <param name="traceId">跟踪标识</param>
    public static void Detach(this ISpan span, String? traceId)
    {
        if (span == null || String.IsNullOrWhiteSpace(traceId)) return;

        var parts = traceId.Split('-');
        if (parts.Length == 1)
            span.TraceId = parts[0];
        else if (parts.Length > 1)
            span.TraceId = parts[1];

        if (parts.Length > 2) span.ParentId = parts[2];

        if (parts.Length > 3 && span is DefaultSpan defaultSpan && defaultSpan.TraceFlag == 0 && TryParseTraceFlag(parts[3], out var traceFlag))
            defaultSpan.TraceFlag = traceFlag;
    }

    /// <summary>附加Tag信息在原Tag信息后面</summary>
    /// <param name="span">片段</param>
    /// <param name="tag">Tag信息</param>
    public static void AppendTag(this ISpan span, Object tag)
    {
        if (span == null || tag == null) return;

        AppendTag(span, tag, -1);
    }

    /// <summary>附加Tag信息在原Tag信息后面</summary>
    /// <param name="span">片段</param>
    /// <param name="tag">Tag信息</param>
    /// <param name="value">可累加的数值标量</param>
    public static void AppendTag(this ISpan span, Object tag, Int64 value)
    {
        if (span == null) return;

        if (value >= 0) span.Value = value;

        if (tag != null && span is DefaultSpan ds && ds.TraceFlag > 0)
        {
            var maxLength = ds.Tracer?.MaxTagLength ?? 1024;
            if (String.IsNullOrEmpty(span.Tag))
                span.SetTag(tag);
            else if (span.Tag.Length < maxLength)
            {
                var old = span.Tag;
                span.SetTag(tag);

                var appended = old + "\r\n" + span.Tag;
                span.Tag = appended.Length > maxLength ? appended[..maxLength] : appended;
            }
        }
    }

    /// <summary>附加Http响应内容在原Tag信息后面</summary>
    /// <param name="span">埋点</param>
    /// <param name="response">响应</param>
    public static void AppendTag(this ISpan span, HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = response.Content;
            var len = content.Headers?.ContentLength ?? 0;
            if (span.Value == 0) span.Value = len;

            if (span is DefaultSpan ds && ds.TraceFlag > 0)
            {
                var maxLength = ds.Tracer?.MaxTagLength ?? 1024;
                if (String.IsNullOrEmpty(span.Tag) || span.Tag.Length < maxLength)
                {
                    var mediaType = content.Headers?.ContentType?.MediaType;
                    if (!String.IsNullOrEmpty(mediaType) && len >= 0 && len < 1024 * 8 &&
                        (mediaType.EndsWith("json", StringComparison.OrdinalIgnoreCase) ||
                         mediaType.EndsWith("xml", StringComparison.OrdinalIgnoreCase) ||
                         mediaType.EndsWith("text", StringComparison.OrdinalIgnoreCase) ||
                         mediaType.EndsWith("html", StringComparison.OrdinalIgnoreCase)))
                    {
                        var result = content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                        if (!String.IsNullOrEmpty(result))
                        {
                            var text = (span.Tag ?? String.Empty) + "\r\n" + result;
                            span.Tag = text.Length > maxLength ? text[..maxLength] : text;
                        }
                    }
                }
            }
        }
        else if ((Int32)response.StatusCode > 299 && String.IsNullOrEmpty(span.Error))
        {
            span.Error = response.ReasonPhrase;
        }
    }

    private static void DetachCore<T>(ISpan span, IDictionary<String, T> values)
    {
        if (values.TryGetValue("traceparent", out var traceParent))
        {
            span.Detach(traceParent + "");
            return;
        }

        if (values.TryGetValue("Request-Id", out var requestId))
        {
            var parts = (requestId + "").Split('.', '_');
            if (parts.Length > 0) span.TraceId = parts[0].TrimStart('|');
            if (parts.Length > 1) span.ParentId = parts[^1];
            return;
        }

        if (values.TryGetValue("Eagleeye-Traceid", out var eagleEyeTraceId))
        {
            var parts = (eagleEyeTraceId + "").Split('-');
            if (parts.Length > 0) span.TraceId = parts[0];
            if (parts.Length > 1) span.ParentId = parts[1];
            return;
        }

        if (values.TryGetValue("TraceId", out var traceId)) span.Detach(traceId + "");
    }

    private static Boolean TryParseTraceFlag(String? value, out Byte traceFlag)
    {
        traceFlag = 0;
        if (String.IsNullOrWhiteSpace(value)) return false;

        value = value.Length > 2 ? value[..2] : value;
        return Byte.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out traceFlag);
    }
}