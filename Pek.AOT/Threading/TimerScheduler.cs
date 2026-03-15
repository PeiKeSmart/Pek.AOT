using System.Diagnostics;
using Pek.Log;

namespace Pek.Threading;

/// <summary>定时器调度器</summary>
public class TimerScheduler : IDisposable
{
    private const String LogScope = "Pek.Threading";

    private static readonly Dictionary<String, TimerScheduler> _cache = [];

    [ThreadStatic]
    private static TimerScheduler? _current;

    private Thread? _thread;
    private AutoResetEvent? _waitHandle;
    private TimerX[] _timers = [];
    private Int32 _nextId;
    private Int32 _period = 10;
    private volatile Boolean _disposing;

    static TimerScheduler()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => ClearAll();
    }

    private TimerScheduler(String name) => Name = name;

    /// <summary>默认调度器</summary>
    public static TimerScheduler Default { get; } = Create("Default");

    /// <summary>当前调度器</summary>
    public static TimerScheduler? Current => _current;

    /// <summary>全局时间提供者</summary>
    public static TimeProvider GlobalTimeProvider { get; set; } = TimeProvider.System;

    /// <summary>调度器名称</summary>
    public String Name { get; }

    /// <summary>定时器数量</summary>
    public Int32 Count { get; private set; }

    /// <summary>最大耗时阈值</summary>
    public Int32 MaxCost { get; set; } = 500;

    /// <summary>时间提供者</summary>
    public TimeProvider? TimeProvider { get; set; }

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>创建调度器</summary>
    /// <param name="name">调度器名称</param>
    /// <returns>调度器实例</returns>
    public static TimerScheduler Create(String name)
    {
        lock (_cache)
        {
            if (_cache.TryGetValue(name, out var scheduler)) return scheduler;

            scheduler = new TimerScheduler(name);
            if (_cache.TryGetValue("Default", out var defaultScheduler)) scheduler.Log = defaultScheduler.Log;
            _cache[name] = scheduler;
            return scheduler;
        }
    }

    /// <summary>添加定时器</summary>
    /// <param name="timer">定时器实例</param>
    public void Add(TimerX timer)
    {
        if (timer == null) throw new ArgumentNullException(nameof(timer));
        if (_disposing) throw new ObjectDisposedException(nameof(TimerScheduler));

        using var span = DefaultTracer.Instance?.NewSpan("timer:Add", new { Name, timer = timer.ToString() });

        lock (this)
        {
            if (_timers.Contains(timer)) return;

            timer.Id = Interlocked.Increment(ref _nextId);
            var list = _timers.ToList();
            list.Add(timer);
            _timers = [.. list];
            Count++;

            if (_thread == null)
            {
                _thread = new Thread(Process)
                {
                    Name = Name == "Default" ? "T" : Name,
                    IsBackground = true
                };
                _thread.Start();
                WriteLog("启动定时调度器：{0}", Name);
            }

            Wake();
        }
    }

    /// <summary>移除定时器</summary>
    /// <param name="timer">定时器实例</param>
    /// <param name="reason">移除原因</param>
    public void Remove(TimerX timer, String reason)
    {
        if (timer == null || timer.Id == 0) return;

        using var span = DefaultTracer.Instance?.NewSpan("timer:Remove", new { Name, timer = timer.ToString(), reason });

        lock (this)
        {
            timer.Id = 0;
            var list = _timers.ToList();
            if (!list.Remove(timer)) return;

            _timers = [.. list];
            Count--;
        }

        WriteLog("Timer.Remove {0} reason:{1}", timer, reason);
    }

    /// <summary>唤醒调度线程</summary>
    public void Wake()
    {
        var handle = _waitHandle;
        if (handle == null) return;

        try
        {
            handle.Set();
        }
        catch { }
    }

    /// <summary>获取当前时间</summary>
    /// <returns>当前时间</returns>
    public DateTime GetNow() => (TimeProvider ?? GlobalTimeProvider).GetUtcNow().LocalDateTime;

    /// <summary>释放资源</summary>
    public void Dispose()
    {
        if (_disposing) return;

        _disposing = true;
        WriteLog("正在销毁定时调度器：{0}", Name);
        Wake();

        var thread = _thread;
        if (thread != null && thread.IsAlive) thread.Join(5000);

        foreach (var item in _timers.ToArray())
        {
            item.Dispose();
        }

        _waitHandle?.Dispose();
        _waitHandle = null;
        _thread = null;
    }

    /// <summary>转为文本</summary>
    /// <returns>调度器名称</returns>
    public override String ToString() => Name;

    private static void ClearAll()
    {
        lock (_cache)
        {
            WriteGlobalLog("ClearAll Count={0}", _cache.Count);
            foreach (var item in _cache.Values.ToArray())
            {
                item.Dispose();
            }

            _cache.Clear();
        }
    }

    private void Process()
    {
        _current = this;
        while (!_disposing)
        {
            var timers = _timers;
            if (timers.Length == 0 && _period == 60_000)
            {
                _thread = null;
                break;
            }

            try
            {
                var now = Runtime.TickCount64;
                _period = 60_000;
                foreach (var timer in timers)
                {
                    if (_disposing) break;
                    if (timer.Calling || !CheckTime(timer, now)) continue;

                    timer.Calling = true;
                    if (timer.IsAsyncTask)
                    {
                        Task.Run(() => ExecuteAsync(timer));
                    }
                    else if (timer.Async)
                    {
                        ThreadPool.UnsafeQueueUserWorkItem(state => Execute(state), timer);
                    }
                    else
                    {
                        Execute(timer);
                    }
                }
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }

            if (_disposing) break;

            _waitHandle ??= new AutoResetEvent(false);
            if (_period > 0) _waitHandle.WaitOne(_period);
        }

        WriteLog("调度线程已退出：{0}", Name);
    }

    private Boolean CheckTime(TimerX timer, Int64 now)
    {
        if (timer.Period is < 10 and > 0)
        {
            XXTrace.WriteScope(LogScope, "TimerScheduler", "关闭过小周期任务 Timer={0} Period={1}ms", timer, timer.Period);
            timer.Dispose();
            return false;
        }

        var diff = timer.NextTick - now;
        if (diff > 0)
        {
            if (diff < _period) _period = (Int32)diff;
            return false;
        }

        return true;
    }

    private void Execute(Object? state)
    {
        if (state is not TimerX timer) return;

        TimerX.Current = timer;
        WriteLogEventArgs.CurrentThreadName = Name == "Default" ? "T" : Name;
        timer.hasSetNext = false;
        DefaultSpan.Current = null;
        using var span = timer.Tracer?.NewSpan(timer.TracerName ?? "timer:Execute", timer.Timers + "");
        var watch = Stopwatch.StartNew();
        try
        {
            if (!timer.TryGetTarget(out _))
            {
                Remove(timer, "委托已不存在（GC回收委托所在对象）");
                timer.Dispose();
                return;
            }

            timer.Invoke();
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            XTrace.WriteException(ex);
        }
        finally
        {
            watch.Stop();
            OnExecuted(timer, (Int32)watch.ElapsedMilliseconds);
        }
    }

    private async Task ExecuteAsync(TimerX timer)
    {
        TimerX.Current = timer;
        WriteLogEventArgs.CurrentThreadName = Name == "Default" ? "T" : Name;
        timer.hasSetNext = false;
        DefaultSpan.Current = null;
        using var span = timer.Tracer?.NewSpan(timer.TracerName ?? "timer:ExecuteAsync", timer.Timers + "");
        var watch = Stopwatch.StartNew();
        try
        {
            if (!timer.TryGetTarget(out _))
            {
                Remove(timer, "委托已不存在（GC回收委托所在对象）");
                timer.Dispose();
                return;
            }

            await timer.InvokeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            XTrace.WriteException(ex);
        }
        finally
        {
            watch.Stop();
            OnExecuted(timer, (Int32)watch.ElapsedMilliseconds);
        }
    }

    private void OnExecuted(TimerX timer, Int32 cost)
    {
        timer.Cost = timer.Cost == 0 ? cost : (timer.Cost + cost) / 2;
        if (cost > MaxCost && !timer.Async && !timer.IsAsyncTask)
            XXTrace.WriteScope(LogScope, "TimerScheduler", "任务执行耗时过长 Timer={0} Cost={1:n0}ms Suggest=Async", timer, cost);

        timer.Timers++;
        OnFinish(timer);
        timer.Calling = false;
        TimerX.Current = null;
        WriteLogEventArgs.CurrentThreadName = null;
        Wake();
    }

    private void OnFinish(TimerX timer)
    {
        var period = timer.SetAndGetNextTime();
        if (period <= 0)
        {
            Remove(timer, "Period<=0");
            timer.Dispose();
        }
        else if (period < _period)
        {
            _period = period;
        }
    }

    private void WriteLog(String format, params Object?[] args)
    {
        if (Log == null || !Log.Enable || LogLevel.Info < Log.Level) return;
        Log.Info(XXTrace.FormatScope(LogScope, "TimerScheduler", Name + " ", format), args);
    }

    private static void WriteGlobalLog(String format, params Object?[] args)
    {
        var log = XTrace.Log;
        if (log == null || !log.Enable || LogLevel.Info < log.Level) return;
        log.Info(XXTrace.FormatScope(LogScope, nameof(TimerScheduler), format), args);
    }
}
