#nullable enable

using System.Collections;
using System.Collections.Generic;

namespace Pek.Caching;

/// <summary>Redis 哈希结构</summary>
/// <typeparam name="TKey">字段类型</typeparam>
/// <typeparam name="TValue">值类型</typeparam>
public class RedisHash<TKey, TValue> : RedisBase, IDictionary<TKey, TValue>
    where TKey : notnull
{
    /// <summary>实例化</summary>
    /// <param name="redis">Redis 实例</param>
    /// <param name="key">键</param>
    public RedisHash(Redis redis, String key) : base(redis, key) { }

    /// <summary>个数</summary>
    public Int32 Count => Execute(redisClient => redisClient.Execute<Int32>("HLEN", Key));

    Boolean ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;

    /// <summary>全部字段</summary>
    public ICollection<TKey> Keys => DecodeArray<TKey>(Execute(redisClient => redisClient.Execute<Object[]>("HKEYS", Key)));

    /// <summary>全部值</summary>
    public ICollection<TValue> Values => DecodeArray<TValue>(Execute(redisClient => redisClient.Execute<Object[]>("HVALS", Key)));

    /// <summary>获取或设置字段值</summary>
    /// <param name="key">字段</param>
    /// <returns>值</returns>
    public TValue this[TKey key]
    {
        get => Execute(redisClient => redisClient.Execute<TValue>("HGET", Key, key))!;
        set => Execute(redisClient => redisClient.Execute<Int32>("HSET", Key, key, value), true);
    }

    /// <summary>是否包含字段</summary>
    /// <param name="key">字段</param>
    /// <returns>是否包含</returns>
    public Boolean ContainsKey(TKey key) => Execute(redisClient => redisClient.Execute<Int32>("HEXISTS", Key, key)) > 0;

    /// <summary>添加字段</summary>
    /// <param name="key">字段</param>
    /// <param name="value">值</param>
    public void Add(TKey key, TValue value) => Execute(redisClient => redisClient.Execute<Int32>("HSET", Key, key, value), true);

    /// <summary>移除字段</summary>
    /// <param name="key">字段</param>
    /// <returns>是否成功</returns>
    public Boolean Remove(TKey key) => Execute(redisClient => redisClient.Execute<Int32>("HDEL", Key, key), true) > 0;

    /// <summary>尝试获取字段</summary>
    /// <param name="key">字段</param>
    /// <param name="value">值</param>
    /// <returns>是否成功</returns>
    public Boolean TryGetValue(TKey key, out TValue value)
    {
        var result = Execute(redisClient => redisClient.Execute("HGET", Key, key));
        if (result == null)
        {
            value = default!;
            return false;
        }

        value = Decode<TValue>(result)!;
        return true;
    }

    /// <summary>清空</summary>
    public void Clear() => Redis.Remove(Key);

    void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

    Boolean ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item)
    {
        if (!TryGetValue(item.Key, out var value)) return false;
        return EqualityComparer<TValue>.Default.Equals(value, item.Value);
    }

    void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, Int32 arrayIndex)
    {
        foreach (var item in this)
        {
            array[arrayIndex++] = item;
        }
    }

    Boolean ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item) => Remove(item.Key);

    /// <summary>获取全部字段和值</summary>
    /// <returns>结果字典</returns>
    public IDictionary<TKey, TValue?> GetAll()
    {
        var result = new Dictionary<TKey, TValue?>();
        var values = Execute(redisClient => redisClient.Execute<Object[]>("HGETALL", Key));
        if (values == null || values.Length == 0) return result;

        for (var i = 0; i + 1 < values.Length; i += 2)
        {
            var field = Decode<TKey>(values[i]);
            if (field == null) continue;

            result[field] = Decode<TValue>(values[i + 1]);
        }

        return result;
    }

    /// <summary>批量设置</summary>
    /// <param name="values">字段和值</param>
    /// <returns>是否成功</returns>
    public Boolean HMSet(IEnumerable<KeyValuePair<TKey, TValue>> values)
    {
        var args = new List<Object?> { Key };
        foreach (var item in values)
        {
            args.Add(item.Key);
            args.Add(item.Value);
        }

        return Execute(redisClient => redisClient.Execute<String>("HMSET", [.. args]), true) == "OK";
    }

    /// <summary>批量获取</summary>
    /// <param name="fields">字段集合</param>
    /// <returns>结果数组</returns>
    public TValue[] HMGet(params TKey[] fields)
    {
        var args = new List<Object?> { Key };
        foreach (var item in fields)
        {
            args.Add(item);
        }

        return DecodeArray<TValue>(Execute(redisClient => redisClient.Execute<Object[]>("HMGET", [.. args])));
    }

    /// <summary>遍历</summary>
    /// <returns>枚举器</returns>
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        foreach (var item in GetAll())
        {
            yield return new KeyValuePair<TKey, TValue>(item.Key, item.Value!);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

#nullable restore