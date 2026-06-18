using System.Collections.Concurrent;

namespace Pek.Helpers;

/// <summary>并行分区执行。AOT 安全版：仅保留不依赖 IServiceProvider/Activator.CreateInstance/DynamicInvoke 的重载</summary>
public static class ParallelPart
{
    /// <summary>并行处理<paramref name="source"/>，一个并行任务中分配<paramref name="rangeSize"/>个元素给<paramref name="action"/></summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="source">源数据</param>
    /// <param name="rangeSize">一个并行任务的最大分配数量</param>
    /// <param name="parallelOptions"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    public static ParallelLoopResult ForEach<T>(IEnumerable<T> source, Int32 rangeSize, ParallelOptions parallelOptions, Action<T> action)
    {
        return Parallel.ForEach(Partitioner.Create(0, source.Count(), Math.Min(source.Count(), rangeSize)), parallelOptions ?? new ParallelOptions(), (range, loopState) =>
        {
            for (var i = range.Item1; i < range.Item2; i++)
            {
                action?.Invoke(source.ElementAt(i));
            }
        });
    }

    /// <summary>并行处理<paramref name="source"/>，按照最多<paramref name="_maxDegreeOfParallelism"/>个个数分配元素给<paramref name="action"/>处理</summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="_maxDegreeOfParallelism">最大任务量</param>
    /// <param name="action"></param>
    /// <returns></returns>
    public static ParallelLoopResult ForEach<T>(IEnumerable<T> source, Int32 _maxDegreeOfParallelism, Action<T> action) =>
        ForEach(source, (source.Count() + _maxDegreeOfParallelism - 1) / _maxDegreeOfParallelism, new ParallelOptions { MaxDegreeOfParallelism = _maxDegreeOfParallelism }, action);

    /// <summary>并行处理<paramref name="source"/>给<paramref name="action"/>处理</summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    public static ParallelLoopResult ForEach<T>(IEnumerable<T> source, Action<T> action)
    {
        var parallelOptions = new ParallelOptions();
        return ForEach(source, (source.Count() + parallelOptions.MaxDegreeOfParallelism - 1) / parallelOptions.MaxDegreeOfParallelism, parallelOptions, action);
    }

    /// <summary>并行处理<paramref name="source"/>给<paramref name="action"/>处理，可指定<paramref name="parallelOptions"/></summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="parallelOptions"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    public static ParallelLoopResult ForEach<T>(IEnumerable<T> source, ParallelOptions parallelOptions, Action<T> action) =>
        ForEach(source, (source.Count() + parallelOptions.MaxDegreeOfParallelism - 1) / parallelOptions.MaxDegreeOfParallelism, parallelOptions, action);

    // AOT: skipped - 以下 IServiceProvider/Activator.CreateInstance/DynamicInvoke 依赖的重载已省略
    // 原 Pek.Common ParallelPart 中的 ForEach<T, T1> 等泛型委托重载使用了运行时反射创建实例和动态调用，
    // 这在 NativeAOT 中不被支持。如需类似功能，请在调用方预创建所需对象后传入简单的 Action<T> 重载。
}
