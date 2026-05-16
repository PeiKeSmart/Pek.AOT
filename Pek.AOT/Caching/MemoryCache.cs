using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

using Pek.Extension;
using Pek.Messaging;
using Pek.Threading;

namespace Pek.Caching;

/// <summary>缓存键事件参数</summary>
public class KeyEventArgs : CancelEventArgs
{
    /// <summary>缓存键</summary>
    public String Key { get; set; } = String.Empty;
}

/// <summary>内存缓存</summary>
public class MemoryCache : Cache
{
    /// <summary>缓存核心</summary>
    protected ConcurrentDictionary<String, CacheItem> _cache = new(StringComparer.Ordinal);

    /// <summary>容量</summary>
    public Int32 Capacity { get; set; } = 100_000;

    /// <summary>定时清理周期，秒</summary>
    public Int32 Period { get; set; } = 60;

    /// <summary>缓存键过期事件</summary>
    public event EventHandler<KeyEventArgs>? KeyExpired;

    /// <summary>默认实例</summary>
    public static MemoryCache Instance { get; set; } = new();

    private Int32 _count;
    private TimerX? _clearTimer;

    /// <summary>缓存个数</summary>
    public override Int32 Count => _count;

    /// <summary>所有键</summary>
    public override ICollection<String> Keys => _cache.Keys;

    /// <summary>构造函数</summary>
    public MemoryCache() => Init(null);

    /// <summary>释放资源</summary>
    /// <param name="disposing">是否由 Dispose 调用</param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        _clearTimer?.Dispose();
        _clearTimer = null;
    }

    /// <summary>初始化</summary>
    /// <param name="config">配置</param>
    public override void Init(String? config)
    {
        if (_clearTimer == null)
            _clearTimer = new TimerX(RemoveNotAlive, null, 10_000, Period * 1000) { Async = true };
    }

    /// <summary>是否包含键</summary>
    public override Boolean ContainsKey(String key) => _cache.TryGetValue(key, out var item) && item != null && !item.Expired;

    /// <summary>设置缓存项</summary>
    public override Boolean Set<T>(String key, T value, Int32 expire = -1)
    {
        if (expire < 0) expire = Expire;

        CacheItem? item = null;
        do
        {
            if (_cache.TryGetValue(key, out item) && item != null)
            {
                item.Set(value, expire);
                return true;
            }

            item ??= new CacheItem(value, expire);
        } while (!_cache.TryAdd(key, item));

        Interlocked.Increment(ref _count);
        return true;
    }

    /// <summary>获取缓存项</summary>
    [return: MaybeNull]
    public override T Get<T>(String key)
    {
        if (!_cache.TryGetValue(key, out var item) || item == null || item.Expired) return default;
        return item.Visit<T>();
    }

    /// <summary>移除缓存项</summary>
    public override Int32 Remove(String key)
    {
        if (key.Contains('*') || key.Contains('?'))
            return RemoveInternal(Search(key).ToArray());

        if (_cache.TryRemove(key, out _))
        {
            Interlocked.Decrement(ref _count);
            return 1;
        }

        return 0;
    }

    /// <summary>批量移除</summary>
    public override Int32 Remove(params String[] keys)
    {
        if (keys.All(static k => !k.Contains('*') && !k.Contains('?'))) return RemoveInternal(keys);

        var count = 0;
        foreach (var item in _cache)
        {
            if (!keys.Any(pattern => pattern.IsMatch(item.Key))) continue;
            if (_cache.TryRemove(item.Key, out _))
            {
                Interlocked.Decrement(ref _count);
                count++;
            }
        }

        return count;
    }

    /// <summary>清空</summary>
    public override void Clear()
    {
        _cache.Clear();
        _count = 0;
    }

    /// <summary>设置过期时间</summary>
    public override Boolean SetExpire(String key, TimeSpan expire)
    {
        if (!_cache.TryGetValue(key, out var item) || item == null) return false;
        item.SetExpire(expire);
        return true;
    }

    /// <summary>获取过期时间</summary>
    public override TimeSpan GetExpire(String key)
    {
        if (!_cache.TryGetValue(key, out var item) || item == null) return TimeSpan.FromSeconds(-1);
        if (item.ExpiredTime == Int64.MaxValue) return TimeSpan.Zero;

        return TimeSpan.FromMilliseconds(item.ExpiredTime - Runtime.TickCount64);
    }

    /// <summary>添加</summary>
    public override Boolean Add<T>(String key, T value, Int32 expire = -1)
    {
        if (expire < 0) expire = Expire;

        CacheItem? item = null;
        do
        {
            if (_cache.TryGetValue(key, out item) && item != null)
            {
                if (!item.Expired) return false;

                item.Set(value, expire);
                return true;
            }

            item ??= new CacheItem(value, expire);
        } while (!_cache.TryAdd(key, item));

        Interlocked.Increment(ref _count);
        return true;
    }

    /// <summary>替换并返回旧值</summary>
    [return: MaybeNull]
    public override T Replace<T>(String key, T value)
    {
        var expire = Expire;
        CacheItem? item = null;
        do
        {
            if (_cache.TryGetValue(key, out item) && item != null)
            {
                var rs = item.Expired ? default : item.Visit<T>();
                item.Set(value, expire);
                return rs;
            }

            item ??= new CacheItem(value, expire);
        } while (!_cache.TryAdd(key, item));

        Interlocked.Increment(ref _count);
        return default;
    }

    /// <summary>尝试获取</summary>
    public override Boolean TryGetValue<T>(String key, [MaybeNull] out T value)
    {
        value = default;
        if (!_cache.TryGetValue(key, out var item) || item == null || item.Expired) return false;

        value = item.Visit<T>();
        return true;
    }

    /// <summary>获取或添加</summary>
    [return: MaybeNull]
    public override T GetOrAdd<T>(String key, Func<String, T> callback, Int32 expire = -1)
    {
        if (expire < 0) expire = Expire;

        CacheItem? item = null;
        do
        {
            if (_cache.TryGetValue(key, out item) && item != null)
            {
                if (!item.Expired) return item.Visit<T>();

                item.Set(callback(key), expire);
                return item.Visit<T>();
            }

            item ??= new CacheItem(callback(key), expire);
        } while (!_cache.TryAdd(key, item));

        Interlocked.Increment(ref _count);
        return item.Visit<T>();
    }

    /// <summary>整数累加</summary>
    public override Int64 Increment(String key, Int64 value) => GetOrAddItem(key, static _ => 0L).Inc(value);

    /// <summary>浮点累加</summary>
    public override Double Increment(String key, Double value) => GetOrAddItem(key, static _ => 0d).Inc(value);

    /// <summary>整数递减</summary>
    public override Int64 Decrement(String key, Int64 value) => GetOrAddItem(key, static _ => 0L).Dec(value);

    /// <summary>浮点递减</summary>
    public override Double Decrement(String key, Double value) => GetOrAddItem(key, static _ => 0d).Dec(value);

    /// <summary>搜索键</summary>
    public override IEnumerable<String> Search(String pattern, Int32 offset = 0, Int32 count = -1)
    {
        foreach (var item in _cache)
        {
            var key = item.Key;
            if (!pattern.IsNullOrEmpty() && pattern != key && !pattern.IsMatch(key)) continue;

            if (offset > 0)
            {
                offset--;
                continue;
            }

            if (count == 0) yield break;
            if (count > 0) count--;

            yield return key;
        }
    }

    /// <summary>获取列表</summary>
    public override IList<T> GetList<T>(String key) => GetOrAddItem(key, static _ => new List<T>()).Visit<IList<T>>() ?? throw new InvalidCastException($"Unable to convert the value of [{key}] to {typeof(IList<T>).FullName}");

    /// <summary>获取字典</summary>
    public override IDictionary<String, T> GetDictionary<T>(String key) => GetOrAddItem(key, static _ => new ConcurrentDictionary<String, T>()).Visit<IDictionary<String, T>>() ?? throw new InvalidCastException($"Unable to convert the value of [{key}] to {typeof(IDictionary<String, T>).FullName}");

    /// <summary>获取队列</summary>
    public override IProducerConsumer<T> GetQueue<T>(String key) => GetOrAddItem(key, static _ => new MemoryQueue<T>(new ConcurrentQueue<T>())).Visit<IProducerConsumer<T>>() ?? throw new InvalidCastException($"Unable to convert the value of [{key}] to {typeof(IProducerConsumer<T>).FullName}");

    /// <summary>获取栈</summary>
    public override IProducerConsumer<T> GetStack<T>(String key) => GetOrAddItem(key, static _ => new MemoryQueue<T>(new ConcurrentStack<T>())).Visit<IProducerConsumer<T>>() ?? throw new InvalidCastException($"Unable to convert the value of [{key}] to {typeof(IProducerConsumer<T>).FullName}");

    /// <summary>获取集合</summary>
    public override ICollection<T> GetSet<T>(String key) => GetOrAddItem(key, static _ => new HashSet<T>()).Visit<ICollection<T>>() ?? throw new InvalidCastException($"Unable to convert the value of [{key}] to {typeof(ICollection<T>).FullName}");

    /// <summary>创建事件总线</summary>
    public override IEventBus<TEvent> CreateEventBus<TEvent>(String topic, String clientId = "") => GetOrAddItem($"event:{topic}", _ => new QueueEventBus<TEvent>(this, topic)).Visit<IEventBus<TEvent>>() ?? new QueueEventBus<TEvent>(this, topic);

    /// <summary>性能测试</summary>
    public override Int64 Bench(Boolean rand = false, Int32 batch = 0) => 0;

    /// <summary>获取或添加缓存项</summary>
    protected CacheItem GetOrAddItem(String key, Func<String, Object> valueFactory)
    {
        var expire = Expire;
        CacheItem? item = null;
        do
        {
            if (_cache.TryGetValue(key, out item) && item != null)
            {
                if (!item.Expired) return item;

                item.Set(valueFactory(key), expire);
                return item;
            }

            item ??= new CacheItem(valueFactory(key), expire);
        } while (!_cache.TryAdd(key, item));

        Interlocked.Increment(ref _count);
        return item;
    }

    private Int32 RemoveInternal(IEnumerable<String> keys)
    {
        var count = 0;
        foreach (var item in keys)
        {
            if (!_cache.TryRemove(item, out _)) continue;

            Interlocked.Decrement(ref _count);
            count++;
        }

        return count;
    }

    private void RemoveNotAlive(Object? state)
    {
        var now = Runtime.TickCount64;
        var toDelete = new List<String>();
        foreach (var item in _cache)
        {
            if (item.Value.ExpiredTime != Int64.MaxValue && item.Value.ExpiredTime <= now) toDelete.Add(item.Key);
        }

        foreach (var item in toDelete)
        {
            if (!OnExpire(item)) continue;
            if (_cache.TryRemove(item, out _)) Interlocked.Decrement(ref _count);
        }

        if (Capacity > 0 && _count > Capacity)
        {
            var ordered = _cache.OrderBy(e => e.Value.VisitTime).Take(_count - Capacity).Select(e => e.Key).ToArray();
            RemoveInternal(ordered);
        }
    }

    /// <summary>缓存过期</summary>
    protected virtual Boolean OnExpire(String key)
    {
        var e = new KeyEventArgs { Key = key, Cancel = false };
        KeyExpired?.Invoke(this, e);
        return !e.Cancel;
    }

    /// <summary>缓存项</summary>
    protected class CacheItem
    {
        private TypeCode _typeCode;
        private Int64 _valueLong;
        private Object? _value;

        /// <summary>过期时间</summary>
        public Int64 ExpiredTime { get; private set; }

        /// <summary>是否过期</summary>
        public Boolean Expired => ExpiredTime != Int64.MaxValue && ExpiredTime <= Runtime.TickCount64;

        /// <summary>最近访问时间</summary>
        public Int64 VisitTime { get; private set; }

        /// <summary>构造缓存项</summary>
        public CacheItem(Object? value, Int32 expire) => Set(value, expire);

        /// <summary>设置值</summary>
        public void Set<T>(T value, Int32 expire)
        {
            _typeCode = Type.GetTypeCode(typeof(T));
            if (value == null)
            {
                _value = null;
                _valueLong = 0;
            }
            else if (IsInt(_typeCode))
            {
                _valueLong = value.ToLong();
                _value = null;
            }
            else
            {
                _value = value;
            }

            var now = VisitTime = Runtime.TickCount64;
            ExpiredTime = expire <= 0 ? Int64.MaxValue : now + expire * 1000L;
        }

        /// <summary>设置过期时间</summary>
        public void SetExpire(TimeSpan expire)
        {
            var now = VisitTime = Runtime.TickCount64;
            ExpiredTime = expire == TimeSpan.Zero ? Int64.MaxValue : now + (Int64)expire.TotalMilliseconds;
        }

        /// <summary>访问值</summary>
        [return: MaybeNull]
        public T Visit<T>()
        {
            VisitTime = Runtime.TickCount64;

            if (_value != null)
            {
                if (_value is T typed) return typed;
                return ConvertTo<T>(_value);
            }

            if (_valueLong is T matched) return matched;
            return ConvertTo<T>(_valueLong);
        }

        /// <summary>整数累加</summary>
        public Int64 Inc(Int64 value)
        {
            if (!IsInt(_typeCode))
            {
                _valueLong = _value.ToLong();
                _value = null;
                _typeCode = TypeCode.Int64;
            }

            VisitTime = Runtime.TickCount64;
            return Interlocked.Add(ref _valueLong, value);
        }

        /// <summary>浮点累加</summary>
        public Double Inc(Double value)
        {
            Double newValue;
            Object? oldValue;
            do
            {
                oldValue = _value;
                newValue = (oldValue is Double number ? number : oldValue.ToDouble()) + value;
            } while (Interlocked.CompareExchange(ref _value, newValue, oldValue) != oldValue);

            _typeCode = TypeCode.Double;
            VisitTime = Runtime.TickCount64;
            return newValue;
        }

        /// <summary>整数递减</summary>
        public Int64 Dec(Int64 value)
        {
            if (!IsInt(_typeCode))
            {
                _valueLong = _value.ToLong();
                _value = null;
                _typeCode = TypeCode.Int64;
            }

            VisitTime = Runtime.TickCount64;
            return Interlocked.Add(ref _valueLong, -value);
        }

        /// <summary>浮点递减</summary>
        public Double Dec(Double value)
        {
            Double newValue;
            Object? oldValue;
            do
            {
                oldValue = _value;
                newValue = (oldValue is Double number ? number : oldValue.ToDouble()) - value;
            } while (Interlocked.CompareExchange(ref _value, newValue, oldValue) != oldValue);

            _typeCode = TypeCode.Double;
            VisitTime = Runtime.TickCount64;
            return newValue;
        }

        private static Boolean IsInt(TypeCode typeCode) => typeCode >= TypeCode.SByte && typeCode <= TypeCode.UInt64;

        [return: MaybeNull]
        private static T ConvertTo<T>(Object value)
        {
            var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            if (targetType == typeof(Object)) return (T)value;
            if (targetType.IsEnum)
            {
                var underlying = Enum.GetUnderlyingType(targetType);
                return (T)Enum.ToObject(targetType, System.Convert.ChangeType(value, underlying)!);
            }

            if (Type.GetTypeCode(targetType) == TypeCode.Object) return default;
            return (T)System.Convert.ChangeType(value, targetType)!;
        }
    }

    private sealed class MemoryQueue<T> : IProducerConsumer<T>
    {
        private readonly IProducerConsumerCollection<T> _queue;
        private readonly SemaphoreSlim _signal = new(0);

        public MemoryQueue(IProducerConsumerCollection<T> queue) => _queue = queue;

        public Int32 Count => _queue.Count;

        public Boolean IsEmpty => _queue.Count == 0;

        public Int32 Add(params T[] values)
        {
            var count = 0;
            foreach (var item in values)
            {
                if (!_queue.TryAdd(item)) continue;
                _signal.Release();
                count++;
            }

            return count;
        }

        public IEnumerable<T> Take(Int32 count = 1)
        {
            for (var i = 0; i < count; i++)
            {
                if (!_queue.TryTake(out var item)) yield break;
                _signal.Wait(0);
                yield return item;
            }
        }

        public T? TakeOne(Int32 timeout = 0) => TakeOneAsync(timeout).ConfigureAwait(false).GetAwaiter().GetResult();

        public Task<T?> TakeOneAsync(Int32 timeout = 0) => TakeOneAsync(timeout, CancellationToken.None);

        public async Task<T?> TakeOneAsync(Int32 timeout, CancellationToken cancellationToken)
        {
            if (timeout <= 0)
            {
                await _signal.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var ok = await _signal.WaitAsync(TimeSpan.FromSeconds(timeout), cancellationToken).ConfigureAwait(false);
                if (!ok) return default;
            }

            return _queue.TryTake(out var item) ? item : default;
        }

        public Int32 Acknowledge(params String[] keys) => 0;
    }
}
