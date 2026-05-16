using Pek.Extension;

namespace Pek.Caching;

/// <summary>分布式锁</summary>
public class CacheLock : DisposeBase
{
    private readonly ICache _client;
    private Boolean _hasLock;

    /// <summary>是否持有锁</summary>
    public Boolean HasLock => _hasLock;

    /// <summary>锁键</summary>
    public String Key { get; }

    /// <summary>实例化分布式锁</summary>
    /// <param name="client">缓存客户端</param>
    /// <param name="key">锁键</param>
    public CacheLock(ICache client, String key)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        if (key.IsNullOrEmpty()) throw new ArgumentNullException(nameof(key));

        Key = key;
    }

    /// <summary>申请锁</summary>
    /// <param name="msTimeout">等待时间</param>
    /// <param name="msExpire">过期时间</param>
    /// <returns>是否成功</returns>
    public Boolean Acquire(Int32 msTimeout, Int32 msExpire)
    {
        var now = Runtime.TickCount64;
        var end = now + msTimeout;
        while (now < end)
        {
            if (_client.Add(Key, now + msExpire, msExpire / 1000))
                return _hasLock = true;

            var expiredAt = _client.Get<Int64>(Key);
            if (expiredAt <= now)
            {
                var oldValue = _client.Replace(Key, now + msExpire);
                if (oldValue <= expiredAt)
                {
                    _client.SetExpire(Key, TimeSpan.FromMilliseconds(msExpire));
                    return _hasLock = true;
                }
            }

            Thread.Sleep(200);
            now = Runtime.TickCount64;
        }

        return false;
    }

    /// <summary>释放资源</summary>
    /// <param name="disposing">是否由 Dispose 调用</param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        if (!_hasLock) return;
        if (_client is DisposeBase disposeBase && disposeBase.Disposed) return;

        _client.Remove(Key);
    }
}
