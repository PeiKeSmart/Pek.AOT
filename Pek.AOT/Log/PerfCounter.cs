using System.Diagnostics;
using Pek.Threading;

namespace Pek.Log;

/// <summary>性能计数器。次数、TPS、平均耗时</summary>
public class PerfCounter : DisposeBase, ICounter
{
    private Int64 _value;
    private Int64 _times;
    private Int64 _totalCost;
    private TimerX? _timer;
    private Stopwatch? _stopwatch;
    private Int64 _lastValue;
    private Int64 _lastTimes;
    private Int64 _lastCost;
    private Int64[] _queueSpeed = new Int64[60];
    private Int64[] _queueCost = new Int64[60];
    private Int32 _queueIndex = -1;

    /// <summary>是否启用。默认 true</summary>
    public Boolean Enable { get; set; } = true;

    /// <summary>数值</summary>
    public Int64 Value => _value;

    /// <summary>次数</summary>
    public Int64 Times => _times;

    /// <summary>采样间隔，默认 1000 毫秒</summary>
    public Int32 Interval { get; set; } = 1000;

    /// <summary>持续采样时间，默认 60 秒</summary>
    public Int32 Duration { get; set; } = 60;

    /// <summary>当前速度</summary>
    public Int64 Speed { get; private set; }

    /// <summary>最大速度</summary>
    public Int64 MaxSpeed => _queueSpeed.Length == 0 ? 0 : _queueSpeed.Max();

    /// <summary>最后一个采样周期的平均耗时，单位 us</summary>
    public Int64 Cost { get; private set; }

    /// <summary>持续采样时间内的最大平均耗时，单位 us</summary>
    public Int64 MaxCost => _queueCost.Length == 0 ? 0 : _queueCost.Max();

    /// <summary>增加</summary>
    /// <param name="value">增加的数量</param>
    /// <param name="usCost">耗时，单位 us</param>
    public void Increment(Int64 value, Int64 usCost)
    {
        if (!Enable) return;

        Interlocked.Add(ref _value, value);
        Interlocked.Increment(ref _times);
        if (usCost > 0) Interlocked.Add(ref _totalCost, usCost);

        if (_timer != null) return;

        lock (this)
        {
            _timer ??= new TimerX(DoWork, null, Interval, Interval) { Async = true };
        }
    }

    /// <summary>销毁</summary>
    /// <param name="disposing">是否显式释放</param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);
        _timer?.Dispose();
    }

    /// <summary>已重载。输出统计信息</summary>
    /// <returns>统计信息</returns>
    public override String ToString()
    {
        if (Cost >= 1000)
            return $"{Times:n0}/{MaxSpeed:n0}/{Speed:n0}tps/{MaxCost / 1000:n0}/{Cost / 1000:n0}ms";
        if (Cost > 0)
            return $"{Times:n0}/{MaxSpeed:n0}/{Speed:n0}tps/{MaxCost:n0}/{Cost:n0}us";

        return $"{Times:n0}/{MaxSpeed:n0}/{Speed:n0}tps";
    }

    private void DoWork(Object? state)
    {
        var len = Math.Max(1, Duration * 1000 / Interval);
        if (_queueSpeed.Length != len) _queueSpeed = new Int64[len];
        if (_queueCost.Length != len) _queueCost = new Int64[len];

        var speed = 0L;
        if (_stopwatch == null)
            _stopwatch = Stopwatch.StartNew();
        else
        {
            var ms = _stopwatch.Elapsed.TotalMilliseconds;
            _stopwatch.Restart();
            if (ms > 0) speed = (Int64)((Value - _lastValue) * 1000 / ms);
        }

        _lastValue = Value;

        var times = Times - _lastTimes;
        var cost = times == 0 ? Cost : (_totalCost - _lastCost) / times;
        _lastTimes = Times;
        _lastCost = _totalCost;

        Speed = speed;
        Cost = cost;

        _queueIndex++;
        if (_queueIndex >= len) _queueIndex = 0;
        _queueSpeed[_queueIndex] = speed;
        _queueCost[_queueIndex] = cost;
    }
}