using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Pek.Collections;

/// <summary>定长并发队列，超出限制时自动出队旧元素</summary>
/// <typeparam name="T">元素类型</typeparam>
public class ConcurrentLimitedQueue<T> : ConcurrentQueue<T>
{
    /// <summary>长度限制</summary>
    public Int32 Limit { get; set; }

    /// <summary>实例化定长并发队列</summary>
    /// <param name="limit">最大长度</param>
    public ConcurrentLimitedQueue(Int32 limit)
    {
        Limit = limit;
    }

    /// <summary>使用集合初始化定长并发队列</summary>
    /// <param name="list">初始集合</param>
    public ConcurrentLimitedQueue(IEnumerable<T> list) : base(list)
    {
        Limit = list.Count();
    }

    /// <summary>从 List 隐式转换</summary>
    /// <param name="list">源列表</param>
    public static implicit operator ConcurrentLimitedQueue<T>(List<T> list) => new(list);

    /// <summary>入队，超出限制时自动出队</summary>
    /// <param name="item">元素</param>
    public new void Enqueue(T item)
    {
        if (Count >= Limit) TryDequeue(out _);

        base.Enqueue(item);
    }
}
