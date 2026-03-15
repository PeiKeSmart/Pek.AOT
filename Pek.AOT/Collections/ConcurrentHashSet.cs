using System.Collections;
using System.Collections.Concurrent;

namespace Pek.Collections;

/// <summary>并行哈希集合</summary>
/// <typeparam name="T">元素类型</typeparam>
public class ConcurrentHashSet<T> : IEnumerable<T> where T : notnull
{
    private readonly ConcurrentDictionary<T, Byte> _dic = new();

    /// <summary>是否空集合</summary>
    public Boolean IsEmpty => _dic.IsEmpty;

    /// <summary>元素个数</summary>
    public Int32 Count => _dic.Count;

    /// <summary>是否包含元素</summary>
    /// <param name="item">元素</param>
    /// <returns>是否存在</returns>
    [Obsolete("Use Contains instead")]
    public Boolean Contain(T item) => _dic.ContainsKey(item);

    /// <summary>是否包含元素</summary>
    /// <param name="item">元素</param>
    /// <returns>是否存在</returns>
    public Boolean Contains(T item) => _dic.ContainsKey(item);

    /// <summary>尝试添加</summary>
    /// <param name="item">元素</param>
    /// <returns>是否成功加入</returns>
    public Boolean TryAdd(T item) => _dic.TryAdd(item, 0);

    /// <summary>尝试删除</summary>
    /// <param name="item">元素</param>
    /// <returns>是否成功删除</returns>
    public Boolean TryRemove(T item) => _dic.TryRemove(item, out _);

    /// <summary>枚举集合元素</summary>
    /// <returns>枚举器</returns>
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => _dic.Keys.GetEnumerator();

    /// <summary>枚举集合元素</summary>
    /// <returns>枚举器</returns>
    IEnumerator IEnumerable.GetEnumerator() => _dic.Keys.GetEnumerator();
}