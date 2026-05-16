#nullable enable

using Pek.Data;
using Pek.Extension;

namespace Pek.Caching;

/// <summary>Redis 有序集合</summary>
/// <typeparam name="T">元素类型</typeparam>
public class RedisSortedSet<T> : RedisBase
    where T : notnull
{
    /// <summary>实例化</summary>
    /// <param name="redis">Redis 实例</param>
    /// <param name="key">键</param>
    public RedisSortedSet(Redis redis, String key) : base(redis, key) { }

    /// <summary>个数</summary>
    public Int32 Count => Execute(redisClient => redisClient.Execute<Int32>("ZCARD", Key));

    /// <summary>添加元素</summary>
    /// <param name="member">元素</param>
    /// <param name="score">分数</param>
    /// <returns>新增数量</returns>
    public Int32 Add(T member, Double score) => Execute(redisClient => redisClient.Execute<String>("ZADD", Key, score, member), true).ToInt(-1);

    /// <summary>添加元素</summary>
    /// <param name="member">元素</param>
    /// <param name="score">分数</param>
    /// <returns>新增数量</returns>
    public Int32 Add(T member, Int64 score) => Execute(redisClient => redisClient.Execute<String>("ZADD", Key, score, member), true).ToInt(-1);

    /// <summary>批量添加</summary>
    /// <param name="members">元素集合</param>
    /// <param name="score">统一分数</param>
    /// <returns>新增数量</returns>
    public Int32 Add(IEnumerable<T> members, Double score)
    {
        var args = new List<Object?> { Key };
        foreach (var item in members)
        {
            args.Add(score);
            args.Add(item);
        }

        return Execute(redisClient => redisClient.Execute<String>("ZADD", [.. args]), true).ToInt(-1);
    }

    /// <summary>批量添加</summary>
    /// <param name="members">元素集合</param>
    /// <param name="score">统一分数</param>
    /// <returns>新增数量</returns>
    public Int32 Add(IEnumerable<T> members, Int64 score)
    {
        var args = new List<Object?> { Key };
        foreach (var item in members)
        {
            args.Add(score);
            args.Add(item);
        }

        return Execute(redisClient => redisClient.Execute<String>("ZADD", [.. args]), true).ToInt(-1);
    }

    /// <summary>按选项批量添加</summary>
    /// <param name="options">选项，如 XX/NX/CH/INCR</param>
    /// <param name="members">成员及分数</param>
    /// <returns>结果</returns>
    public Double Add(String options, IDictionary<T, Double> members)
    {
        var args = new List<Object?> { Key };
        if (!options.IsNullOrEmpty() && options.EqualIgnoreCase("XX", "NX", "CH", "INCR")) args.Add(options);

        foreach (var item in members)
        {
            args.Add(item.Value);
            args.Add(item.Key);
        }

        return Execute(redisClient => redisClient.Execute<Double>("ZADD", [.. args]), true);
    }

    /// <summary>删除元素</summary>
    /// <param name="members">元素集合</param>
    /// <returns>删除数量</returns>
    public Int32 Remove(params T[] members)
    {
        var args = new List<Object?> { Key };
        foreach (var item in members)
        {
            args.Add(item);
        }

        if (args.Count == 1) return 0;
        return Execute(redisClient => redisClient.Execute<Int32>("ZREM", [.. args]), true);
    }

    /// <summary>获取元素分数</summary>
    /// <param name="member">元素</param>
    /// <returns>分数</returns>
    public Double GetScore(T member) => Execute(redisClient => redisClient.Execute<Double>("ZSCORE", Key, member));

    /// <summary>递增分数</summary>
    /// <param name="member">元素</param>
    /// <param name="score">增量</param>
    /// <returns>新分数</returns>
    public Double Increment(T member, Double score) => Execute(redisClient => redisClient.Execute<Double>("ZINCRBY", Key, score, member), true);

    /// <summary>弹出最高分元素</summary>
    /// <param name="count">个数</param>
    /// <returns>元素和分数字典</returns>
    public IDictionary<T, Double> PopMax(Int32 count = 1)
    {
        var result = Execute(redisClient => redisClient.Execute<Object[]>("ZPOPMAX", Key, count), true);
        return DecodeScorePairs(result);
    }

    /// <summary>弹出最低分元素</summary>
    /// <param name="count">个数</param>
    /// <returns>元素和分数字典</returns>
    public IDictionary<T, Double> PopMin(Int32 count = 1)
    {
        var result = Execute(redisClient => redisClient.Execute<Object[]>("ZPOPMIN", Key, count), true);
        return DecodeScorePairs(result);
    }

    /// <summary>查找分数区间内的数量</summary>
    /// <param name="min">最小分数</param>
    /// <param name="max">最大分数</param>
    /// <returns>数量</returns>
    public Int32 FindCount(Double min, Double max) => Execute(redisClient => redisClient.Execute<Int32>("ZCOUNT", Key, min, max));

    /// <summary>按位置获取成员</summary>
    /// <param name="start">开始位置</param>
    /// <param name="stop">结束位置</param>
    /// <returns>成员数组</returns>
    public T[] Range(Int32 start, Int32 stop) => DecodeArray<T>(Execute(redisClient => redisClient.Execute<Object[]>("ZRANGE", Key, start, stop)));

    /// <summary>按位置获取成员和分数</summary>
    /// <param name="start">开始位置</param>
    /// <param name="stop">结束位置</param>
    /// <returns>成员和分数字典</returns>
    public IDictionary<T, Double> RangeWithScores(Int32 start, Int32 stop) => DecodeScorePairs(Execute(redisClient => redisClient.Execute<Object[]>("ZRANGE", Key, start, stop, "WITHSCORES")));

    /// <summary>按分数区间获取成员</summary>
    /// <param name="min">最小分数</param>
    /// <param name="max">最大分数</param>
    /// <param name="offset">偏移</param>
    /// <param name="count">数量</param>
    /// <returns>成员数组</returns>
    public T[] RangeByScore(Double min, Double max, Int32 offset, Int32 count) => DecodeArray<T>(Execute(redisClient => redisClient.Execute<Object[]>("ZRANGEBYSCORE", Key, min, max, "LIMIT", offset, count)));

    /// <summary>按分数区间异步获取成员</summary>
    /// <param name="min">最小分数</param>
    /// <param name="max">最大分数</param>
    /// <param name="offset">偏移</param>
    /// <param name="count">数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成员数组</returns>
    public async Task<T[]> RangeByScoreAsync(Double min, Double max, Int32 offset, Int32 count, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(redisClient => redisClient.ExecuteAsync<Object[]>("ZRANGEBYSCORE", [Key, min, max, "LIMIT", offset, count], cancellationToken)).ConfigureAwait(false);
        return DecodeArray<T>(result);
    }

    /// <summary>按分数区间获取成员和分数</summary>
    /// <param name="min">最小分数</param>
    /// <param name="max">最大分数</param>
    /// <param name="offset">偏移</param>
    /// <param name="count">数量</param>
    /// <returns>成员和分数字典</returns>
    public IDictionary<T, Double> RangeByScoreWithScores(Double min, Double max, Int32 offset, Int32 count) => DecodeScorePairs(Execute(redisClient => redisClient.Execute<Object[]>("ZRANGEBYSCORE", Key, min, max, "WITHSCORES", "LIMIT", offset, count)));

    /// <summary>获取排名</summary>
    /// <param name="member">元素</param>
    /// <returns>排名</returns>
    public Int32 Rank(T member) => Execute(redisClient => redisClient.Execute<Int32>("ZRANK", Key, member));

    /// <summary>搜索</summary>
    /// <param name="pattern">匹配模式</param>
    /// <param name="count">数量</param>
    /// <param name="position">游标</param>
    /// <returns>成员和分数字典</returns>
    public virtual IEnumerable<KeyValuePair<T, Double>> Search(String pattern, Int32 count, Int32 position = 0)
    {
        while (count > 0)
        {
            var result = Execute(redisClient => redisClient.Execute<Object[]>("ZSCAN", Key, position, "MATCH", pattern + String.Empty, "COUNT", count));
            if (result == null || result.Length != 2) yield break;

            position = 0;
            if (result[0] is IPacket packet)
                position = packet.ToStr().ToInt();
            else if (result[0] != null)
                position = result[0].ToString().ToInt();

            if (result[1] is not Object[] items) yield break;

            for (var i = 0; i + 1 < items.Length; i += 2)
            {
                if (count-- <= 0) yield break;

                var member = Decode<T>(items[i]);
                if (member == null) continue;

                var score = items[i + 1] is IPacket scorePacket ? scorePacket.ToStr().ToDouble() : items[i + 1].ToDouble();
                yield return new KeyValuePair<T, Double>(member, score);
            }

            if (position == 0) yield break;
        }
    }

    private IDictionary<T, Double> DecodeScorePairs(Object[]? values)
    {
        var result = new Dictionary<T, Double>();
        if (values == null || values.Length == 0) return result;

        for (var i = 0; i + 1 < values.Length; i += 2)
        {
            var member = Decode<T>(values[i]);
            if (member == null) continue;

            var score = values[i + 1] is IPacket packet ? packet.ToStr().ToDouble() : values[i + 1].ToDouble();
            result[member] = score;
        }

        return result;
    }
}

#nullable restore