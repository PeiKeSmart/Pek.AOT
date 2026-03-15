using Pek.Collections;

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
    private static readonly AsyncLocal<ISpan?> _current = new();
    private static Int64 _traceSequence;
    private static Int64 _spanSequence;
    private ISpan? _parent;
    private Int32 _finished;

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
        if (tag != null) SetTag(tag);
        if (exception == null) return;

        Error = exception.Message;
        AppendTag(exception.ToString());
    }

    /// <summary>设置数据标签</summary>
    /// <param name="tag">标签</param>
    public virtual void SetTag(Object tag)
    {
        if (tag == null) return;

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

    private static String CreateId() => Interlocked.Increment(ref _spanSequence).ToString("x16");

    private static String CreateTraceId()
    {
        var timestamp = Runtime.UtcNow.ToUnixTimeMilliseconds();
        var sequence = Interlocked.Increment(ref _traceSequence) & 0xFFFF;
        return $"{timestamp:x12}{sequence:x4}";
    }
}