using System.Collections.Concurrent;
using Pek.Collections;
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
    private readonly ConcurrentDictionary<String, ISpanBuilder> _builders = new(StringComparer.Ordinal);
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

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>构建器对象池</summary>
    public IPool<ISpanBuilder> BuilderPool => _builderPool ??= new SpanBuilderPool(this);

    /// <summary>埋点对象池</summary>
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
            span.SetTag(tag);
            if (span is DefaultSpan ds && ds.TraceFlag == 0) ds.TraceFlag = 1;
        }

        return span;
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
        if (_builders.IsEmpty) return [];

        var builders = _builders.ToArray();
        _builders.Clear();
        return builders.Select(e => e.Value).ToArray();
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
            foreach (var builder in builders) builder.EndTime = Runtime.UtcNow.ToUnixTimeMilliseconds();
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