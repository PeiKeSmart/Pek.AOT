namespace NewLife.Log;

/// <summary>追踪器接口</summary>
public interface ITracer
{
    /// <summary>开始一个片段</summary>
    /// <param name="name">片段名称</param>
    /// <returns>追踪片段</returns>
    ISpan NewSpan(String name);

    /// <summary>开始一个带标签的片段</summary>
    /// <param name="name">片段名称</param>
    /// <param name="tag">标签对象</param>
    /// <returns>追踪片段</returns>
    ISpan NewSpan(String name, Object? tag);
}
