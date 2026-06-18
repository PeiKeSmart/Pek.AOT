using System;
using System.Collections.Generic;

namespace Pek.Timing;

/// <summary>时间段</summary>
public class DateTimeRange
{
    /// <summary>实例化时间段</summary>
    /// <param name="start">起始时间</param>
    /// <param name="end">结束时间</param>
    public DateTimeRange(DateTime start, DateTime end)
    {
        if (start > end) throw new ArgumentException("开始时间不能大于结束时间");

        Start = start;
        End = end;
    }

    /// <summary>起始时间</summary>
    public DateTime Start { get; }

    /// <summary>结束时间</summary>
    public DateTime End { get; }

    /// <summary>是否相交</summary>
    /// <param name="start">起始时间</param>
    /// <param name="end">结束时间</param>
    public Boolean HasIntersect(DateTime start, DateTime end) => HasIntersect(new DateTimeRange(start, end));

    /// <summary>是否相交</summary>
    /// <param name="range">另一个时间段</param>
    public Boolean HasIntersect(DateTimeRange range) => Start >= range.Start && Start <= range.End || End >= range.Start && End <= range.End;

    /// <summary>相交时间段</summary>
    /// <param name="range">另一个时间段</param>
    /// <returns>是否相交及相交时间段</returns>
    public (Boolean intersected, DateTimeRange? range) Intersect(DateTimeRange range)
    {
        if (HasIntersect(range))
        {
            var list = new List<DateTime> { Start, range.Start, End, range.End };
            list.Sort();
            return (true, new DateTimeRange(list[1], list[2]));
        }

        return (false, null);
    }

    /// <summary>相交时间段</summary>
    /// <param name="start">起始时间</param>
    /// <param name="end">结束时间</param>
    /// <returns>是否相交及相交时间段</returns>
    public (Boolean intersected, DateTimeRange? range) Intersect(DateTime start, DateTime end) => Intersect(new DateTimeRange(start, end));

    /// <summary>是否包含时间段</summary>
    /// <param name="range">另一个时间段</param>
    public Boolean Contains(DateTimeRange range) => range.Start >= Start && range.Start <= End && range.End >= Start && range.End <= End;

    /// <summary>是否包含时间段</summary>
    /// <param name="start">起始时间</param>
    /// <param name="end">结束时间</param>
    public Boolean Contains(DateTime start, DateTime end) => Contains(new DateTimeRange(start, end));

    /// <summary>是否在时间段内</summary>
    /// <param name="range">另一个时间段</param>
    public Boolean In(DateTimeRange range) => Start >= range.Start && Start <= range.End && End >= range.Start && End <= range.End;

    /// <summary>是否在时间段内</summary>
    /// <param name="start">起始时间</param>
    /// <param name="end">结束时间</param>
    public Boolean In(DateTime start, DateTime end) => In(new DateTimeRange(start, end));

    /// <summary>合并时间段</summary>
    /// <param name="range">另一个时间段</param>
    public DateTimeRange Union(DateTimeRange range)
    {
        if (HasIntersect(range))
        {
            var list = new List<DateTime> { Start, range.Start, End, range.End };
            list.Sort();
            return new DateTimeRange(list[0], list[3]);
        }

        throw new InvalidOperationException("不相交的时间段不能合并");
    }

    /// <summary>合并时间段</summary>
    /// <param name="start">起始时间</param>
    /// <param name="end">结束时间</param>
    public DateTimeRange Union(DateTime start, DateTime end) => Union(new DateTimeRange(start, end));

    /// <summary>返回表示当前时间段的字符串</summary>
    /// <returns>yyyy-MM-dd HH:mm:ss~yyyy-MM-dd HH:mm:ss 格式的字符串</returns>
    public override String ToString() => $"{Start:yyyy-MM-dd HH:mm:ss}~{End:yyyy-MM-dd HH:mm:ss}";
}
