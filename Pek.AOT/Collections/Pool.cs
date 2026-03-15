using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Pek.Collections;

/// <summary>轻量级对象池。数组无锁实现，高性能</summary>
/// <remarks>
/// 文档 https://newlifex.com/core/object_pool
///
/// 设计说明：
/// - 存储结构采用 1 + N：热点槽位 <see cref="_current"/> 放置最热对象，内部数组存其余对象；
/// - 使用 <see cref="Interlocked.CompareExchange{T}(ref T, T, T)"/> 对热点槽位与数组元素进行无锁并发抢占/归还；
/// - 数组查找 O(N)，且为结构体持有引用，避免对象级别的额外分配；
/// - 通过延迟初始化避免冷启动分配；
/// - 可选择在二代 GC 触发时定期清理，降低长时间闲置的内存占用（限速 60 秒一次）。
///
/// AOT 约束：
/// - 上游默认通过反射创建对象；
/// - Pek.AOT 保留同样的池结构，但默认不走反射创建；
/// - 需要创建对象时，请在派生类重写 <see cref="OnCreate"/>，或通过工厂构造函数传入创建委托。
/// </remarks>
/// <typeparam name="T">池化的引用类型</typeparam>
public class Pool<T> : IPool<T> where T : class
{
    private Item[]? _items;
    private T? _current;
    private readonly Object _sync = new();
    private readonly Func<T?>? _factory;
    private Int64 _next;

    struct Item
    {
        public T? Value;
    }

    /// <summary>对象池大小</summary>
    public Int32 Max { get; set; }

    /// <summary>实例化对象池</summary>
    /// <param name="max">最大对象数</param>
    /// <param name="factory">工厂委托</param>
    public Pool(Int32 max = 0, Func<T?>? factory = null)
    {
        if (max <= 0) max = Environment.ProcessorCount * 2;
        if (max < 8) max = 8;

        Max = max;
        _factory = factory;
    }

    /// <summary>实例化对象池，并在 GC 第二代触发时尝试清理</summary>
    /// <param name="max">最大对象数</param>
    /// <param name="useGcClear">是否启用 GC 清理</param>
    protected Pool(Int32 max, Boolean useGcClear) : this(max)
    {
        if (useGcClear) Gen2GcCallback.Register(s => (s as Pool<T>)!.OnGen2(), this);
    }

    /// <summary>实例化对象池，并在 GC 第二代触发时尝试清理</summary>
    /// <param name="max">最大对象数</param>
    /// <param name="useGcClear">是否启用 GC 清理</param>
    /// <param name="factory">工厂委托</param>
    protected Pool(Int32 max, Boolean useGcClear, Func<T?>? factory = null) : this(max, factory)
    {
        if (useGcClear) Gen2GcCallback.Register(s => (s as Pool<T>)!.OnGen2(), this);
    }

    private Boolean OnGen2()
    {
        var now = Runtime.TickCount64;
        if (_next <= 0)
            _next = now + 60000;
        else if (_next < now)
        {
            Clear();
            _next = now + 60000;
        }

        return true;
    }

    [MemberNotNull(nameof(_items))]
    private Item[] Init()
    {
        if (_items == null)
        {
            lock (_sync)
            {
                _items ??= new Item[Max - 1];
            }
        }

        return _items;
    }

    /// <summary>获取一个实例</summary>
    /// <returns>对象实例</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual T Get()
    {
        var val = _current;
        if (val != null && Interlocked.CompareExchange(ref _current, null, val) == val) return val;

        var items = Init();
        for (var i = 0; i < items.Length; i++)
        {
            val = items[i].Value;
            if (val != null && Interlocked.CompareExchange(ref items[i].Value, null, val) == val) return val;
        }

        var rs = OnCreate();
        if (rs == null) throw new InvalidOperationException($"[Pool] Unable to create an instance of [{typeof(T).FullName}]");

        return rs;
    }

    /// <summary>归还实例</summary>
    /// <param name="value">对象实例</param>
    /// <returns>是否归还成功</returns>
    [Obsolete("Please use Return from 2024-02-01")]
    public virtual Boolean Put(T value) => Return(value);

    /// <summary>归还实例</summary>
    /// <param name="value">对象实例</param>
    /// <returns>是否归还成功</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual Boolean Return(T value)
    {
        if (_current == null && Interlocked.CompareExchange(ref _current, value, null) == null) return true;

        var items = Init();
        for (var i = 0; i < items.Length; ++i)
        {
            if (Interlocked.CompareExchange(ref items[i].Value, value, null) == null) return true;
        }

        return false;
    }

    /// <summary>清空对象池</summary>
    /// <returns>清理数量</returns>
    public virtual Int32 Clear()
    {
        var count = 0;

        if (_current != null)
        {
            _current = null;
            count++;
        }

        var items = _items;
        if (items == null) return count;

        for (var i = 0; i < items.Length; ++i)
        {
            if (items[i].Value != null)
            {
                items[i].Value = null;
                count++;
            }
        }

        _items = null;
        return count;
    }

    /// <summary>创建实例</summary>
    /// <returns>新实例</returns>
    /// <remarks>
    /// Pek.AOT 默认不走反射创建。
    /// 如需创建实例，请在派生类中重写，或通过工厂构造函数传入创建委托。
    /// </remarks>
    protected virtual T? OnCreate() => _factory != null ? _factory() : null;
}