#nullable enable

using System.Collections;

namespace Pek.Caching;

/// <summary>Redis 集合结构</summary>
/// <typeparam name="T">元素类型</typeparam>
public class RedisSet<T> : RedisBase, ICollection<T>
{
    /// <summary>实例化</summary>
    /// <param name="redis">Redis 实例</param>
    /// <param name="key">键</param>
    public RedisSet(Redis redis, String key) : base(redis, key) { }

    /// <summary>个数</summary>
    public Int32 Count => Execute(redisClient => redisClient.Execute<Int32>("SCARD", Key));

    Boolean ICollection<T>.IsReadOnly => false;

    /// <summary>添加</summary>
    /// <param name="item">元素</param>
    public void Add(T item) => SAdd(item);

    /// <summary>清空</summary>
    public void Clear() => Redis.Remove(Key);

    /// <summary>是否包含</summary>
    /// <param name="item">元素</param>
    /// <returns>是否包含</returns>
    public Boolean Contains(T item) => Execute(redisClient => redisClient.Execute<Int32>("SISMEMBER", Key, item)) > 0;

    /// <summary>复制到数组</summary>
    /// <param name="array">目标数组</param>
    /// <param name="arrayIndex">起始位置</param>
    public void CopyTo(T[] array, Int32 arrayIndex)
    {
        foreach (var item in GetAll())
        {
            array[arrayIndex++] = item;
        }
    }

    /// <summary>移除</summary>
    /// <param name="item">元素</param>
    /// <returns>是否成功</returns>
    public Boolean Remove(T item) => SDel(item) > 0;

    /// <summary>遍历</summary>
    /// <returns>枚举器</returns>
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)GetAll()).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>批量添加</summary>
    /// <param name="members">元素集合</param>
    /// <returns>新增数量</returns>
    public Int32 SAdd(params T[] members)
    {
        var args = new List<Object?> { Key };
        foreach (var item in members)
        {
            args.Add(item);
        }

        if (args.Count == 1) return 0;
        return Execute(redisClient => redisClient.Execute<Int32>("SADD", [.. args]), true);
    }

    /// <summary>批量删除</summary>
    /// <param name="members">元素集合</param>
    /// <returns>删除数量</returns>
    public Int32 SDel(params T[] members)
    {
        var args = new List<Object?> { Key };
        foreach (var item in members)
        {
            args.Add(item);
        }

        if (args.Count == 1) return 0;
        return Execute(redisClient => redisClient.Execute<Int32>("SREM", [.. args]), true);
    }

    /// <summary>获取全部元素</summary>
    /// <returns>元素数组</returns>
    public T[] GetAll() => DecodeArray<T>(Execute(redisClient => redisClient.Execute<Object[]>("SMEMBERS", Key)));
}

#nullable restore