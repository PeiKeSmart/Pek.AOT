using System.Diagnostics;

namespace Pek;

[DebuggerStepThrough]
public static class ExtIEnumerable
{
    /// <summary>遍历集合并对每个元素执行操作</summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="source">源集合</param>
    /// <param name="fun">操作</param>
    /// <returns>源集合</returns>
    public static IEnumerable<T> Each<T>(this IEnumerable<T> source, Action<T> fun)
    {
        foreach (var item in source)
        {
            fun(item);
        }
        return source;
    }

    /// <summary>转换集合并生成列表</summary>
    /// <typeparam name="T">源类型</typeparam>
    /// <typeparam name="TResult">目标类型</typeparam>
    /// <param name="source">源集合</param>
    /// <param name="fun">转换函数</param>
    /// <returns>结果列表</returns>
    public static List<TResult> ToList<T, TResult>(this IEnumerable<T> source, Func<T, TResult> fun)
    {
        var result = new List<TResult>();
        source.Each(m => result.Add(fun(m)));
        return result;
    }
}
