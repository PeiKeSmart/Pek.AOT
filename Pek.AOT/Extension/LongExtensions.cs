namespace Pek;

/// <summary>
/// 长整型(<see cref="Int64"/>) 扩展
/// </summary>
public static class LongExtensions
{
    #region Times(执行n次指定操作)

    /// <summary>
    /// 执行n次指定操作，基于底层long值
    /// </summary>
    /// <param name="value">值</param>
    /// <param name="action">操作-委托</param>
    public static void Times(this Int64 value, Action action)
    {
        for (var i = 0; i < value; i++)
        {
            action();
        }
    }
    /// <summary>
    /// 执行n次指定操作，基于底层long值
    /// </summary>
    /// <param name="value">值</param>
    /// <param name="action">操作-委托</param>
    public static void Times(this Int64 value, Action<Int64> action)
    {
        for (var i = 0L; i < value; i++)
        {
            action(i);
        }
    }

    #endregion

    #region IsEven(是否偶数)

    /// <summary>
    /// 是否偶数
    /// </summary>
    /// <param name="value">值</param>
    /// <returns></returns>
    public static Boolean IsEven(this Int64 value)
    {
        return value % 2 == 0;
    }

    #endregion

    #region IsOdd(是否奇数)

    /// <summary>
    /// 是否奇数
    /// </summary>
    /// <param name="value">值</param>
    /// <returns></returns>
    public static Boolean IsOdd(this Int64 value)
    {
        return value % 2 != 0;
    }

    #endregion

    #region InRange(判断值是否在指定范围内)

    /// <summary>
    /// 判断值是否在指定范围内
    /// </summary>
    /// <param name="value">值</param>
    /// <param name="minValue">最小值</param>
    /// <param name="maxValue">最大值</param>
    /// <returns></returns>
    public static Boolean InRange(this Int64 value, Int64 minValue, Int64 maxValue)
    {
        return (value >= minValue && value <= maxValue);
    }

    /// <summary>
    /// 判断值是否在指定范围内，否则返回默认值
    /// </summary>
    /// <param name="value">值</param>
    /// <param name="minValue">最小值</param>
    /// <param name="maxValue">最大值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns></returns>
    public static Int64 InRange(this Int64 value, Int64 minValue, Int64 maxValue, Int64 defaultValue)
    {
        return value.InRange(minValue, maxValue) ? value : defaultValue;
    }

    #endregion

    #region IsPrime(是否质数)

    /// <summary>
    /// 是否质数（素数），一个质数（或素数）是具有两个不同约束的自然数：1和它本身
    /// </summary>
    /// <param name="value">值</param>
    /// <returns></returns>
    public static Boolean IsPrime(this Int64 value)
    {
        if ((value & 1) == 0)
        {
            if (value == 2)
            {
                return true;
            }
            return false;
        }
        for (var i = 3L; (i * i) <= value; i += 2)
        {
            if ((value % i) == 0)
            {
                return false;
            }
        }
        return value != 1;
    }

    #endregion

    #region ToOrdinal(数值转换为顺序序号)

    /// <summary>
    /// 将数值转换为顺序序号，（英语序号）
    /// </summary>
    /// <param name="i">值</param>
    /// <returns>返回的字符串包含序号标记毗邻的数字表示</returns>
    public static String ToOrdinal(this Int64 i)
    {
        var suffix = "th";
        switch (i % 100)
        {
            case 11:
            case 12:
            case 13:
                break;
            default:
                switch (i % 10)
                {
                    case 1:
                        suffix = "st";
                        break;
                    case 2:
                        suffix = "nd";
                        break;
                    case 3:
                        suffix = "rd";
                        break;
                }
                break;
        }
        return $"{i}{suffix}";
    }

    /// <summary>
    /// 将数值转换为指定格式的序号字符串，（英语序号）
    /// </summary>
    /// <param name="i">值</param>
    /// <param name="format">自定义格式</param>
    /// <returns>返回的字符串包含序号标记毗邻的数字表示</returns>
    public static String ToOrdinal(this Int64 i, String format)
    {
        return String.Format(format, i.ToOrdinal());
    }

    #endregion

    #region Days(获取日期间隔)

    /// <summary>
    /// 获取日期间隔，根据数值获取时间间隔
    /// </summary>
    /// <param name="days">值</param>
    /// <returns>日期间隔</returns>
    public static TimeSpan Days(this Int64 days)
    {
        return TimeSpan.FromDays(days);
    }

    #endregion

    #region Hours(获取小时间隔)

    /// <summary>
    /// 获取小时间隔，根据数值获取时间间隔
    /// </summary>
    /// <param name="hours">值</param>
    /// <returns>小时间隔</returns>
    public static TimeSpan Hours(this Int64 hours)
    {
        return TimeSpan.FromHours(hours);
    }

    #endregion

    #region Minutes(获取分钟间隔)

    /// <summary>
    /// 获取分钟间隔，根据数值获取时间间隔
    /// </summary>
    /// <param name="minutes">值</param>
    /// <returns>分钟间隔</returns>
    public static TimeSpan Minutes(this Int64 minutes)
    {
        return TimeSpan.FromMinutes(minutes);
    }

    #endregion

    #region Seconds(获取秒间隔)

    /// <summary>
    /// 获取秒间隔，根据数值获取时间间隔
    /// </summary>
    /// <param name="seconds">long</param>
    /// <returns>秒间隔</returns>
    public static TimeSpan Seconds(this Int64 seconds)
    {
        return TimeSpan.FromSeconds(seconds);
    }

    #endregion

    #region Milliseconds(获取毫秒间隔)

    /// <summary>
    /// 获取毫秒间隔，根据数值获取时间间隔
    /// </summary>
    /// <param name="milliseconds">long</param>
    /// <returns>毫秒间隔</returns>
    public static TimeSpan Milliseconds(this Int64 milliseconds)
    {
        return TimeSpan.FromMilliseconds(milliseconds);
    }

    #endregion

    #region Ticks(获取刻度间隔)

    /// <summary>
    /// 获取刻度间隔，根据数值获取时间间隔
    /// </summary>
    /// <param name="ticks">long</param>
    /// <returns>刻度间隔</returns>
    public static TimeSpan Ticks(this Int64 ticks)
    {
        return TimeSpan.FromTicks(ticks);
    }

    #endregion

    #region ToDateTime(将给定Unix时间戳转换为DateTime时间)

    /// <summary>
    /// 将给定 Unix 时间戳 转换为 DateTime 时间。
    /// </summary>
    /// <param name="unixTimeStamp">Unix 时间戳。</param>
    /// <returns></returns>
    public static DateTime ToDateTime(this Int64 unixTimeStamp)
    {
        // 上游依赖 Pek.Timing.DateTimeExtensions.Date1970，用 BCL 等价替换
        var value = (unixTimeStamp + 8 * 60 * 60) * 10000000L;
        return DateTime.UnixEpoch.AddTicks(value);
    }

    #endregion
}
