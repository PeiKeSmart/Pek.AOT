namespace Pek.Collections;

/// <summary>集群管理</summary>
/// <typeparam name="TKey">键类型</typeparam>
/// <typeparam name="TValue">值类型</typeparam>
public interface ICluster<TKey, TValue>
{
    /// <summary>最后使用资源</summary>
    KeyValuePair<TKey, TValue> Current { get; }

    /// <summary>资源列表</summary>
    Func<IEnumerable<TKey>> GetItems { get; set; }

    /// <summary>打开</summary>
    /// <returns>是否成功</returns>
    Boolean Open();

    /// <summary>关闭</summary>
    /// <param name="reason">关闭原因</param>
    /// <returns>是否成功</returns>
    Boolean Close(String reason);

    /// <summary>从集群获取资源</summary>
    /// <returns>资源</returns>
    TValue Get();

    /// <summary>归还资源</summary>
    /// <param name="value">资源</param>
    /// <returns>是否成功</returns>
    Boolean Put(TValue value);
}

/// <summary>集群助手</summary>
public static class ClusterHelper
{
    /// <summary>借助集群资源处理事务</summary>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <typeparam name="TValue">值类型</typeparam>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <param name="cluster">集群</param>
    /// <param name="func">处理函数</param>
    /// <returns>处理结果</returns>
    public static TResult Invoke<TKey, TValue, TResult>(this ICluster<TKey, TValue> cluster, Func<TValue, TResult> func)
    {
        var item = default(TValue);
        try
        {
            item = cluster.Get();
            return func(item);
        }
        finally
        {
            cluster.Put(item!);
        }
    }

    /// <summary>借助集群资源异步处理事务</summary>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <typeparam name="TValue">值类型</typeparam>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <param name="cluster">集群</param>
    /// <param name="func">异步处理函数</param>
    /// <returns>处理结果</returns>
    public static async Task<TResult> InvokeAsync<TKey, TValue, TResult>(this ICluster<TKey, TValue> cluster, Func<TValue, Task<TResult>> func)
    {
        var item = default(TValue);
        try
        {
            item = cluster.Get();
            return await func(item).ConfigureAwait(false);
        }
        finally
        {
            cluster.Put(item!);
        }
    }

#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
    /// <summary>借助集群资源异步处理事务</summary>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <typeparam name="TValue">值类型</typeparam>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <param name="cluster">集群</param>
    /// <param name="func">异步处理函数</param>
    /// <returns>处理结果</returns>
    public static async ValueTask<TResult> InvokeAsync<TKey, TValue, TResult>(this ICluster<TKey, TValue> cluster, Func<TValue, ValueTask<TResult>> func)
    {
        var item = default(TValue);
        try
        {
            item = cluster.Get();
            return await func(item).ConfigureAwait(false);
        }
        finally
        {
            cluster.Put(item!);
        }
    }
#endif
}