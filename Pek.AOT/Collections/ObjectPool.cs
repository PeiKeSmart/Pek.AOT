using System.Collections.Concurrent;
using System.Diagnostics;
using Pek.Threading;
using Pek.Logging;

namespace Pek.Collections;

/// <summary>资源池。支持空闲释放</summary>
/// <typeparam name="T">池化类型</typeparam>
public class ObjectPool<T> : DisposeBase, IPool<T> where T : notnull
{
    private const String LogScope = "Pek.Collections";

    private readonly ConcurrentStack<Item> _free = new();
    private readonly ConcurrentQueue<Item> _free2 = new();
    private readonly ConcurrentDictionary<T, Item> _busy = new();
    private readonly Object _sync = new();
    private readonly Func<T?>? _factory;
    private volatile Boolean _inited;
    private TimerX? _timer;
    private Int32 _freeCount;
    private Int32 _busyCount;
    private Int32 _total;
    private Int32 _success;
    private Int32 _newCount;
    private Int32 _releaseCount;
    private Double _cost;

    class Item
    {
        public T? Value { get; set; }
        public DateTime LastTime { get; set; }
    }

    /// <summary>实例化资源池</summary>
    /// <param name="factory">工厂委托</param>
    public ObjectPool(Func<T?>? factory = null)
    {
        _factory = factory;

        var str = GetType().Name;
        var p = str.IndexOf('`');
        if (p >= 0) str = str[..p];
        Name = str != "Pool" ? str : $"Pool<{typeof(T).Name}>";
    }

    /// <summary>名称</summary>
    public String Name { get; set; }

    /// <summary>空闲个数</summary>
    public Int32 FreeCount => _freeCount;

    /// <summary>繁忙个数</summary>
    public Int32 BusyCount => _busyCount;

    /// <summary>最大个数。0 表示无上限</summary>
    public Int32 Max { get; set; } = 100;

    /// <summary>最小个数</summary>
    public Int32 Min { get; set; } = 1;

    /// <summary>空闲清理时间，单位秒</summary>
    public Int32 IdleTime { get; set; } = 10;

    /// <summary>完全空闲清理时间，单位秒</summary>
    public Int32 AllIdleTime { get; set; }

    /// <summary>总请求数</summary>
    public Int32 Total => _total;

    /// <summary>命中次数</summary>
    public Int32 Success => _success;

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>借出实例</summary>
    /// <returns>对象实例</returns>
    public virtual T Get()
    {
        var sw = ReferenceEquals(Log, Logger.Null) ? null : Stopwatch.StartNew();
        Interlocked.Increment(ref _total);

        var success = false;
        Item? pi;
        do
        {
            if (_free.TryPop(out pi) || _free2.TryDequeue(out pi))
            {
                Interlocked.Decrement(ref _freeCount);
                success = true;
            }
            else
            {
                var count = BusyCount;
                if (Max > 0 && count >= Max)
                {
                    var msg = $"申请失败，已有 {count:n0} 达到或超过最大值 {Max:n0}";
                    WriteLog("Acquire Max " + msg);
                    throw new Exception(Name + " " + msg);
                }

                pi = new Item { Value = OnCreate() };
                if (count == 0) Init();
                Interlocked.Increment(ref _newCount);
                success = false;
            }
        } while (pi.Value == null || !OnGet(pi.Value));

        pi.LastTime = TimerX.Now;
        _busy.TryAdd(pi.Value, pi);

        Interlocked.Increment(ref _busyCount);
        if (success) Interlocked.Increment(ref _success);

        if (sw != null)
        {
            sw.Stop();
            var ms = sw.Elapsed.TotalMilliseconds;
            _cost = _cost < 0.001 ? ms : (_cost * 3 + ms) / 4;
        }

        return pi.Value;
    }

    /// <summary>获取包装项，Dispose 时自动归还</summary>
    /// <returns>包装项</returns>
    public PoolItem<T> GetItem() => new(this, Get());

    /// <summary>归还实例</summary>
    /// <param name="value">对象实例</param>
    /// <returns>是否归还成功</returns>
    [Obsolete("Please use Return from 2024-02-01")]
    public virtual Boolean Put(T value) => Return(value);

    /// <summary>归还实例</summary>
    /// <param name="value">对象实例</param>
    /// <returns>是否归还成功</returns>
    public virtual Boolean Return(T value)
    {
        if (value == null) return false;
        if (!_busy.TryRemove(value, out var pi))
        {
            Interlocked.Increment(ref _releaseCount);
            return false;
        }

        Interlocked.Decrement(ref _busyCount);

        if (!OnReturn(value))
        {
            Interlocked.Increment(ref _releaseCount);
            return false;
        }

        if (value is DisposeBase db && db.Disposed)
        {
            Interlocked.Increment(ref _releaseCount);
            return false;
        }

        if (_freeCount < Min)
            _free.Push(pi);
        else
            _free2.Enqueue(pi);

        pi.LastTime = TimerX.Now;
        Interlocked.Increment(ref _freeCount);

        if (IdleTime > 0 || AllIdleTime > 0) StartTimer();
        return true;
    }

    /// <summary>清空资源池</summary>
    /// <returns>清理数量</returns>
    public virtual Int32 Clear()
    {
        var count = _freeCount + _busyCount;

        while (_free.TryPop(out var pi)) OnDispose(pi.Value);
        while (_free2.TryDequeue(out var pi)) OnDispose(pi.Value);
        _freeCount = 0;

        foreach (var item in _busy)
        {
            OnDispose(item.Key);
        }

        _busy.Clear();
        _busyCount = 0;
        return count;
    }

    /// <summary>释放资源</summary>
    /// <param name="disposing">是否由 Dispose 调用</param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        _timer?.Dispose();
        _timer = null;

        WriteLog($"Dispose {typeof(T).FullName} FreeCount={FreeCount:n0} BusyCount={BusyCount:n0} Total={Total:n0}");
        Clear();
    }

    /// <summary>借出时是否可用</summary>
    /// <param name="value">对象实例</param>
    /// <returns>是否可用</returns>
    protected virtual Boolean OnGet(T value) => true;

    /// <summary>归还时是否可用</summary>
    /// <param name="value">对象实例</param>
    /// <returns>是否可用</returns>
    protected virtual Boolean OnReturn(T value) => true;

    /// <summary>销毁实例</summary>
    /// <param name="value">对象实例</param>
    protected virtual void OnDispose(T? value) => value.TryDispose();

    /// <summary>创建实例</summary>
    /// <returns>对象实例</returns>
    protected virtual T? OnCreate()
    {
        if (_factory != null) return _factory();

        throw new InvalidOperationException($"ObjectPool<{typeof(T).FullName}> 未配置工厂委托。AOT 环境下请通过构造函数传入 factory，或在派生类中重写 OnCreate。");
    }

    /// <summary>写日志</summary>
    /// <param name="format">格式化模板</param>
    /// <param name="args">格式化参数</param>
    public void WriteLog(String format, params Object?[] args)
    {
        if (Log == null || !Log.Enable || LogLevel.Info < Log.Level) return;
        Log.Info(XXTrace.FormatScope(LogScope, "ObjectPool", Name + ".", format), args);
    }

    private void Init()
    {
        if (_inited) return;

        lock (_sync)
        {
            if (_inited) return;
            _inited = true;

            WriteLog($"Init {typeof(T).FullName} Min={Min} Max={Max} IdleTime={IdleTime}s AllIdleTime={AllIdleTime}s");
        }
    }

    private void StartTimer()
    {
        if (_timer != null) return;
        lock (_sync)
        {
            if (_timer != null) return;

            _timer = new TimerX(Work, null, 5000, 5000) { Async = true };
        }
    }

    private void Work(Object? state)
    {
        var count = 0;

        if (AllIdleTime > 0 && !_busy.IsEmpty)
        {
            var exp = TimerX.Now.AddSeconds(-AllIdleTime);
            foreach (var item in _busy)
            {
                if (item.Value.LastTime < exp && _busy.TryRemove(item.Key, out _))
                    Interlocked.Decrement(ref _busyCount);
            }
        }

        if (IdleTime > 0 && !_free2.IsEmpty && FreeCount + BusyCount > Min)
        {
            var exp = TimerX.Now.AddSeconds(-IdleTime);
            while (_free2.TryPeek(out var pi) && pi.LastTime < exp)
            {
                if (_free2.TryDequeue(out var pi2))
                {
                    if (pi2.LastTime < exp)
                    {
                        pi2.Value.TryDispose();
                        count++;
                        Interlocked.Decrement(ref _freeCount);
                    }
                    else
                    {
                        _free2.Enqueue(pi2);
                    }
                }
            }
        }

        if (AllIdleTime > 0 && !_free.IsEmpty)
        {
            var exp = TimerX.Now.AddSeconds(-AllIdleTime);
            while (_free.TryPeek(out var pi) && pi.LastTime < exp)
            {
                if (_free.TryPop(out var pi2))
                {
                    if (pi2.LastTime < exp)
                    {
                        pi2.Value.TryDispose();
                        count++;
                        Interlocked.Decrement(ref _freeCount);
                    }
                    else
                    {
                        _free.Push(pi2);
                    }
                }
            }
        }

        var ncount = _newCount;
        var fcount = _releaseCount;
        if (count > 0 || ncount > 0 || fcount > 0)
        {
            Interlocked.Add(ref _newCount, -ncount);
            Interlocked.Add(ref _releaseCount, -fcount);

            var p = Total == 0 ? 0 : (Double)Success / Total;
            WriteLog("Release New={6:n0} Release={7:n0} Free={0} Busy={1} 清除过期资源 {2:n0} 项。总请求 {3:n0} 次，命中 {4:p2}，平均 {5:n2}us", FreeCount, BusyCount, count, Total, p, _cost * 1000, ncount, fcount);
        }
    }
}

/// <summary>资源池包装项，Dispose 时自动归还资源到池中</summary>
/// <typeparam name="T">池化类型</typeparam>
public class PoolItem<T> : DisposeBase
{
    /// <summary>实例化包装项</summary>
    /// <param name="pool">所属对象池</param>
    /// <param name="value">对象实例</param>
    public PoolItem(IPool<T> pool, T value)
    {
        Pool = pool;
        Value = value;
    }

    /// <summary>对象实例</summary>
    public T Value { get; }

    /// <summary>所属对象池</summary>
    public IPool<T> Pool { get; }

    /// <summary>释放资源</summary>
    /// <param name="disposing">是否由 Dispose 调用</param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);
        Pool.Return(Value);
    }
}