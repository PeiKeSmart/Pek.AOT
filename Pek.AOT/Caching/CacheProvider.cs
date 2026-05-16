namespace Pek.Caching;

/// <summary>缓存提供者默认实现</summary>
public class CacheProvider : ICacheProvider
{
    /// <summary>全局缓存</summary>
    public ICache Cache { get; set; }

    /// <summary>应用内本地缓存</summary>
    public ICache InnerCache { get; set; }

    /// <summary>实例化</summary>
    public CacheProvider()
    {
        var cache = Pek.Caching.Cache.Default ?? new MemoryCache();
        Cache = cache;
        InnerCache = cache;
    }

    /// <summary>获取队列</summary>
    public virtual IProducerConsumer<T> GetQueue<T>(String topic, String? group = null) => Cache.GetQueue<T>(topic);

    /// <summary>获取内部队列</summary>
    public virtual IProducerConsumer<T> GetInnerQueue<T>(String topic) => InnerCache.GetQueue<T>(topic);

    /// <summary>申请分布式锁</summary>
    public virtual IDisposable? AcquireLock(String lockKey, Int32 msTimeout) => Cache.AcquireLock(lockKey, msTimeout);
}
