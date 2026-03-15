using System.Diagnostics;

namespace Pek.Log;

/// <summary>统计代码的时间消耗</summary>
public class TimeCost : DisposeBase
{
    private const String LogScope = "Pek.Log";
    private Stopwatch? _stopwatch;

    /// <summary>名称</summary>
    public String Name { get; set; }

    /// <summary>最大时间。毫秒</summary>
    public Int32 Max { get; set; }

    /// <summary>日志输出</summary>
    public ILog Log { get; set; }

    /// <summary>指定最大执行时间来构造一个代码时间统计</summary>
    /// <param name="name">名称</param>
    /// <param name="msMax">最大时间</param>
    public TimeCost(String name, Int32 msMax = 0)
    {
        Name = name;
        Max = msMax;
        Log = XTrace.Log;

        if (msMax >= 0) Start();
    }

    /// <summary>销毁</summary>
    /// <param name="disposing">是否显式释放</param>
    protected override void Dispose(Boolean disposing)
    {
        Stop();
        base.Dispose(disposing);
    }

    /// <summary>开始</summary>
    public void Start()
    {
        if (_stopwatch == null)
            _stopwatch = Stopwatch.StartNew();
        else if (!_stopwatch.IsRunning)
            _stopwatch.Start();
    }

    /// <summary>停止</summary>
    public void Stop()
    {
        if (_stopwatch == null) return;

        _stopwatch.Stop();
        if (Log == Logger.Null || !Log.Enable) return;

        var ms = _stopwatch.ElapsedMilliseconds;
        if (ms <= Max) return;

        if (Max > 0)
            Log.Warn(XXTrace.FormatScope(LogScope, nameof(TimeCost), "{0}执行过长警告 {1:n0}ms > {2:n0}ms"), Name, ms, Max);
        else
            Log.Warn(XXTrace.FormatScope(LogScope, nameof(TimeCost), "{0}执行 {1:n0}ms"), Name, ms);
    }
}