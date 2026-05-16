using System.Diagnostics.CodeAnalysis;

using Pek.Messaging;

namespace Pek.Caching;

/// <summary>缓存基类</summary>
public abstract class Cache : DisposeBase, ICache, IEventBusFactory
{
    /// <summary>默认缓存</summary>
    public static ICache Default { get; set; } = new MemoryCache();

    /// <summary>名称</summary>
    public String Name { get; set; }

    /// <summary>默认过期时间</summary>
    public Int32 Expire { get; set; }

    /// <summary>缓存项</summary>
    public virtual Object? this[String key]
    {
        get => Get<Object>(key);
        set => Set(key, value);
    }

    /// <summary>缓存个数</summary>
    public abstract Int32 Count { get; }

    /// <summary>所有键</summary>
    public abstract ICollection<String> Keys { get; }

    /// <summary>构造函数</summary>
    protected Cache()
    {
        var name = GetType().Name;
        Name = name.EndsWith("Cache", StringComparison.Ordinal) ? name[..^5] : name;
    }

    /// <summary>初始化</summary>
    /// <param name="config">配置</param>
    public virtual void Init(String? config) { }

    /// <summary>是否包含键</summary>
    public abstract Boolean ContainsKey(String key);

    /// <summary>设置缓存项</summary>
    public abstract Boolean Set<T>(String key, T value, Int32 expire = -1);

    /// <summary>设置缓存项</summary>
    public virtual Boolean Set<T>(String key, T value, TimeSpan expire) => Set(key, value, (Int32)expire.TotalSeconds);

    /// <summary>获取缓存项</summary>
    [return: MaybeNull]
    public abstract T Get<T>(String key);

    /// <summary>移除缓存项</summary>
    public abstract Int32 Remove(String key);

    /// <summary>批量移除缓存项</summary>
    public abstract Int32 Remove(params String[] keys);

    /// <summary>清空</summary>
    public virtual void Clear() => throw new NotSupportedException();

    /// <summary>设置过期时间</summary>
    public abstract Boolean SetExpire(String key, TimeSpan expire);

    /// <summary>获取过期时间</summary>
    public abstract TimeSpan GetExpire(String key);

    /// <summary>批量获取</summary>
    public virtual IDictionary<String, T?> GetAll<T>(IEnumerable<String> keys)
    {
        var rs = new Dictionary<String, T?>();
        foreach (var item in keys)
        {
            rs[item] = Get<T>(item);
        }

        return rs;
    }

    /// <summary>批量设置</summary>
    public virtual void SetAll<T>(IDictionary<String, T> values, Int32 expire = -1)
    {
        foreach (var item in values)
        {
            Set(item.Key, item.Value, expire);
        }
    }

    /// <summary>获取列表</summary>
    public virtual IList<T> GetList<T>(String key) => throw new NotSupportedException();

    /// <summary>获取字典</summary>
    public virtual IDictionary<String, T> GetDictionary<T>(String key) => throw new NotSupportedException();

    /// <summary>获取队列</summary>
    public virtual IProducerConsumer<T> GetQueue<T>(String key) => throw new NotSupportedException();

    /// <summary>获取栈</summary>
    public virtual IProducerConsumer<T> GetStack<T>(String key) => throw new NotSupportedException();

    /// <summary>获取集合</summary>
    public virtual ICollection<T> GetSet<T>(String key) => throw new NotSupportedException();

    /// <summary>创建事件总线</summary>
    public virtual IEventBus<TEvent> CreateEventBus<TEvent>(String topic, String clientId = "") => new QueueEventBus<TEvent>(this, topic);

    /// <summary>添加</summary>
    public virtual Boolean Add<T>(String key, T value, Int32 expire = -1)
    {
        if (ContainsKey(key)) return false;

        return Set(key, value, expire);
    }

    /// <summary>替换并返回旧值</summary>
    [return: MaybeNull]
    public virtual T Replace<T>(String key, T value)
    {
        var rs = Get<T>(key);
        Set(key, value);
        return rs;
    }

    /// <summary>尝试获取指定键</summary>
    public virtual Boolean TryGetValue<T>(String key, [MaybeNull] out T value)
    {
        value = Get<T>(key)!;
        if (!Equals(value, default(T))) return true;

        return ContainsKey(key);
    }

    /// <summary>获取或添加</summary>
    [return: MaybeNull]
    public virtual T GetOrAdd<T>(String key, Func<String, T> callback, Int32 expire = -1)
    {
        var value = Get<T>(key);
        if (!Equals(value, default(T))) return value;
        if (ContainsKey(key)) return value;

        value = callback(key);
        if (expire < 0) expire = Expire;

        if (Add(key, value, expire)) return value;
        return Get<T>(key);
    }

    /// <summary>整数累加</summary>
    public virtual Int64 Increment(String key, Int64 value)
    {
        lock (this)
        {
            var current = Get<Int64>(key) + value;
            Set(key, current);
            return current;
        }
    }

    /// <summary>浮点累加</summary>
    public virtual Double Increment(String key, Double value)
    {
        lock (this)
        {
            var current = Get<Double>(key) + value;
            Set(key, current);
            return current;
        }
    }

    /// <summary>整数递减</summary>
    public virtual Int64 Decrement(String key, Int64 value)
    {
        lock (this)
        {
            var current = Get<Int64>(key) - value;
            Set(key, current);
            return current;
        }
    }

    /// <summary>浮点递减</summary>
    public virtual Double Decrement(String key, Double value)
    {
        lock (this)
        {
            var current = Get<Double>(key) - value;
            Set(key, current);
            return current;
        }
    }

    /// <summary>整数累加并返回过期时间</summary>
    public virtual (Int64 Value, Int32 Ttl) IncrementWithTtl(String key, Int64 value = 1)
    {
        var result = Increment(key, value);
        var expire = GetExpire(key);
        var ttl = expire < TimeSpan.Zero ? -2 : expire == TimeSpan.Zero ? -1 : (Int32)expire.TotalSeconds;
        return (result, ttl);
    }

    /// <summary>浮点累加并返回过期时间</summary>
    public virtual (Double Value, Int32 Ttl) IncrementWithTtl(String key, Double value)
    {
        var result = Increment(key, value);
        var expire = GetExpire(key);
        var ttl = expire < TimeSpan.Zero ? -2 : expire == TimeSpan.Zero ? -1 : (Int32)expire.TotalSeconds;
        return (result, ttl);
    }

    /// <summary>整数递减并返回过期时间</summary>
    public virtual (Int64 Value, Int32 Ttl) DecrementWithTtl(String key, Int64 value = 1)
    {
        var result = Decrement(key, value);
        var expire = GetExpire(key);
        var ttl = expire < TimeSpan.Zero ? -2 : expire == TimeSpan.Zero ? -1 : (Int32)expire.TotalSeconds;
        return (result, ttl);
    }

    /// <summary>浮点递减并返回过期时间</summary>
    public virtual (Double Value, Int32 Ttl) DecrementWithTtl(String key, Double value) => IncrementWithTtl(key, -value);

    /// <summary>搜索键</summary>
    public virtual IEnumerable<String> Search(String pattern, Int32 offset = 0, Int32 count = -1) => [];

    /// <summary>提交</summary>
    public virtual Int32 Commit() => 0;

    /// <summary>申请简易锁</summary>
    public virtual IDisposable? AcquireLock(String key, Int32 msTimeout)
    {
        var rlock = new CacheLock(this, key);
        if (!rlock.Acquire(msTimeout, msTimeout)) throw new InvalidOperationException($"Lock [{key}] failed! msTimeout={msTimeout}");

        return rlock;
    }

    /// <summary>申请锁</summary>
    public virtual IDisposable? AcquireLock(String key, Int32 msTimeout, Int32 msExpire, Boolean throwOnFailure)
    {
        var rlock = new CacheLock(this, key);
        if (!rlock.Acquire(msTimeout, msExpire))
        {
            if (throwOnFailure) throw new InvalidOperationException($"Lock [{key}] failed! msTimeout={msTimeout}");
            return null;
        }

        return rlock;
    }

    /// <summary>性能测试</summary>
    public virtual Int64 Bench(Boolean rand = false, Int32 batch = 0) => 0;
}
