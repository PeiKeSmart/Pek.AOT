using Pek.Messaging;

namespace Pek.Caching;

/// <summary>缓存提供者</summary>
public interface ICacheProvider
{
    /// <summary>全局缓存</summary>
    ICache Cache { get; set; }

    /// <summary>应用内本地缓存</summary>
    ICache InnerCache { get; set; }

    /// <summary>获取队列</summary>
    IProducerConsumer<T> GetQueue<T>(String topic, String? group = null);

    /// <summary>获取内部队列</summary>
    IProducerConsumer<T> GetInnerQueue<T>(String topic);

    /// <summary>申请分布式锁</summary>
    IDisposable? AcquireLock(String lockKey, Int32 msTimeout);
}

/// <summary>缓存提供者助手</summary>
public static class CacheProviderHelper
{
    /// <summary>创建事件总线</summary>
    public static IEventBus<TEvent>? CreateEventBus<TEvent>(this ICacheProvider provider, String topic, String? clientId = null)
    {
        if (provider.Cache is not Cache cache) return null;

        return cache.CreateEventBus<TEvent>(topic, clientId ?? String.Empty);
    }
}
