#nullable enable

using System.Diagnostics;
using System.Net;

using Pek;
using Pek.Extension;
using Pek.Log;
using Pek.Serialization;

namespace Pek.Caching;

/// <summary>可靠 Redis 队列</summary>
/// <typeparam name="T">元素类型</typeparam>
public class RedisReliableQueue<T> : QueueBase, IProducerConsumer<T>, IDisposable
    where T : notnull
{
    /// <summary>确认列表键</summary>
    public String AckKey { get; set; }

    /// <summary>重试确认间隔，秒</summary>
    public Int32 RetryInterval { get; set; } = 60;

    /// <summary>最小管道阈值</summary>
    public Int32 MinPipeline { get; set; } = 3;

    /// <summary>个数</summary>
    public Int32 Count => Execute(redisClient => redisClient.Execute<Int32>("LLEN", Key));

    /// <summary>是否为空</summary>
    public Boolean IsEmpty => Count == 0;

    /// <summary>消费状态</summary>
    public RedisQueueStatus Status => _status;

    private readonly String _queueKey;
    private readonly String _statusKey;
    private readonly RedisQueueStatus _status;

    private RedisDelayQueue<T>? _delay;
    private CancellationTokenSource? _source;
    private Task? _delayTask;
    private DateTime _nextRetry;

    /// <summary>实例化</summary>
    /// <param name="redis">Redis 实例</param>
    /// <param name="key">键</param>
    public RedisReliableQueue(Redis redis, String key) : base(redis, key)
    {
        _queueKey = redis is FullRedis fullRedis ? fullRedis.GetKey(key) : key;
        _status = CreateStatus();
        AckKey = $"{_queueKey}:Ack:{_status.Key}";
        _statusKey = $"{_queueKey}:Status:{_status.Key}";
    }

    /// <summary>释放</summary>
    public void Dispose()
    {
        _delay = null;
        _delayTask = null;

        if (_source != null)
        {
            try
            {
                _source.Cancel();
            }
            catch { }

            _source.Dispose();
            _source = null;
        }
    }

    /// <summary>批量生产添加</summary>
    /// <param name="values">消息集合</param>
    /// <returns>队列长度</returns>
    public Int32 Add(params T[] values)
    {
        if (values == null || values.Length == 0) return 0;

        using var span = Redis.Tracer?.NewSpan($"redismq:{TraceName}:Add", values);
        try
        {
            var args = new List<Object?> { Key };
            foreach (var item in values)
            {
                args.Add(item);
            }

            var result = 0;
            for (var i = 0; i <= RetryTimesWhenSendFailed; i++)
            {
                result = Execute(redisClient => redisClient.Execute<Int32>("LPUSH", [.. args]), true);
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

    /// <summary>消费一个</summary>
    /// <param name="timeout">超时秒数。0 表示永久阻塞，负数表示不阻塞</param>
    /// <returns>消息</returns>
    public T? TakeOne(Int32 timeout = 0)
    {
        RetryAck();
        if (timeout > 0 && Redis.Timeout < (timeout + 1) * 1_000) Redis.Timeout = (timeout + 1) * 1_000;

        var result = timeout >= 0
            ? Execute(redisClient => redisClient.Execute<T>("BRPOPLPUSH", Key, AckKey, timeout), true)
            : Execute(redisClient => redisClient.Execute<T>("RPOPLPUSH", Key, AckKey), true);

        if (result != null) _status.Consumes++;
        return result;
    }

    /// <summary>异步消费一个</summary>
    /// <param name="timeout">超时秒数。0 表示永久阻塞，负数表示不阻塞</param>
    /// <returns>消息</returns>
    public Task<T?> TakeOneAsync(Int32 timeout = 0) => TakeOneAsync(timeout, CancellationToken.None);

    /// <summary>异步消费一个</summary>
    /// <param name="timeout">超时秒数。0 表示永久阻塞，负数表示不阻塞</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>消息</returns>
    public async Task<T?> TakeOneAsync(Int32 timeout, CancellationToken cancellationToken)
    {
        RetryAck();
        if (timeout > 0 && Redis.Timeout < (timeout + 1) * 1_000) Redis.Timeout = (timeout + 1) * 1_000;

        var result = timeout < 0
            ? await ExecuteAsync(redisClient => redisClient.ExecuteAsync<T>("RPOPLPUSH", [Key, AckKey], cancellationToken), true).ConfigureAwait(false)
            : await ExecuteAsync(redisClient => redisClient.ExecuteAsync<T>("BRPOPLPUSH", [Key, AckKey, timeout], cancellationToken), true).ConfigureAwait(false);

        if (result != null) _status.Consumes++;
        return result;
    }

    /// <summary>批量消费</summary>
    /// <param name="count">数量</param>
    /// <returns>消息集合</returns>
    public IEnumerable<T> Take(Int32 count = 1)
    {
        if (count <= 0) yield break;

        RetryAck();
        if (count >= MinPipeline)
        {
            var redis = Redis;
            redis.StartPipeline();
            for (var i = 0; i < count; i++)
            {
                Execute(redisClient => redisClient.Execute<T>("RPOPLPUSH", Key, AckKey), true);
            }

            var results = redis.StopPipeline(true);
            if (results == null) yield break;

            foreach (var item in results)
            {
                if (item is null || Equals(item, default(T))) break;
                _status.Consumes++;
                yield return (T)item;
            }

            yield break;
        }

        for (var i = 0; i < count; i++)
        {
            var value = Execute(redisClient => redisClient.Execute<T>("RPOPLPUSH", Key, AckKey), true);
            if (value is null || Equals(value, default(T))) yield break;

            _status.Consumes++;
            yield return value;
        }
    }

    /// <summary>确认消费</summary>
    /// <param name="keys">消息键</param>
    /// <returns>确认数量</returns>
    public Int32 Acknowledge(params String[] keys)
    {
        var result = 0;
        _status.Acks += keys.Length;

        if (keys.Length >= MinPipeline)
        {
            var redis = Redis;
            redis.StartPipeline();
            foreach (var item in keys)
            {
                Execute(redisClient => redisClient.Execute<Int32>("LREM", AckKey, 1, item), true);
            }

            var results = redis.StopPipeline(true);
            if (results != null)
            {
                foreach (var item in results)
                {
                    result += item.ToInt();
                }
            }

            return result;
        }

        foreach (var item in keys)
        {
            result += Execute(redisClient => redisClient.Execute<Int32>("LREM", AckKey, 1, item), true);
        }

        return result;
    }

    /// <summary>初始化延迟队列</summary>
    /// <returns>延迟队列</returns>
    public RedisDelayQueue<T> InitDelay()
    {
        _delay ??= new RedisDelayQueue<T>(Redis, $"{Key}:Delay");
        if (_delayTask == null || _delayTask.IsCompleted)
        {
            _source?.Dispose();
            _source = new CancellationTokenSource();
            var delayQueue = _delay;
            var token = _source.Token;
            _delayTask = Task.Factory.StartNew(
                () => delayQueue.TransferAsync(this, null, token),
                token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap();
        }

        return _delay;
    }

    /// <summary>添加延迟消息</summary>
    /// <param name="value">消息</param>
    /// <param name="delay">延迟秒数</param>
    /// <returns>结果</returns>
    public Int32 AddDelay(T value, Int32 delay) => InitDelay().Add(value, delay);

    /// <summary>高级生产消息。消息体和值分离</summary>
    /// <param name="messages">消息字典</param>
    /// <param name="expire">消息体过期秒数</param>
    /// <returns>队列长度</returns>
    public Int32 Publish(IDictionary<String, T> messages, Int32 expire)
    {
        Redis.SetAll(messages, expire);

        var args = new List<Object?> { Key };
        foreach (var item in messages)
        {
            args.Add(item.Key);
        }

        return Execute(redisClient => redisClient.Execute<Int32>("LPUSH", [.. args]), true);
    }

    /// <summary>高级消费消息</summary>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <param name="func">处理函数</param>
    /// <param name="timeout">超时秒数</param>
    /// <returns>处理结果</returns>
    public async Task<TResult?> ConsumeAsync<TResult>(Func<T, Task<TResult>> func, Int32 timeout = 0)
    {
        RetryAck();

        var messageId = timeout < 0
            ? await ExecuteAsync(redisClient => redisClient.ExecuteAsync<String>("RPOPLPUSH", [Key, AckKey], CancellationToken.None), true).ConfigureAwait(false)
            : await ExecuteAsync(redisClient => redisClient.ExecuteAsync<String>("BRPOPLPUSH", [Key, AckKey, timeout], CancellationToken.None), true).ConfigureAwait(false);
        if (messageId.IsNullOrEmpty()) return default;

        _status.Consumes++;
        if (!Redis.TryGetValue(messageId, out T? message) || message == null)
        {
            Acknowledge(messageId);
            return default;
        }

        var result = await func(message).ConfigureAwait(false);
        Redis.Remove(messageId);
        Acknowledge(messageId);
        return result;
    }

    /// <summary>获取确认队列中的消息</summary>
    /// <param name="count">数量</param>
    /// <returns>消息键集合</returns>
    public IEnumerable<String> TakeAck(Int32 count = 1)
    {
        if (count <= 0) yield break;

        for (var i = 0; i < count; i++)
        {
            var value = Execute(redisClient => redisClient.Execute<String>("RPOP", AckKey), true);
            if (value == null) yield break;
            yield return value;
        }
    }

    /// <summary>清空所有确认队列</summary>
    /// <returns>清理数量</returns>
    public Int32 ClearAllAck()
    {
        if (Redis is not FullRedis redis) return 0;

        var keys = redis.Search($"{_queueKey}:Ack:*", 0, 1000).ToArray();
        return keys.Length > 0 ? redis.Remove(keys) : 0;
    }

    /// <summary>全局回滚确认队列中的死信</summary>
    /// <returns>回滚数量</returns>
    public Int32 RollbackAllAck()
    {
        if (Redis is not FullRedis redis) return 0;

        var count = 0;
        var ackKeys = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
        var statusPrefix = $"{_queueKey}:Status:";
        foreach (var key in redis.Search($"{_queueKey}:Status:*", 0, 1000))
        {
            var suffix = key.StartsWith(statusPrefix, StringComparison.OrdinalIgnoreCase) ? key[statusPrefix.Length..] : String.Empty;
            var ackKey = $"{_queueKey}:Ack:{suffix}";
            ackKeys.Add(ackKey);

            var status = redis.Get<RedisQueueStatus>(key);
            if (status != null && status.LastActive.AddSeconds(RetryInterval * 10) < DateTime.Now)
            {
                if (redis.ContainsKey(ackKey))
                {
                    redis.WriteLog("发现死信队列：{0}", ackKey);

                    var list = RollbackAck(_queueKey, ackKey);
                    foreach (var item in list)
                    {
                        redis.WriteLog("全局回滚死信：{0}", item);
                    }

                    count += list.Count;
                }

                redis.Remove(key);
                redis.WriteLog("删除队列状态：{0} {1}", key, status.ToJson());
            }
        }

        foreach (var key in redis.Search($"{_queueKey}:Ack:*", 0, 1000))
        {
            if (!ackKeys.Contains(key))
            {
                redis.WriteLog("全局清理死信：{0}", key);
                redis.Remove(key);
            }
        }

        return count;
    }

    private List<String> RollbackAck(String key, String ackKey)
    {
        var list = new List<String>();
        while (true)
        {
            var value = Execute(redisClient => redisClient.Execute<String>("RPOPLPUSH", ackKey, key), true);
            if (value == null) break;
            list.Add(value);
        }

        return list;
    }

    private void RetryAck()
    {
        var now = DateTime.Now;
        if (_nextRetry >= now) return;

        _nextRetry = now.AddSeconds(RetryInterval);
        var list = RollbackAck(_queueKey, AckKey);
        foreach (var item in list)
        {
            Redis.WriteLog("定时回滚死信：{0}", item);
        }

        UpdateStatus();
        if (Redis.Add($"{_queueKey}:AllStatus", _status, RetryInterval)) RollbackAllAck();
    }

    private static readonly RedisQueueStatus _default = new()
    {
        MachineName = Environment.MachineName,
        UserName = Environment.UserName,
        ProcessId = Process.GetCurrentProcess().Id,
        Ip = GetLocalIp(),
    };

    private RedisQueueStatus CreateStatus() => new()
    {
        Key = Guid.NewGuid().ToString("N")[..8],
        MachineName = _default.MachineName,
        UserName = _default.UserName,
        ProcessId = _default.ProcessId,
        Ip = _default.Ip,
        CreateTime = DateTime.Now,
        LastActive = DateTime.Now,
    };

    private void UpdateStatus()
    {
        _status.LastActive = DateTime.Now;
        Redis.Set(_statusKey, _status, 7 * 24 * 3600);
    }

    private static String GetLocalIp()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var address = host.AddressList.FirstOrDefault(item => item.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            return address?.ToString() ?? IPAddress.Loopback.ToString();
        }
        catch
        {
            return IPAddress.Loopback.ToString();
        }
    }
}

#nullable restore