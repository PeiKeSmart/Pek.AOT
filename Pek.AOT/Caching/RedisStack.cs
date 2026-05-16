#nullable enable

using Pek.Data;

namespace Pek.Caching;

/// <summary>Redis 栈，右进右出</summary>
/// <typeparam name="T">元素类型</typeparam>
public class RedisStack<T> : RedisBase, IProducerConsumer<T>
{
    /// <summary>最小管道阈值</summary>
    public Int32 MinPipeline { get; set; } = 3;

    /// <summary>实例化</summary>
    /// <param name="redis">Redis 实例</param>
    /// <param name="key">键</param>
    public RedisStack(Redis redis, String key) : base(redis, key) { }

    /// <summary>个数</summary>
    public Int32 Count => Execute(redisClient => redisClient.Execute<Int32>("LLEN", Key));

    /// <summary>是否为空</summary>
    public Boolean IsEmpty => Count == 0;

    /// <summary>生产添加</summary>
    /// <param name="values">元素集合</param>
    /// <returns>栈长度</returns>
    public Int32 Add(params T[] values)
    {
        if (values == null || values.Length == 0) return Count;

        var args = new List<Object?> { Key };
        foreach (var item in values)
        {
            args.Add(item);
        }

        return Execute(redisClient => redisClient.Execute<Int32>("RPUSH", [.. args]), true);
    }

    /// <summary>批量消费获取</summary>
    /// <param name="count">数量</param>
    /// <returns>元素集合</returns>
    public IEnumerable<T> Take(Int32 count = 1)
    {
        if (count <= 0) yield break;

        if (count >= MinPipeline)
        {
            var redis = Redis;
            redis.StartPipeline();
            for (var i = 0; i < count; i++)
            {
                Execute(redisClient => redisClient.Execute<T>("RPOP", Key), true);
            }

            var results = redis.StopPipeline(true);
            if (results == null) yield break;

            foreach (var item in results)
            {
                if (item == null || Equals(item, default(T))) break;
                yield return (T)item;
            }

            yield break;
        }

        for (var i = 0; i < count; i++)
        {
            var value = Execute(redisClient => redisClient.Execute<T>("RPOP", Key), true);
            if (value == null || Equals(value, default(T))) yield break;

            yield return value;
        }
    }

    /// <summary>消费一个</summary>
    /// <param name="timeout">超时秒数。0 表示永久阻塞，负数表示不阻塞</param>
    /// <returns>元素</returns>
    public T? TakeOne(Int32 timeout = 0)
    {
        if (timeout < 0) return Execute(redisClient => redisClient.Execute<T>("RPOP", Key), true);
        if (timeout > 0 && Redis.Timeout < (timeout + 1) * 1000) Redis.Timeout = (timeout + 1) * 1000;

        var result = Execute(redisClient => redisClient.Execute<IPacket[]>("BRPOP", Key, timeout), true);
        if (result == null || result.Length < 2) return default;

        return Redis.Encoder.Decode<T>(result[1]);
    }

    /// <summary>异步消费一个</summary>
    /// <param name="timeout">超时秒数。0 表示永久阻塞，负数表示不阻塞</param>
    /// <returns>元素</returns>
    public Task<T?> TakeOneAsync(Int32 timeout = 0) => TakeOneAsync(timeout, CancellationToken.None);

    /// <summary>异步消费一个</summary>
    /// <param name="timeout">超时秒数。0 表示永久阻塞，负数表示不阻塞</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>元素</returns>
    public async Task<T?> TakeOneAsync(Int32 timeout, CancellationToken cancellationToken)
    {
        if (timeout < 0) return await ExecuteAsync(redisClient => redisClient.ExecuteAsync<T>("RPOP", Key), true).ConfigureAwait(false);
        if (timeout > 0 && Redis.Timeout < (timeout + 1) * 1000) Redis.Timeout = (timeout + 1) * 1000;

        var result = await ExecuteAsync(redisClient => redisClient.ExecuteAsync<IPacket[]>("BRPOP", [Key, timeout], cancellationToken), true).ConfigureAwait(false);
        if (result == null || result.Length < 2) return default;

        return Redis.Encoder.Decode<T>(result[1]);
    }

    /// <summary>确认消费</summary>
    /// <param name="keys">键集合</param>
    /// <returns>确认数量</returns>
    public Int32 Acknowledge(params String[] keys) => 0;
}

#nullable restore