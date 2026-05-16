using System.Diagnostics.CodeAnalysis;

namespace Pek.Caching;

/// <summary>缓存接口</summary>
public interface ICache
{
    /// <summary>缓存名称</summary>
    String Name { get; }

    /// <summary>默认过期时间，秒</summary>
    Int32 Expire { get; set; }

    /// <summary>缓存项总数</summary>
    Int32 Count { get; }

    /// <summary>所有键</summary>
    ICollection<String> Keys { get; }

    /// <summary>获取或设置缓存项</summary>
    Object? this[String key] { get; set; }

    /// <summary>检查键是否存在</summary>
    Boolean ContainsKey(String key);

    /// <summary>设置缓存项</summary>
    Boolean Set<T>(String key, T value, Int32 expire = -1);

    /// <summary>设置缓存项</summary>
    Boolean Set<T>(String key, T value, TimeSpan expire);

    /// <summary>获取缓存项</summary>
    [return: MaybeNull]
    T Get<T>(String key);

    /// <summary>尝试获取缓存项</summary>
    Boolean TryGetValue<T>(String key, [MaybeNull] out T value);

    /// <summary>移除缓存项</summary>
    Int32 Remove(String key);

    /// <summary>批量移除缓存项</summary>
    Int32 Remove(params String[] keys);

    /// <summary>清空缓存</summary>
    void Clear();

    /// <summary>设置过期时间</summary>
    Boolean SetExpire(String key, TimeSpan expire);

    /// <summary>获取过期时间</summary>
    TimeSpan GetExpire(String key);

    /// <summary>批量获取</summary>
    IDictionary<String, T?> GetAll<T>(IEnumerable<String> keys);

    /// <summary>批量设置</summary>
    void SetAll<T>(IDictionary<String, T> values, Int32 expire = -1);

    /// <summary>添加缓存项</summary>
    Boolean Add<T>(String key, T value, Int32 expire = -1);

    /// <summary>替换并返回旧值</summary>
    [return: MaybeNull]
    T Replace<T>(String key, T value);

    /// <summary>获取或添加</summary>
    [return: MaybeNull]
    T GetOrAdd<T>(String key, Func<String, T> callback, Int32 expire = -1);

    /// <summary>整数累加</summary>
    Int64 Increment(String key, Int64 value);

    /// <summary>浮点累加</summary>
    Double Increment(String key, Double value);

    /// <summary>整数递减</summary>
    Int64 Decrement(String key, Int64 value);

    /// <summary>浮点递减</summary>
    Double Decrement(String key, Double value);

    /// <summary>整数累加并获取过期时间</summary>
    (Int64 Value, Int32 Ttl) IncrementWithTtl(String key, Int64 value = 1);

    /// <summary>浮点累加并获取过期时间</summary>
    (Double Value, Int32 Ttl) IncrementWithTtl(String key, Double value);

    /// <summary>整数递减并获取过期时间</summary>
    (Int64 Value, Int32 Ttl) DecrementWithTtl(String key, Int64 value = 1);

    /// <summary>浮点递减并获取过期时间</summary>
    (Double Value, Int32 Ttl) DecrementWithTtl(String key, Double value);

    /// <summary>搜索键</summary>
    IEnumerable<String> Search(String pattern, Int32 offset = 0, Int32 count = -1);

    /// <summary>获取列表</summary>
    IList<T> GetList<T>(String key);

    /// <summary>获取字典</summary>
    IDictionary<String, T> GetDictionary<T>(String key);

    /// <summary>获取队列</summary>
    IProducerConsumer<T> GetQueue<T>(String key);

    /// <summary>获取栈</summary>
    IProducerConsumer<T> GetStack<T>(String key);

    /// <summary>获取集合</summary>
    ICollection<T> GetSet<T>(String key);

    /// <summary>申请简化分布式锁</summary>
    IDisposable? AcquireLock(String key, Int32 msTimeout);

    /// <summary>申请分布式锁</summary>
    IDisposable? AcquireLock(String key, Int32 msTimeout, Int32 msExpire, Boolean throwOnFailure);

    /// <summary>提交变更</summary>
    Int32 Commit();

    /// <summary>性能测试</summary>
    Int64 Bench(Boolean rand = false, Int32 batch = 0);
}
