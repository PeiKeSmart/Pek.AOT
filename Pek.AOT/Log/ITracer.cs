using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text;

using Pek.Collections;
using Pek.Data;
using Pek.Serialization;
using Pek.Threading;

namespace Pek.Log;

/// <summary>性能跟踪器</summary>
public interface ITracer
{
    /// <summary>采样周期。单位秒</summary>
    Int32 Period { get; set; }

    /// <summary>最大正常采样数</summary>
    Int32 MaxSamples { get; set; }

    /// <summary>最大异常采样数</summary>
    Int32 MaxErrors { get; set; }

    /// <summary>超时时间。单位毫秒</summary>
    Int32 Timeout { get; set; }

    /// <summary>最大标签长度</summary>
    Int32 MaxTagLength { get; set; }

    /// <summary>注入 TraceId 的参数名</summary>
    String? AttachParameter { get; set; }

    /// <summary>埋点解析器</summary>
    ITracerResolver Resolver { get; set; }

    /// <summary>建立 Span 构建器</summary>
    /// <param name="name">操作名</param>
    /// <returns>构建器</returns>
    ISpanBuilder BuildSpan(String name);

    /// <summary>开始一个 Span</summary>
    /// <param name="name">操作名</param>
    /// <returns>跟踪片段</returns>
    ISpan NewSpan(String name);

    /// <summary>开始一个 Span，并设置标签</summary>
    /// <param name="name">操作名</param>
    /// <param name="tag">标签</param>
    /// <returns>跟踪片段</returns>
    ISpan NewSpan(String name, Object? tag);

    /// <summary>截取所有构建器</summary>
    /// <returns>构建器数组</returns>
    ISpanBuilder[] TakeAll();
}

/// <summary>默认跟踪器</summary>
public class DefaultTracer : DisposeBase, ITracer, ILogFeature
{
    private ConcurrentDictionary<String, ISpanBuilder> _builders = new();
    private Int32 _inited;
    private TimerX? _timer;
    private IPool<ISpanBuilder>? _builderPool;
    private IPool<ISpan>? _spanPool;

    /// <summary>全局实例</summary>
    public static ITracer? Instance { get; set; }

    /// <summary>采样周期。默认 15 秒</summary>
    public Int32 Period { get; set; } = 15;

    /// <summary>最大正常采样数</summary>
    public Int32 MaxSamples { get; set; } = 1;

    /// <summary>最大异常采样数</summary>
    public Int32 MaxErrors { get; set; } = 10;

    /// <summary>超时时间</summary>
    public Int32 Timeout { get; set; } = 15_000;

    /// <summary>最大标签长度</summary>
    public Int32 MaxTagLength { get; set; } = 1024;

    /// <summary>注入 TraceId 的参数名</summary>
    public String? AttachParameter { get; set; } = "traceparent";

    /// <summary>埋点解析器</summary>
    public ITracerResolver Resolver { get; set; } = new DefaultTracerResolver();

    /// <summary>Json序列化选项</summary>
    public JsonOptions JsonOptions { get; set; } = new()
    {
        CamelCase = false,
        IgnoreNullValues = false,
        IgnoreCycles = true,
        WriteIndented = false,
        FullTime = true,
        EnumString = true,
    };

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>构建器对象池</summary>
    [IgnoreDataMember]
    public IPool<ISpanBuilder> BuilderPool => _builderPool ??= new SpanBuilderPool(this);

    /// <summary>埋点对象池</summary>
    [IgnoreDataMember]
    public IPool<ISpan> SpanPool => _spanPool ??= new TracerSpanPool();

    /// <summary>开始一个 Span</summary>
    /// <param name="name">操作名</param>
    /// <returns>跟踪片段</returns>
    public virtual ISpan NewSpan(String name) => BuildSpan(name).Start();

    /// <summary>开始一个 Span，并设置标签</summary>
    /// <param name="name">操作名</param>
    /// <param name="tag">标签</param>
    /// <returns>跟踪片段</returns>
    public virtual ISpan NewSpan(String name, Object? tag)
    {
        var span = BuildSpan(name).Start();
        if (tag != null)
        {
            var needSample = span is DefaultSpan ds && ds.TraceFlag > 0;
            BuildTag(span, tag, needSample);
        }

        return span;
    }

    /// <summary>构建数据标签</summary>
    /// <param name="span">跟踪片段</param>
    /// <param name="tag">标签数据</param>
    /// <param name="needSample">是否需要采样</param>
    public virtual void BuildTag(ISpan span, Object? tag, Boolean needSample)
    {
        if (tag == null) return;

        var len = MaxTagLength;
        if (len <= 0) return;

        if (tag is String str)
        {
            span.Tag = str.Length > len ? str[..len] : str;
        }
        else if (tag is StringBuilder builder)
        {
            span.Tag = builder.Length <= len ? builder.ToString() : builder.ToString(0, len);
            span.Value = builder.Length;
        }
        else if (needSample)
        {
            if (tag is IPacket packet)
            {
                var total = packet.Total;
                if (total >= 2 && (packet[0] == '{' || packet[0] == '<') && (packet[total - 1] == '}' || packet[total - 1] == '>'))
                    span.Tag = packet.ToStr(null, 0, len);
                else
                    span.Tag = packet.ToHex(len / 2);

                span.Value = total;
            }
            else if (tag is EndPoint endPoint)
            {
                span.Tag = endPoint.ToString();
            }
            else if (tag is IPAddress address)
            {
                span.Tag = address.ToString();
            }
            else
            {
                try
                {
                    var json = tag.ToJson(JsonOptions);
                    span.Tag = json.Length > len ? json[..len] : json;
                }
                catch
                {
                    var value = tag.ToString();
                    if (!String.IsNullOrEmpty(value)) span.Tag = value.Length > len ? value[..len] : value;
                }
            }
        }
    }

    /// <summary>建立 Span 构建器</summary>
    /// <param name="name">操作名</param>
    /// <returns>构建器</returns>
    public virtual ISpanBuilder BuildSpan(String name)
    {
        InitTimer();

        name ??= String.Empty;
        var p = name.IndexOfAny(['?', '#', '&']);
        if (p > 0) name = name[..p];

        return _builders.GetOrAdd(name, OnBuildSpan);
    }

    /// <summary>截取所有构建器</summary>
    /// <returns>构建器数组</returns>
    public virtual ISpanBuilder[] TakeAll()
    {
        var builders = _builders;
        if (builders.IsEmpty) return [];

        _builders = new ConcurrentDictionary<String, ISpanBuilder>();

        var values = builders.Values.Where(e => e.Total > 0).ToArray();
        foreach (var item in values)
        {
            item.EndTime = Runtime.UtcNow.ToUnixTimeMilliseconds();
        }

        return values;
    }

    /// <summary>销毁资源</summary>
    /// <param name="disposing">是否显式释放</param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        _timer?.Dispose();
        DoProcessSpans();
    }

    /// <summary>处理 Span 集合</summary>
    /// <param name="builders">构建器集合</param>
    protected virtual void ProcessSpans(ISpanBuilder[] builders)
    {
        if (builders == null) return;

        foreach (var builder in builders)
        {
            if (builder.Total <= 0) continue;

            var averageCost = builder.Total == 0 ? 0 : builder.Cost / builder.Total;
            var duration = builder.EndTime > builder.StartTime ? builder.EndTime - builder.StartTime : builder.Cost;
            var speed = duration <= 0 ? 0 : builder.Total * 1000d / duration;
            this.WriteLog("Tracer[{0}] Total={1:n0} Errors={2:n0} Speed={3:n2}tps Cost={4:n0}ms MaxCost={5:n0}ms MinCost={6:n0}ms", builder.Name, builder.Total, builder.Errors, speed, averageCost, builder.MaxCost, builder.MinCost);
        }
    }

    private void InitTimer()
    {
        if (Interlocked.CompareExchange(ref _inited, 1, 0) != 0) return;
        _timer ??= new TimerX(_ => DoProcessSpans(), null, 5_000, Period * 1000) { Async = true };
    }

    private ISpanBuilder OnBuildSpan(String name)
    {
        var builder = BuilderPool.Get();
        if (builder is DefaultSpanBuilder dsb)
            dsb.Init(this, name);
        else
            builder.Name = name;

        return builder;
    }

    private void DoProcessSpans()
    {
        var builders = TakeAll();
        if (builders.Length > 0)
        {
            ProcessSpans(builders);

            foreach (var item in builders)
            {
                if (item is DefaultSpanBuilder builder) builder.ReturnToPool(BuilderPool);
            }
        }

        if (Period > 0 && _timer != null && _timer.Period != Period * 1000) _timer.Period = Period * 1000;
    }

    private sealed class SpanBuilderPool : Pool<ISpanBuilder>
    {
        public SpanBuilderPool(DefaultTracer tracer) : base(Environment.ProcessorCount * 2, () => new DefaultSpanBuilder { Tracer = tracer }) { }
    }

    private sealed class TracerSpanPool : Pool<ISpan>
    {
        public TracerSpanPool() : base(Environment.ProcessorCount * 4, () => new DefaultSpan()) { }
    }
}

/// <summary>跟踪扩展</summary>
public static class TracerExtension
{
    /// <summary>开始一个Span，指定数据标签和用户数值</summary>
    /// <param name="tracer">跟踪器</param>
    /// <param name="name">操作名</param>
    /// <param name="tag">数据</param>
    /// <param name="value">用户数值</param>
    /// <returns>跟踪片段</returns>
    public static ISpan NewSpan(this ITracer tracer, String name, Object? tag, Int64 value)
    {
        var span = tracer.NewSpan(name, tag);
        span.Value = value;

        return span;
    }

    /// <summary>为Http请求创建Span</summary>
    /// <param name="tracer">跟踪器</param>
    /// <param name="request">Http请求</param>
    /// <returns>跟踪片段</returns>
    public static ISpan? NewSpan(this ITracer tracer, HttpRequestMessage request)
    {
        if (request.RequestUri == null) return null;

        var span = tracer.Resolver.CreateSpan(tracer, request.RequestUri, request);
        if (span == null) return null;

        span.Attach(request);

        var len = request.Content?.Headers?.ContentLength;
        if (len != null) span.Value = len.Value;

        return span;
    }

    /// <summary>为Http请求创建Span</summary>
    /// <param name="tracer">跟踪器</param>
    /// <param name="request">Http请求</param>
    /// <returns>跟踪片段</returns>
    public static ISpan? NewSpan(this ITracer tracer, WebRequest request)
    {
        var span = tracer.Resolver.CreateSpan(tracer, request.RequestUri, request);
        if (span == null) return null;

        span.Attach(request);

        var len = request.Headers["Content-Length"];
        if (!String.IsNullOrEmpty(len) && Int64.TryParse(len, out var value)) span.Value = value;

        return span;
    }

    /// <summary>直接创建错误Span</summary>
    /// <param name="tracer">跟踪器</param>
    /// <param name="name">操作名</param>
    /// <param name="error">异常对象或错误信息</param>
    /// <returns>跟踪片段</returns>
    public static ISpan NewError(this ITracer tracer, String name, Object? error)
    {
        var span = tracer.NewSpan(name);
        if (error is Exception ex)
            span.SetError(ex, null);
        else
            span.Error = error + "";

        span.Dispose();

        return span;
    }

    /// <summary>直接创建错误Span</summary>
    /// <param name="tracer">跟踪器</param>
    /// <param name="name">操作名</param>
    /// <param name="error">异常对象或错误信息</param>
    /// <param name="tag">标签</param>
    /// <returns>跟踪片段</returns>
    public static ISpan NewError(this ITracer tracer, String name, Object error, Object tag)
    {
        var span = tracer.NewSpan(name);
        if (error is Exception ex)
            span.SetError(ex, null);
        else
            span.Error = error + "";

        span.SetTag(tag);
        span.Dispose();

        return span;
    }
}