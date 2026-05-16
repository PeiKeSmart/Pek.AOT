#nullable enable

using Pek;
using Pek.Log;

namespace Pek.Caching;

/// <summary>Redis 延迟队列</summary>
/// <typeparam name="T">元素类型</typeparam>
public class RedisDelayQueue<T> : QueueBase, IProducerConsumer<T>
    where T : notnull
{
    /// <summary>转移延迟消息到目标队列的间隔，秒</summary>
    public Int32 TransferInterval { get; set; } = 10;

    /// <summary>默认延迟时间，秒</summary>
    public Int32 Delay { get; set; } = 60;

    /// <summary>个数</summary>
    public Int32 Count => _sortedSet.Count;

    /// <summary>是否为空</summary>
    public Boolean IsEmpty => Count == 0;

    private readonly RedisSortedSet<T> _sortedSet;

    /// <summary>实例化</summary>
    /// <param name="redis">Redis 实例</param>
    /// <param name="key">键</param>
    public RedisDelayQueue(Redis redis, String key) : base(redis, key)
    {
        var queueKey = redis is FullRedis fullRedis ? fullRedis.GetKey(key) : key;
        _sortedSet = new RedisSortedSet<T>(redis, queueKey);
    }

    /// <summary>添加延迟消息</summary>
    /// <param name="value">消息</param>
    /// <param name="delay">延迟秒数</param>
    /// <returns>结果</returns>
    public Int32 Add(T value, Int32 delay)
    {
        using var span = Redis.Tracer?.NewSpan($"redismq:{TraceName}:Add", value);
        try
        {
            var target = DateTime.UtcNow.ToInt() + delay;
            var result = 0;
            for (var i = 0; i <= RetryTimesWhenSendFailed; i++)
            {
                result = _sortedSet.Add(value, target);
                if (result >= 0) return result;

                span?.SetError(new InvalidOperationException($"发布到队列[{Topic}]失败！"), null);
                if (i < RetryTimesWhenSendFailed) Thread.Sleep(RetryIntervalWhenSendFailed);
            }

            ValidWhenSendFailed(span);
            return result;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }

    /// <summary>批量添加，使用默认延迟时间</summary>
    /// <param name="values">消息集合</param>
    /// <returns>结果</returns>
    public Int32 Add(params T[] values)
    {
        if (values == null || values.Length == 0) return 0;

        using var span = Redis.Tracer?.NewSpan($"redismq:{TraceName}:Add", values);
        try
        {
            var target = DateTime.UtcNow.ToInt() + Delay;
            var result = 0;
            for (var i = 0; i <= RetryTimesWhenSendFailed; i++)
            {
                result = _sortedSet.Add(values, target);
                if (result > 0) return result;

                span?.SetError(new InvalidOperationException($"发布到队列[{Topic}]失败！"), null);
                if (i < RetryTimesWhenSendFailed) Thread.Sleep(RetryIntervalWhenSendFailed);
            }

            ValidWhenSendFailed(span);
            return result;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }

    /// <summary>移除延迟项</summary>
    /// <param name="value">消息</param>
    /// <returns>结果</returns>
    public Int32 Remove(T value) => _sortedSet.Remove(value);

    /// <summary>获取一个</summary>
    /// <param name="timeout">超时秒数。0 表示最多等待 60 秒，负数表示立即返回</param>
    /// <returns>消息</returns>
    public T? TakeOne(Int32 timeout = 0)
    {
        if (timeout == 0) timeout = 60;

        while (true)
        {
            var score = DateTime.UtcNow.ToInt();
            var values = _sortedSet.RangeByScore(0, score, 0, 1);
            if (values.Length > 0 && TryPop(values[0])) return values[0];

            if (timeout <= 0) break;

            Thread.Sleep(1_000);
            timeout--;
        }

        return default;
    }

    /// <summary>异步获取一个</summary>
    /// <param name="timeout">超时秒数。0 表示最多等待 60 秒，负数表示立即返回</param>
    /// <returns>消息</returns>
    public Task<T?> TakeOneAsync(Int32 timeout = 0) => TakeOneAsync(timeout, CancellationToken.None);

    /// <summary>异步获取一个</summary>
    /// <param name="timeout">超时秒数。0 表示最多等待 60 秒，负数表示立即返回</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>消息</returns>
    public async Task<T?> TakeOneAsync(Int32 timeout, CancellationToken cancellationToken)
    {
        if (timeout == 0) timeout = 60;

        while (!cancellationToken.IsCancellationRequested)
        {
            var score = DateTime.UtcNow.ToInt();
            var values = await _sortedSet.RangeByScoreAsync(0, score, 0, 1, cancellationToken).ConfigureAwait(false);
            if (values.Length > 0 && TryPop(values[0])) return values[0];

            if (timeout <= 0) break;

            await Task.Delay(1_000, cancellationToken).ConfigureAwait(false);
            timeout--;
        }

        return default;
    }

    /// <summary>获取一批</summary>
    /// <param name="count">数量</param>
    /// <returns>消息集合</returns>
    public IEnumerable<T> Take(Int32 count = 1)
    {
        if (count <= 0) yield break;

        var score = DateTime.UtcNow.ToInt();
        var values = _sortedSet.RangeByScore(0, score, 0, count);
        foreach (var item in values)
        {
            if (TryPop(item)) yield return item;
        }
    }

    /// <summary>确认消费。不支持</summary>
    /// <param name="keys">键集合</param>
    /// <returns>结果</returns>
    public Int32 Acknowledge(params String[] keys) => -1;

    /// <summary>转移已到期消息到目标队列</summary>
    /// <param name="queue">目标队列</param>
    /// <param name="onException">异常回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public async Task TransferAsync(IProducerConsumer<T> queue, Action<Exception>? onException = null, CancellationToken cancellationToken = default)
    {
        DefaultSpan.Current = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            ISpan? span = null;
            try
            {
                var score = DateTime.UtcNow.ToInt();
                var values = await _sortedSet.RangeByScoreAsync(0, score, 0, 10, cancellationToken).ConfigureAwait(false);
                if (values.Length > 0)
                {
                    span = Redis.Tracer?.NewSpan($"redismq:{TraceName}:Transfer", values);

                    var list = new List<T>();
                    foreach (var item in values)
                    {
                        if (Remove(item) > 0) list.Add(item);
                    }

                    if (list.Count > 0) queue.Add([.. list]);
                }
                else
                {
                    await Task.Delay(TransferInterval * 1_000, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (ThreadAbortException)
            {
                break;
            }
            catch (ThreadInterruptedException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested) break;

                span?.SetError(ex, null);
                onException?.Invoke(ex);
            }
            finally
            {
                span?.Dispose();
            }
        }
    }

    private Boolean TryPop(T value) => _sortedSet.Remove(value) > 0;
}

#nullable restore