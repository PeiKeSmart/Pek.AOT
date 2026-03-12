namespace NewLife.Log;

/// <summary>默认链路片段</summary>
public sealed class DefaultSpan : ISpan
{
    private sealed class NullSpan : ISpan
    {
        public void Dispose() { }

        public void SetError(Exception exception, Object? tag) { }
    }

    private static readonly ISpan _nullSpan = new NullSpan();

    /// <summary>当前片段</summary>
    public static ISpan? Current { get; set; }

    /// <summary>空片段</summary>
    public static ISpan Null => _nullSpan;

    /// <summary>释放资源</summary>
    public void Dispose() { }

    /// <summary>记录异常</summary>
    /// <param name="exception">异常对象</param>
    /// <param name="tag">附加标签</param>
    public void SetError(Exception exception, Object? tag) { }
}
