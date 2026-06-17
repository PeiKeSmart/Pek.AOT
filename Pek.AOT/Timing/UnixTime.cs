namespace Pek.Timing;

/// <summary>Unix 时间操作。AOT 安全版</summary>
public static class UnixTime
{
    /// <summary>Unix 纪元时间</summary>
    public static DateTime EpochTime = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>转换为 Unix 时间戳（毫秒）</summary>
    /// <param name="isContainMillisecond">是否包含毫秒。默认 true 返回毫秒，false 返回秒</param>
    public static Int64 ToTimestamp(Boolean isContainMillisecond = true) => ToTimestamp(DateTime.Now, isContainMillisecond);

    /// <summary>转换指定时间为 Unix 时间戳</summary>
    /// <param name="dateTime">时间</param>
    /// <param name="isContainMillisecond">是否包含毫秒。默认 true 返回毫秒，false 返回秒</param>
    public static Int64 ToTimestamp(DateTime dateTime, Boolean isContainMillisecond = true)
    {
        if (dateTime.Kind == DateTimeKind.Utc)
            return Convert.ToInt64((dateTime - EpochTime).TotalMilliseconds / (isContainMillisecond ? 1 : 1000));

        return Convert.ToInt64((TimeZoneInfo.ConvertTimeToUtc(dateTime) - EpochTime).TotalMilliseconds / (isContainMillisecond ? 1 : 1000));
    }

    /// <summary>Unix 时间戳转为 DateTime 对象（本地时间）</summary>
    /// <param name="timestamp">时间戳</param>
    /// <param name="isContainMillisecond">是否包含毫秒。默认 true 表示毫秒，false 表示秒</param>
    public static DateTime ToDateTime(Int64 timestamp, Boolean isContainMillisecond = true)
    {
        if (isContainMillisecond)
            return EpochTime.AddMilliseconds(timestamp).ToLocalTime();

        return EpochTime.AddSeconds(timestamp).ToLocalTime();
    }

    /// <summary>Unix 时间戳转为指定时区的 DateTime 对象</summary>
    /// <param name="timestamp">时间戳</param>
    /// <param name="timeZoneOffset">时区偏移。如 +1/-1 等</param>
    /// <param name="isContainMillisecond">是否包含毫秒。默认 true 表示毫秒，false 表示秒</param>
    public static DateTime ToDateTime(Int64 timestamp, String timeZoneOffset, Boolean isContainMillisecond = true)
    {
        DateTime utcDateTime;
        if (isContainMillisecond)
            utcDateTime = EpochTime.AddMilliseconds(timestamp);
        else
            utcDateTime = EpochTime.AddSeconds(timestamp);

        if (TryParseTimeZoneOffset(timeZoneOffset, out var offset))
            return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, TimeZoneInfo.CreateCustomTimeZone(timeZoneOffset, offset, timeZoneOffset, timeZoneOffset));

        throw new ArgumentException("Invalid time zone offset format.");
    }

    /// <summary>尝试解析时区偏移字符串</summary>
    /// <param name="timeZoneOffset">时区偏移。如 +1/-1 等</param>
    /// <param name="offset">解析后的 TimeSpan 对象</param>
    private static Boolean TryParseTimeZoneOffset(String timeZoneOffset, out TimeSpan offset)
    {
        offset = TimeSpan.Zero;
        if (String.IsNullOrEmpty(timeZoneOffset)) return false;

        var isNegative = timeZoneOffset[0] == '-';
        if (timeZoneOffset[0] != '+' && !isNegative) return false;

        if (Int32.TryParse(timeZoneOffset.Substring(1), out var hours))
        {
            offset = new TimeSpan(hours * (isNegative ? -1 : 1), 0, 0);
            return true;
        }

        return false;
    }

    /// <summary>毫秒时间戳转为 UTC DateTimeOffset（时区偏移为 0）</summary>
    /// <param name="timestamp">时间戳（毫秒）</param>
    public static DateTimeOffset ToUtcDateTime(Int64 timestamp)
    {
        var utcTime = DateTimeOffset.FromUnixTimeMilliseconds(timestamp);
        return utcTime.ToOffset(TimeSpan.Zero);
    }

    /// <summary>当前时间转为 UTC DateTimeOffset（时区偏移为 0）</summary>
    public static DateTimeOffset ToUtcZeroDateTime()
    {
        var localTime = DateTimeOffset.Now;
        return TimeZoneInfo.ConvertTime(localTime, TimeZoneInfo.Utc);
    }

    /// <summary>指定 DateTimeOffset 转为 UTC（时区偏移为 0）</summary>
    /// <param name="dateTime">带时区的 UTC 时间</param>
    public static DateTimeOffset ToUtcZeroDateTime(DateTimeOffset dateTime)
    {
        return TimeZoneInfo.ConvertTime(dateTime, TimeZoneInfo.Utc);
    }

    /// <summary>指定 DateTime 转为 UTC 本地时间</summary>
    /// <param name="dateTime">UTC 时间（时区为 0）</param>
    public static DateTimeOffset ToUtcZeroDateTime(DateTime dateTime)
    {
        var inputTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        return inputTime.ToUniversalTime().ToLocalTime();
    }
}
