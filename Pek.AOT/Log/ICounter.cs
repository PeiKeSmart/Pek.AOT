using System.Diagnostics;

namespace Pek.Log;

/// <summary>性能计数器接口</summary>
public interface ICounter
{
    /// <summary>数值</summary>
    Int64 Value { get; }

    /// <summary>次数</summary>
    Int64 Times { get; }

    /// <summary>速度</summary>
    Int64 Speed { get; }

    /// <summary>平均耗时，单位 us</summary>
    Int64 Cost { get; }

    /// <summary>增加</summary>
    /// <param name="value">增加的数量</param>
    /// <param name="usCost">耗时，单位 us</param>
    void Increment(Int64 value, Int64 usCost);
}

/// <summary>计数器助手</summary>
public static class CounterHelper
{
    private static readonly Double TickFrequency = 1_000_000d / Stopwatch.Frequency;

    /// <summary>开始计时</summary>
    /// <param name="counter">计数器</param>
    /// <returns>起始时间戳</returns>
    public static Int64 StartCount(this ICounter? counter) => counter == null ? 0 : Stopwatch.GetTimestamp();

    /// <summary>结束计时</summary>
    /// <param name="counter">计数器</param>
    /// <param name="startTicks">起始时间戳</param>
    /// <returns>耗时，单位 us</returns>
    public static Int64 StopCount(this ICounter? counter, Int64? startTicks)
    {
        if (counter == null || startTicks == null || startTicks <= 0) return 0;

        var ticks = Stopwatch.GetTimestamp() - startTicks.Value;
        var usCost = (Int64)(ticks * TickFrequency);
        counter.Increment(1, usCost);

        return usCost;
    }
}