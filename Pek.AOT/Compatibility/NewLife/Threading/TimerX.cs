using System.Reflection;

using NewLife.Log;

namespace NewLife.Threading;

/// <summary>不可重入定时器</summary>
public class TimerX : ITimer, IDisposable
{
    private static readonly AsyncLocal<TimerX?> _current = new();
    private static TimerX? _nowTimer;
    private static DateTime _now;
    private static DateTime _baseTime;

    private readonly Cron[]? _crons;
    private readonly DateTime _createdAt;
    private readonly Boolean _isAsyncTask;
    private WeakReference? _state;
    private DateTime _absolutelyNext;
    private Int64 _nextTick;

    private TimerX(Object? target, MethodInfo method, Object? state, String? scheduler = null)
    {
        Target = new WeakReference(target);
        Method = method;
        State = state;
        Scheduler = String.IsNullOrWhiteSpace(scheduler) ? TimerScheduler.Default : TimerScheduler.Create(scheduler);
        _createdAt = Scheduler.GetNow();
        _nextTick = Runtime.TickCount64;
        _baseTime = _createdAt.AddMilliseconds(-_nextTick);
        TracerName = $"timer:{method.Name}";
    }

    /// <summary>实例化定时器</summary>
    /// <param name="callback">回调方法</param>
    /// <param name="state">状态对象</param>
    /// <param name="dueTime">首次延迟</param>
    /// <param name="period">周期</param>
    /// <param name="scheduler">调度器名称</param>
    public TimerX(TimerCallback callback, Object? state, Int32 dueTime, Int32 period, String? scheduler = null) : this(callback?.Target, callback?.Method ?? throw new ArgumentNullException(nameof(callback)), state, scheduler)
    {
        if (dueTime < 0) throw new ArgumentOutOfRangeException(nameof(dueTime));
        Period = period;
        Init(dueTime);
    }

    /// <summary>实例化异步定时器</summary>
    /// <param name="callback">异步回调</param>
    /// <param name="state">状态对象</param>
    /// <param name="dueTime">首次延迟</param>
    /// <param name="period">周期</param>
    /// <param name="scheduler">调度器名称</param>
    public TimerX(Func<Object, Task> callback, Object? state, Int32 dueTime, Int32 period, String? scheduler = null) : this(callback?.Target, callback?.Method ?? throw new ArgumentNullException(nameof(callback)), state, scheduler)
    {
        if (dueTime < 0) throw new ArgumentOutOfRangeException(nameof(dueTime));
        _isAsyncTask = true;
        Async = true;
        Period = period;
        Init(dueTime);
    }

    /// <summary>实例化绝对定时器</summary>
    /// <param name="callback">回调方法</param>
    /// <param name="state">状态对象</param>
    /// <param name="startTime">开始时间</param>
    /// <param name="period">周期</param>
    /// <param name="scheduler">调度器名称</param>
    public TimerX(TimerCallback callback, Object? state, DateTime startTime, Int32 period, String? scheduler = null) : this(callback?.Target, callback?.Method ?? throw new ArgumentNullException(nameof(callback)), state, scheduler)
    {
        if (startTime <= DateTime.MinValue) throw new ArgumentOutOfRangeException(nameof(startTime));
        if (period <= 0) throw new ArgumentOutOfRangeException(nameof(period));

        Period = period;
        Absolutely = true;
        var now = Scheduler.GetNow();
        var next = startTime;
        while (next < now) next = next.AddMilliseconds(period);
        _absolutelyNext = next;
        Init((Int64)(next - now).TotalMilliseconds);
    }

    /// <summary>实例化绝对异步定时器</summary>
    /// <param name="callback">异步回调</param>
    /// <param name="state">状态对象</param>
    /// <param name="startTime">开始时间</param>
    /// <param name="period">周期</param>
    /// <param name="scheduler">调度器名称</param>
    public TimerX(Func<Object, Task> callback, Object? state, DateTime startTime, Int32 period, String? scheduler = null) : this(callback?.Target, callback?.Method ?? throw new ArgumentNullException(nameof(callback)), state, scheduler)
    {
        if (startTime <= DateTime.MinValue) throw new ArgumentOutOfRangeException(nameof(startTime));
        if (period <= 0) throw new ArgumentOutOfRangeException(nameof(period));

        _isAsyncTask = true;
        Async = true;
        Period = period;
        Absolutely = true;
        var now = Scheduler.GetNow();
        var next = startTime;
        while (next < now) next = next.AddMilliseconds(period);
        _absolutelyNext = next;
        Init((Int64)(next - now).TotalMilliseconds);
    }

    /// <summary>实例化 Cron 定时器</summary>
    /// <param name="callback">回调方法</param>
    /// <param name="state">状态对象</param>
    /// <param name="cronExpression">Cron 表达式</param>
    /// <param name="scheduler">调度器名称</param>
    public TimerX(TimerCallback callback, Object? state, String cronExpression, String? scheduler = null) : this(callback?.Target, callback?.Method ?? throw new ArgumentNullException(nameof(callback)), state, scheduler)
    {
        if (String.IsNullOrWhiteSpace(cronExpression)) throw new ArgumentNullException(nameof(cronExpression));
        _crons = ParseCrons(cronExpression);
        Absolutely = true;
        var now = Scheduler.GetNow();
        var next = _crons.Min(e => e.GetNext(now));
        _absolutelyNext = next;
        Init((Int64)(next - now).TotalMilliseconds);
    }

    /// <summary>实例化 Cron 异步定时器</summary>
    /// <param name="callback">异步回调</param>
    /// <param name="state">状态对象</param>
    /// <param name="cronExpression">Cron 表达式</param>
    /// <param name="scheduler">调度器名称</param>
    public TimerX(Func<Object, Task> callback, Object? state, String cronExpression, String? scheduler = null) : this(callback?.Target, callback?.Method ?? throw new ArgumentNullException(nameof(callback)), state, scheduler)
    {
        if (String.IsNullOrWhiteSpace(cronExpression)) throw new ArgumentNullException(nameof(cronExpression));
        _isAsyncTask = true;
        Async = true;
        Absolutely = true;
        _crons = ParseCrons(cronExpression);
        var now = Scheduler.GetNow();
        var next = _crons.Min(e => e.GetNext(now));
        _absolutelyNext = next;
        Init((Int64)(next - now).TotalMilliseconds);
    }

    /// <summary>当前定时器</summary>
    public static TimerX? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }

    /// <summary>当前时间缓存</summary>
    public static DateTime Now
    {
        get
        {
            if (_nowTimer == null)
            {
                lock (TimerScheduler.Default)
                {
                    if (_nowTimer == null)
                    {
                        _now = TimerScheduler.Default.GetNow();
                        _nowTimer = new TimerX(CopyNow, null, 0, 500);
                    }
                }
            }

            return _now;
        }
    }

    /// <summary>定时器编号</summary>
    public Int32 Id { get; internal set; }

    /// <summary>所属调度器</summary>
    public TimerScheduler Scheduler { get; }

    /// <summary>目标对象</summary>
    internal WeakReference Target { get; }

    /// <summary>委托方法</summary>
    internal MethodInfo Method { get; }

    /// <summary>是否异步任务方法</summary>
    internal Boolean IsAsyncTask => _isAsyncTask;

    /// <summary>状态对象</summary>
    public Object? State
    {
        get => _state != null && _state.IsAlive ? _state.Target : null;
        set
        {
            if (_state == null)
                _state = new WeakReference(value);
            else
                _state.Target = value;
        }
    }

    /// <summary>下一次执行时刻的 Tick</summary>
    public Int64 NextTick => _nextTick;

    /// <summary>下一次执行时间</summary>
    public DateTime NextTime => _baseTime.AddMilliseconds(_nextTick);

    /// <summary>执行次数</summary>
    public Int32 Timers { get; internal set; }

    /// <summary>周期毫秒数</summary>
    public Int32 Period { get; set; }

    /// <summary>是否异步执行</summary>
    public Boolean Async { get; set; }

    /// <summary>是否绝对时间执行</summary>
    public Boolean Absolutely { get; set; }

    /// <summary>是否正在执行</summary>
    public Boolean Calling { get; internal set; }

    /// <summary>平均耗时</summary>
    public Int32 Cost { get; internal set; }

    /// <summary>Cron 集合</summary>
    public Cron[]? Crons => _crons;

    /// <summary>追踪器</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>追踪名称</summary>
    public String TracerName { get; set; }

    /// <summary>是否已设置下一次时间</summary>
    internal Boolean hasSetNext;

    /// <summary>更改定时器</summary>
    /// <param name="dueTime">首次延迟</param>
    /// <param name="period">周期</param>
    /// <returns>是否成功</returns>
    public Boolean Change(TimeSpan dueTime, TimeSpan period)
    {
        if (Absolutely) return false;
        if (Crons != null && Crons.Length > 0) return false;

        if (period.TotalMilliseconds <= 0)
        {
            Dispose();
            return true;
        }

        Period = (Int32)period.TotalMilliseconds;
        if (dueTime.TotalMilliseconds >= 0) SetNext((Int32)dueTime.TotalMilliseconds);
        return true;
    }

    /// <summary>设置下一次执行时间</summary>
    /// <param name="milliseconds">毫秒数</param>
    public void SetNext(Int32 milliseconds)
    {
        SetNextTick(milliseconds);
        hasSetNext = true;
        Scheduler.Wake();
    }

    /// <summary>销毁定时器</summary>
    public void Dispose()
    {
        Scheduler.Remove(this, "Dispose");
        GC.SuppressFinalize(this);
    }

    /// <summary>异步销毁定时器</summary>
    /// <returns>已完成任务</returns>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>延迟执行回调</summary>
    /// <param name="callback">回调方法</param>
    /// <param name="milliseconds">延迟毫秒</param>
    /// <returns>定时器实例</returns>
    public static TimerX Delay(TimerCallback callback, Int32 milliseconds) => new(callback, null, milliseconds, 0) { Async = true };

    /// <summary>转为文本</summary>
    /// <returns>定时器描述</returns>
    public override String ToString()
    {
        var periodText = _crons != null ? String.Join(';', _crons.Select(e => e.ToString())) : $"{Period}ms";
        return $"[{Id}]{Method.DeclaringType?.Name}.{Method.Name} ({periodText})";
    }

    internal Int32 SetAndGetNextTime()
    {
        var period = Period;
        var nowTick = Runtime.TickCount64;
        if (hasSetNext)
        {
            var diff = (Int32)(_nextTick - nowTick);
            return diff > 0 ? diff : period;
        }

        if (Absolutely)
        {
            var now = Scheduler.GetNow();
            DateTime next;
            if (_crons != null)
            {
                next = _crons.Min(e => e.GetNext(now));
                if ((next - now).TotalMilliseconds < 1000) next = _crons.Min(e => e.GetNext(next));
            }
            else
            {
                next = _absolutelyNext;
                while (next < now) next = next.AddMilliseconds(period);
            }

            _absolutelyNext = next;
            var diff = (Int32)Math.Round((next - now).TotalMilliseconds);
            SetNextTick(diff);
            return diff > 0 ? diff : period;
        }

        SetNextTick(period);
        return period;
    }

    private static void CopyNow(Object? state) => _now = TimerScheduler.Default.GetNow();

    private static Cron[] ParseCrons(String expression)
    {
        var list = new List<Cron>();
        foreach (var item in expression.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var cron = new Cron();
            if (!cron.Parse(item)) throw new ArgumentException($"Invalid Cron expression[{item}]", nameof(expression));
            list.Add(cron);
        }

        return [.. list];
    }

    private void Init(Int64 milliseconds)
    {
        SetNextTick(milliseconds);
        Scheduler.Add(this);
    }

    private void SetNextTick(Int64 milliseconds)
    {
        var tick = Runtime.TickCount64;
        _baseTime = Scheduler.GetNow().AddMilliseconds(-tick);
        _nextTick = tick + milliseconds;
    }
}
