namespace NewLife.Log;

/// <summary>默认追踪器</summary>
public sealed class DefaultTracer : ITracer
{
    /// <summary>全局实例</summary>
    public static ITracer? Instance { get; set; }

    /// <summary>开始一个片段</summary>
    /// <param name="name">片段名称</param>
    /// <returns>追踪片段</returns>
    public ISpan NewSpan(String name) => DefaultSpan.Null;

    /// <summary>开始一个带标签的片段</summary>
    /// <param name="name">片段名称</param>
    /// <param name="tag">标签对象</param>
    /// <returns>追踪片段</returns>
    public ISpan NewSpan(String name, Object? tag) => DefaultSpan.Null;
}
