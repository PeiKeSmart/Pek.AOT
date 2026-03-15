using System.Net.Http;

using Pek.Data;

namespace Pek.Log;

/// <summary>追踪上下文工具</summary>
public static class TraceContext
{
    /// <summary>默认追踪头名称</summary>
    public static String HeaderName => XTrace.Tracer?.AttachParameter ?? "traceparent";

    /// <summary>当前埋点</summary>
    public static ISpan? Current => DefaultSpan.Current;

    /// <summary>当前追踪标识</summary>
    public static String? CurrentTraceId => Current?.TraceId;

    /// <summary>构造追踪头值</summary>
    /// <param name="span">埋点实例</param>
    /// <returns>追踪头值</returns>
    public static String? BuildTraceParent(ISpan? span = null)
    {
        span ??= Current;
        if (span == null || String.IsNullOrWhiteSpace(span.TraceId)) return null;

        var traceId = NormalizeHex(span.TraceId, 32);
        var parentId = NormalizeHex(span.Id, 16);
        var flags = span is DefaultSpan ds && ds.TraceFlag != 0 ? "01" : "00";

        return $"00-{traceId}-{parentId}-{flags}";
    }

    /// <summary>向请求附加追踪头</summary>
    /// <param name="request">请求对象</param>
    /// <param name="span">埋点实例</param>
    /// <returns>是否成功附加</returns>
    public static Boolean Attach(HttpRequestMessage request, ISpan? span = null)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var traceParent = BuildTraceParent(span);
        if (String.IsNullOrWhiteSpace(traceParent)) return false;

        request.Headers.Remove(HeaderName);
        request.Headers.TryAddWithoutValidation(HeaderName, traceParent);
        return true;
    }

    /// <summary>向消息附加追踪标识</summary>
    /// <param name="message">消息对象</param>
    /// <param name="span">埋点实例</param>
    /// <returns>是否成功附加</returns>
    public static Boolean Attach(ITraceMessage message, ISpan? span = null)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));

        span ??= Current;
        if (span == null || String.IsNullOrWhiteSpace(span.TraceId)) return false;

        message.TraceId = span.TraceId;
        return true;
    }

    /// <summary>尝试从对象中提取 TraceId</summary>
    /// <param name="source">来源对象</param>
    /// <param name="traceId">TraceId</param>
    /// <returns>是否成功</returns>
    public static Boolean TryGetTraceId(Object? source, out String? traceId)
    {
        traceId = null;
        if (source == null) return false;

        if (source is ITraceMessage traceMessage)
        {
            traceId = traceMessage.TraceId;
            return !String.IsNullOrWhiteSpace(traceId);
        }

        if (source is IExtend extend)
        {
            if (extend[nameof(ITraceMessage.TraceId)] is String trace && !String.IsNullOrWhiteSpace(trace))
            {
                traceId = trace;
                return true;
            }

            if (extend[HeaderName] is String traceParent && TryExtractTraceId(traceParent, out traceId)) return true;
        }

        if (source is String value) return TryExtractTraceId(value, out traceId);

        return false;
    }

    /// <summary>尝试把埋点续接到指定 TraceId</summary>
    /// <param name="span">埋点实例</param>
    /// <param name="source">来源对象</param>
    /// <returns>是否成功续接</returns>
    public static Boolean Continue(ISpan? span, Object? source)
    {
        if (span is not DefaultSpan defaultSpan) return false;
        if (!TryGetTraceId(source, out var traceId) || String.IsNullOrWhiteSpace(traceId)) return false;

        defaultSpan.Detach(traceId);
        return true;
    }

    /// <summary>尝试从追踪头中解析 TraceId</summary>
    /// <param name="traceParent">追踪头值</param>
    /// <param name="traceId">TraceId</param>
    /// <returns>是否成功</returns>
    public static Boolean TryExtractTraceId(String? traceParent, out String? traceId)
    {
        traceId = null;
        if (String.IsNullOrWhiteSpace(traceParent)) return false;

        var parts = traceParent.Split('-');
        if (parts.Length >= 4)
        {
            traceId = NormalizeHex(parts[1], 32).TrimStart('0');
            if (String.IsNullOrEmpty(traceId)) traceId = "0";
            return true;
        }

        traceId = traceParent.Trim();
        return !String.IsNullOrWhiteSpace(traceId);
    }

    private static String NormalizeHex(String? value, Int32 length)
    {
        if (String.IsNullOrWhiteSpace(value)) return new String('0', length);

        Span<Char> buffer = stackalloc Char[length];
        buffer.Fill('0');

        var position = length - 1;
        for (var i = value.Length - 1; i >= 0 && position >= 0; i--)
        {
            var ch = value[i];
            if (Uri.IsHexDigit(ch)) buffer[position--] = Char.ToLowerInvariant(ch);
        }

        return new String(buffer);
    }
}