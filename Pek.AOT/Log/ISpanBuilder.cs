using Pek.Collections;

namespace Pek.Log;

/// <summary>跟踪片段构建器</summary>
public interface ISpanBuilder
{
    /// <summary>跟踪器</summary>
    ITracer? Tracer { get; }

    /// <summary>操作名</summary>
    String Name { get; set; }

    /// <summary>开始时间</summary>
    Int64 StartTime { get; set; }

    /// <summary>结束时间</summary>
    Int64 EndTime { get; set; }

    /// <summary>采样总数</summary>
    Int32 Total { get; }

    /// <summary>错误次数</summary>
    Int32 Errors { get; }

    /// <summary>总耗时</summary>
    Int64 Cost { get; }

    /// <summary>最大耗时</summary>
    Int32 MaxCost { get; }

    /// <summary>最小耗时</summary>
    Int32 MinCost { get; }

    /// <summary>用户数值</summary>
    Int64 Value { get; set; }

    /// <summary>正常采样</summary>
    IList<ISpan>? Samples { get; set; }

    /// <summary>异常采样</summary>
    IList<ISpan>? ErrorSamples { get; set; }

    /// <summary>开始一个 Span</summary>
    /// <returns>跟踪片段</returns>
    ISpan Start();

    /// <summary>完成 Span</summary>
    /// <param name="span">跟踪片段</param>
    void Finish(ISpan span);
}

/// <summary>默认跟踪片段构建器</summary>
public class DefaultSpanBuilder : ISpanBuilder
{
    private Int32 _total;
    private Int32 _errors;
    private Int64 _cost;
    private Int64 _value;

    /// <summary>跟踪器</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>操作名</summary>
    public String Name { get; set; } = String.Empty;

    /// <summary>开始时间</summary>
    public Int64 StartTime { get; set; }

    /// <summary>结束时间</summary>
    public Int64 EndTime { get; set; }

    /// <summary>采样总数</summary>
    public Int32 Total => _total;

    /// <summary>错误次数</summary>
    public Int32 Errors => _errors;

    /// <summary>总耗时</summary>
    public Int64 Cost => _cost;

    /// <summary>最大耗时</summary>
    public Int32 MaxCost { get; set; }

    /// <summary>最小耗时</summary>
    public Int32 MinCost { get; set; } = -1;

    /// <summary>用户数值</summary>
    public Int64 Value { get => _value; set => _value = value; }

    /// <summary>正常采样</summary>
    public IList<ISpan>? Samples { get; set; }

    /// <summary>异常采样</summary>
    public IList<ISpan>? ErrorSamples { get; set; }

    /// <summary>初始化</summary>
    /// <param name="tracer">跟踪器</param>
    /// <param name="name">操作名</param>
    public void Init(ITracer tracer, String name)
    {
        Tracer = tracer;
        Name = name;
        StartTime = Runtime.UtcNow.ToUnixTimeMilliseconds();
        EndTime = 0;
        _total = 0;
        _errors = 0;
        _cost = 0;
        _value = 0;
        MaxCost = 0;
        MinCost = -1;
        Samples = null;
        ErrorSamples = null;
    }

    /// <summary>开始一个 Span</summary>
    /// <returns>跟踪片段</returns>
    public virtual ISpan Start()
    {
        DefaultSpan? span = null;
        if (Tracer is DefaultTracer tracer)
        {
            span = tracer.SpanPool.Get() as DefaultSpan;
            if (span != null)
            {
                span.Clear();
                span.Tracer = tracer;
                span.Name = Name;
            }
        }

        span ??= new DefaultSpan(Tracer!);
        span.Name = Name;
        span.Start();

        if (span.TraceFlag == 0 && Tracer != null && _total < Tracer.MaxSamples) span.TraceFlag = 1;
        return span;
    }

    /// <summary>完成 Span</summary>
    /// <param name="span">跟踪片段</param>
    public virtual void Finish(ISpan span)
    {
        var tracer = Tracer;
        if (tracer == null) return;

        var cost = (Int32)(span.EndTime - span.StartTime);
        if (cost < 0) cost = 0;
        if (cost > 3_600_000) return;

        Interlocked.Add(ref _cost, cost);
        var total = Interlocked.Increment(ref _total);
        if (span.Value != 0) Interlocked.Add(ref _value, span.Value);

        if (MaxCost < cost) MaxCost = cost;
        if (MinCost > cost || MinCost < 0) MinCost = cost;

        var force = span is DefaultSpan ds && ds.TraceFlag > 0;
        var sampled = false;
        if (!String.IsNullOrEmpty(span.Error))
        {
            if (Interlocked.Increment(ref _errors) <= tracer.MaxErrors || force && _errors <= tracer.MaxErrors * 10)
            {
                var list = ErrorSamples ??= [];
                lock (list)
                {
                    list.Add(span);
                    sampled = true;
                }
            }
        }
        else if (total <= tracer.MaxSamples || ((tracer.Timeout > 0 && cost > tracer.Timeout) || force) && total <= tracer.MaxSamples * 10)
        {
            var list = Samples ??= [];
            lock (list)
            {
                list.Add(span);
                sampled = true;
            }
        }

        if (!sampled && tracer is DefaultTracer tracer2 && span is DefaultSpan ds2)
        {
            ds2.Clear();
            tracer2.SpanPool.Return(ds2);
        }
    }

    internal void ReturnToPool(IPool<ISpanBuilder> builderPool)
    {
        ReturnSpans(Samples);
        ReturnSpans(ErrorSamples);
        Init(Tracer ?? new DefaultTracer(), String.Empty);
        Tracer = null;
        builderPool.Return(this);
    }

    private void ReturnSpans(IList<ISpan>? spans)
    {
        if (spans == null) return;
        if (Tracer is not DefaultTracer tracer) return;

        foreach (var item in spans)
        {
            if (item is DefaultSpan ds)
            {
                ds.Clear();
                tracer.SpanPool.Return(ds);
            }
        }
    }
}