using System.Collections;
using System.Collections.Concurrent;
using Pek.Log;
using Pek.Threading;

namespace Pek.Collections;

/// <summary>字典缓存。当指定键的缓存项不存在时，调用委托获取值，并写入缓存。</summary>
/// <typeparam name="TKey">键类型</typeparam>
/// <typeparam name="TValue">值类型</typeparam>
public class DictionaryCache<TKey, TValue> : DisposeBase, IEnumerable<KeyValuePair<TKey, TValue>> where TKey : notnull
{
    /// <summary>过期时间。单位秒，0 表示永不过期</summary>
    public Int32 Expire { get; set; }

    /// <summary>定时清理时间。单位秒，0 表示不清理</summary>
    public Int32 Period { get; set; }

    /// <summary>容量。超出容量时采用 LRU 逐出，默认 10_000</summary>
    public Int32 Capacity { get; set; } = 10_000;

    /// <summary>是否允许空值进入缓存</summary>
    public Boolean AllowNull { get; set; }

    /// <summary>查找数据的方法</summary>
    public Func<TKey, TValue>? FindMethod { get; set; }

    private readonly ConcurrentDictionary<TKey, CacheItem> _cache;
    private Int32 _count;
    private TimerX? _timer;

    /// <summary>实例化字典缓存</summary>
    public DictionaryCache() => _cache = new ConcurrentDictionary<TKey, CacheItem>();

    /// <summary>实例化字典缓存</summary>
    /// <param name="comparer">键比较器</param>
    public DictionaryCache(IEqualityComparer<TKey> comparer) => _cache = new ConcurrentDictionary<TKey, CacheItem>(comparer);

    /// <summary>实例化字典缓存</summary>
    /// <param name="findMethod">查找数据的方法</param>
    /// <param name="comparer">键比较器</param>
    public DictionaryCache(Func<TKey, TValue> findMethod, IEqualityComparer<TKey>? comparer = null)
    {
        FindMethod = findMethod;
        _cache = comparer != null
            ? new ConcurrentDictionary<TKey, CacheItem>(comparer)
            : new ConcurrentDictionary<TKey, CacheItem>();
    }

    /// <summary>缓存项数量</summary>
    public Int32 Count => _count;

    /// <summary>索引器。取值时若不存在则自动加载，赋值时则写入缓存。</summary>
    /// <param name="key">键</param>
    /// <returns>缓存值</returns>
    public TValue? this[TKey key] { get => GetOrAdd(key); set => Set(key, value!); }

    /// <summary>获取或添加缓存项</summary>
    /// <param name="key">键</param>
    /// <returns>缓存值</returns>
    public virtual TValue? GetOrAdd(TKey key)
    {
        var func = FindMethod;

        if (_cache.TryGetValue(key, out var item))
        {
            if (Expire > 0 && item.Expired)
            {
                if (func != null)
                {
                    item.Set(item.Value, Expire);
                    Task.Factory.StartNew(() => item.Set(func(key), Expire));
                }
                else
                {
                    _cache.Remove(key);
                }
            }

            return item.Visit();
        }

        if (func != null)
        {
            var value = func(key);
            if (value != null || AllowNull)
            {
                if (!TryAdd(key, value, false, out var result)) return result;
                return value;
            }
        }

        return default;
    }

    /// <summary>获取缓存项</summary>
    /// <param name="key">键</param>
    /// <returns>缓存值</returns>
    public virtual TValue? Get(TKey key)
    {
        if (!_cache.TryGetValue(key, out var item) || item.Expired) return default;

        return item.Visit();
    }

    /// <summary>尝试获取缓存值</summary>
    /// <param name="key">键</param>
    /// <param name="value">缓存值</param>
    /// <returns>是否成功</returns>
    public virtual Boolean TryGetValue(TKey key, out TValue? value)
    {
        value = default;
        if (!_cache.TryGetValue(key, out var item) || item.Expired) return false;

        value = item.Visit();
        return true;
    }

    /// <summary>设置缓存项</summary>
    /// <param name="key">键</param>
    /// <param name="value">值</param>
    /// <returns>是否新增</returns>
    public virtual Boolean Set(TKey key, TValue value) => TryAdd(key, value, true, out _);

    /// <summary>尝试添加缓存项，若存在则返回旧值</summary>
    /// <param name="key">键</param>
    /// <param name="value">值</param>
    /// <param name="updateIfExists">是否在已存在时更新</param>
    /// <param name="resultingValue">返回结果值</param>
    /// <returns>是否新增成功</returns>
    public virtual Boolean TryAdd(TKey key, TValue value, Boolean updateIfExists, out TValue? resultingValue)
    {
        CacheItem? cacheItem = null;
        do
        {
            if (_cache.TryGetValue(key, out var item))
            {
                resultingValue = item.Value;
                if (updateIfExists) item.Set(value, Expire);
                return false;
            }

            cacheItem ??= new CacheItem(value, Expire);
        } while (!_cache.TryAdd(key, cacheItem));

        Interlocked.Increment(ref _count);
        resultingValue = default;
        StartTimer();
        return true;
    }

    /// <summary>扩展获取数据项，不存在时调用委托加载，线程安全。</summary>
    /// <param name="key">键</param>
    /// <param name="func">加载委托</param>
    /// <returns>缓存值</returns>
    public virtual TValue? GetItem(TKey key, Func<TKey, TValue> func)
    {
        var exp = Expire;
        var items = _cache;
        if (items.TryGetValue(key, out var item) && (exp <= 0 || !item.Expired)) return item.Visit();

        var value = default(TValue);
        lock (items)
        {
            if (items.TryGetValue(key, out item) && (exp <= 0 || !item.Expired)) return item.Visit();

            if (func != null)
            {
                if (item != null)
                {
                    value = item.Visit();
                    item.Set(value, Expire);
                    ThreadPoolX.QueueUserWorkItem(() => value = func(key));
                }
                else
                {
                    value = func(key);
                    if (value != null || AllowNull)
                    {
                        items[key] = new CacheItem(value, exp);
                        Interlocked.Increment(ref _count);
                    }
                }
            }

            StartTimer();
            return value;
        }
    }

    /// <summary>移除指定缓存项</summary>
    /// <param name="key">键</param>
    /// <returns>是否移除成功</returns>
    public virtual Boolean Remove(TKey key)
    {
        if (!_cache.Remove(key)) return false;

        Interlocked.Decrement(ref _count);
        return true;
    }

    /// <summary>清空缓存</summary>
    public virtual void Clear() => _cache.Clear();

    /// <summary>是否包含指定键</summary>
    /// <param name="key">键</param>
    /// <returns>是否包含</returns>
    public Boolean ContainsKey(TKey key) => _cache.ContainsKey(key);

    /// <summary>复制到目标缓存</summary>
    /// <param name="cache">目标缓存</param>
    public void CopyTo(DictionaryCache<TKey, TValue> cache)
    {
        if (_cache.IsEmpty) return;

        foreach (var item in _cache)
        {
            cache[item.Key] = item.Value.Visit();
        }
    }

    /// <summary>释放资源</summary>
    /// <param name="disposing">是否释放托管资源</param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        _count = 0;
        StopTimer();
    }

    /// <summary>枚举缓存项</summary>
    /// <returns>枚举器</returns>
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        foreach (var item in _cache)
        {
            yield return new KeyValuePair<TKey, TValue>(item.Key, item.Value.Visit());
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void StartTimer()
    {
        var period = Period;
        if (period <= 0 || _count <= 0) return;

        if (_timer != null) return;
        lock (this)
        {
            if (_timer == null) _timer = new TimerX(RemoveNotAlive, null, period * 1000, period * 1000) { Async = true };
        }
    }

    private void StopTimer()
    {
        _timer.TryDispose();
        _timer = null;
    }

    private void RemoveNotAlive(Object? state)
    {
        var dictionary = _cache;
        if (_count == 0 && !dictionary.Any())
        {
            StopTimer();
            return;
        }

        var now = TimerX.Now;
        var keys = new List<TKey>();
        var aliveCount = 0;
        foreach (var item in dictionary)
        {
            var expiredTime = item.Value.ExpiredTime;
            if (expiredTime < now)
                keys.Add(item.Key);
            else
                aliveCount++;
        }

        var threshold = now.AddSeconds(-Expire);
        var evicted = 0;
        while (Capacity > 0 && aliveCount > Capacity)
        {
            threshold = threshold.AddSeconds(Expire / 10);
            if (threshold >= now) break;

            foreach (var item in dictionary)
            {
                var visitTime = item.Value.VisitTime;
                if (visitTime < threshold)
                {
                    keys.Add(item.Key);
                    aliveCount--;
                    evicted++;
                }
            }
        }

#if DEBUG
        if (evicted > 0) XTrace.WriteLine("字典缓存[{0:n0}]超过容量[{1:n0}]，逐出[{2:n0}]个", _count, Capacity, evicted);
#endif

        foreach (var item in keys)
        {
            dictionary.Remove(item);
        }

        _count = aliveCount;
    }

    class CacheItem
    {
        public TValue Value { get; private set; } = default!;

        public DateTime ExpiredTime { get; private set; }

        public Boolean Expired => ExpiredTime <= DateTime.Now;

        public DateTime VisitTime { get; private set; }

        public CacheItem(TValue value, Int32 seconds) => Set(value, seconds);

        public void Set(TValue value, Int32 seconds)
        {
            Value = value;

            var now = VisitTime = DateTime.Now;
            if (seconds > 0) ExpiredTime = now.AddSeconds(seconds);
        }

        public TValue Visit()
        {
            VisitTime = TimerX.Now;
            return Value;
        }
    }
}