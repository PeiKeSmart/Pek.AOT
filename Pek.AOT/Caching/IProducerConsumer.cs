namespace Pek.Caching;

/// <summary>轻量级生产者消费者接口</summary>
/// <typeparam name="T">元素类型</typeparam>
public interface IProducerConsumer<T>
{
    /// <summary>元素个数</summary>
    Int32 Count { get; }

    /// <summary>集合是否为空</summary>
    Boolean IsEmpty { get; }

    /// <summary>生产添加</summary>
    /// <param name="values">数据</param>
    /// <returns>成功添加数量</returns>
    Int32 Add(params T[] values);

    /// <summary>消费获取一批</summary>
    /// <param name="count">数量</param>
    /// <returns>数据集合</returns>
    IEnumerable<T> Take(Int32 count = 1);

    /// <summary>消费获取一个</summary>
    /// <param name="timeout">超时秒数，0表示永久等待</param>
    /// <returns>元素</returns>
    T? TakeOne(Int32 timeout = 0);

    /// <summary>异步消费获取一个</summary>
    /// <param name="timeout">超时秒数，0表示永久等待</param>
    /// <returns>元素</returns>
    Task<T?> TakeOneAsync(Int32 timeout = 0);

    /// <summary>异步消费获取一个</summary>
    /// <param name="timeout">超时秒数，0表示永久等待</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>元素</returns>
    Task<T?> TakeOneAsync(Int32 timeout, CancellationToken cancellationToken);

    /// <summary>确认消费</summary>
    /// <param name="keys">键</param>
    /// <returns>确认数量</returns>
    Int32 Acknowledge(params String[] keys);
}
