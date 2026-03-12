namespace NewLife.Log;

/// <summary>链路片段</summary>
public interface ISpan : IDisposable
{
    /// <summary>记录异常</summary>
    /// <param name="exception">异常对象</param>
    /// <param name="tag">附加标签</param>
    void SetError(Exception exception, Object? tag);
}
