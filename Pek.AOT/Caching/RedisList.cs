#nullable enable

using System.Collections;

namespace Pek.Caching;

/// <summary>Redis 列表，右边进入</summary>
/// <typeparam name="T">元素类型</typeparam>
public class RedisList<T> : RedisBase, IList<T>
{
    /// <summary>实例化</summary>
    /// <param name="redis">Redis 实例</param>
    /// <param name="key">键</param>
    public RedisList(Redis redis, String key) : base(redis, key) { }

    /// <summary>获取或设置指定位置的值</summary>
    /// <param name="index">索引</param>
    /// <returns>值</returns>
    public T this[Int32 index]
    {
        get => Execute(redisClient => redisClient.Execute<T>("LINDEX", Key, index))!;
        set => Execute(redisClient => redisClient.Execute<String>("LSET", Key, index, value), true);
    }

    /// <summary>个数</summary>
    public Int32 Count => Execute(redisClient => redisClient.Execute<Int32>("LLEN", Key));

    Boolean ICollection<T>.IsReadOnly => false;

    /// <summary>追加</summary>
    /// <param name="item">元素</param>
    public void Add(T item) => RPUSH([item]);

    /// <summary>批量追加</summary>
    /// <param name="values">元素集合</param>
    /// <returns>总数</returns>
    public Int32 AddRange(IEnumerable<T> values) => RPUSH(values);

    /// <summary>清空</summary>
    public void Clear() => Redis.Remove(Key);

    /// <summary>是否包含元素</summary>
    /// <param name="item">元素</param>
    /// <returns>是否包含</returns>
    public Boolean Contains(T item) => IndexOf(item) >= 0;

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

    /// <summary>查找索引</summary>
    /// <param name="item">元素</param>
    /// <returns>索引</returns>
    public Int32 IndexOf(T item)
    {
        var values = GetAll();
        return Array.IndexOf(values, item);
    }

    /// <summary>插入元素</summary>
    /// <param name="index">索引</param>
    /// <param name="item">元素</param>
    public void Insert(Int32 index, T item)
    {
        if (index < 0 || index > Count) throw new ArgumentOutOfRangeException(nameof(index));
        if (index == Count)
        {
            Add(item);
            return;
        }

        LInsertBefore(this[index], item);
    }

    /// <summary>移除元素</summary>
    /// <param name="item">元素</param>
    /// <returns>是否成功</returns>
    public Boolean Remove(T item) => LRem(1, item) > 0;

    /// <summary>移除指定位置元素</summary>
    /// <param name="index">索引</param>
    public void RemoveAt(Int32 index) => Remove(this[index]);

    /// <summary>遍历</summary>
    /// <returns>枚举器</returns>
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)GetAll()).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>右边批量添加</summary>
    /// <param name="values">元素集合</param>
    /// <returns>总数</returns>
    public Int32 RPUSH(IEnumerable<T> values)
    {
        var args = new List<Object?> { Key };
        foreach (var item in values)
        {
            args.Add(item);
        }

        if (args.Count == 1) return Count;
        return Execute(redisClient => redisClient.Execute<Int32>("RPUSH", [.. args]), true);
    }

    /// <summary>左边批量添加</summary>
    /// <param name="values">元素集合</param>
    /// <returns>总数</returns>
    public Int32 LPUSH(IEnumerable<T> values)
    {
        var args = new List<Object?> { Key };
        foreach (var item in values)
        {
            args.Add(item);
        }

        if (args.Count == 1) return Count;
        return Execute(redisClient => redisClient.Execute<Int32>("LPUSH", [.. args]), true);
    }

    /// <summary>弹出最右元素</summary>
    /// <returns>元素</returns>
    public T? RPOP() => Execute(redisClient => redisClient.Execute<T>("RPOP", Key), true);

    /// <summary>弹出最左元素</summary>
    /// <returns>元素</returns>
    public T? LPOP() => Execute(redisClient => redisClient.Execute<T>("LPOP", Key), true);

    /// <summary>在指定元素前插入</summary>
    /// <param name="pivot">参考元素</param>
    /// <param name="value">新元素</param>
    /// <returns>结果</returns>
    public Int32 LInsertBefore(T pivot, T value) => Execute(redisClient => redisClient.Execute<Int32>("LINSERT", Key, "BEFORE", pivot, value), true);

    /// <summary>在指定元素后插入</summary>
    /// <param name="pivot">参考元素</param>
    /// <param name="value">新元素</param>
    /// <returns>结果</returns>
    public Int32 LInsertAfter(T pivot, T value) => Execute(redisClient => redisClient.Execute<Int32>("LINSERT", Key, "AFTER", pivot, value), true);

    /// <summary>获取范围</summary>
    /// <param name="start">开始位置</param>
    /// <param name="stop">结束位置</param>
    /// <returns>元素数组</returns>
    public T[] LRange(Int32 start, Int32 stop) => DecodeArray<T>(Execute(redisClient => redisClient.Execute<Object[]>("LRANGE", Key, start, stop)));

    /// <summary>获取全部元素</summary>
    /// <returns>元素数组</returns>
    public T[] GetAll() => LRange(0, -1);

    /// <summary>修剪列表</summary>
    /// <param name="start">开始位置</param>
    /// <param name="stop">结束位置</param>
    /// <returns>是否成功</returns>
    public Boolean LTrim(Int32 start, Int32 stop) => Execute(redisClient => redisClient.Execute<String>("LTRIM", Key, start, stop), true) == "OK";

    /// <summary>移除元素</summary>
    /// <param name="count">次数</param>
    /// <param name="value">元素</param>
    /// <returns>移除数量</returns>
    public Int32 LRem(Int32 count, T value) => Execute(redisClient => redisClient.Execute<Int32>("LREM", Key, count, value), true);
}

#nullable restore