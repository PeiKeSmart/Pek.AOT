using System.Text;

namespace Pek;

/// <summary>日期扩展。AOT 安全版</summary>
public static class DateTimeExtensions
{
    /// <summary>获取格式化字符串，格式："yyyy-MM-dd HH:mm:ss"</summary>
    public static String ToDateTimeString(this DateTime dateTime, Boolean isRemoveSecond = false) => dateTime.ToString(isRemoveSecond ? "yyyy-MM-dd HH:mm" : "yyyy-MM-dd HH:mm:ss");

    /// <summary>获取格式化字符串，格式："yyyy-MM-dd HH:mm:ss"</summary>
    public static String ToDateTimeString(this DateTime? dateTime, Boolean isRemoveSecond = false) => dateTime == null ? String.Empty : ToDateTimeString(dateTime.Value, isRemoveSecond);

    /// <summary>获取格式化字符串，格式："yyyy-MM-dd"</summary>
    public static String ToDateString(this DateTime dateTime) => dateTime.ToString("yyyy-MM-dd");

    /// <summary>获取格式化字符串，格式："yyyy-MM-dd"</summary>
    public static String ToDateString(this DateTime? dateTime) => dateTime == null ? String.Empty : ToDateString(dateTime.Value);

    /// <summary>获取格式化字符串，格式："HH:mm:ss"</summary>
    public static String ToTimeString(this DateTime dateTime) => dateTime.ToString("HH:mm:ss");

    /// <summary>获取格式化字符串，格式："HH:mm:ss"</summary>
    public static String ToTimeString(this DateTime? dateTime) => dateTime == null ? String.Empty : ToTimeString(dateTime.Value);

    /// <summary>获取格式化字符串，带毫秒，格式："yyyy-MM-dd HH:mm:ss.fff"</summary>
    public static String ToMillisecondString(this DateTime dateTime) => dateTime.ToString("yyyy-MM-dd HH:mm:ss.fff");

    /// <summary>获取格式化字符串，带毫秒，格式："yyyy-MM-dd HH:mm:ss.fff"</summary>
    public static String ToMillisecondString(this DateTime? dateTime) => dateTime == null ? String.Empty : ToMillisecondString(dateTime.Value);

    /// <summary>获取格式化字符串，格式："yyyy年MM月dd日"</summary>
    public static String ToChineseDateString(this DateTime dateTime) => $"{dateTime.Year}年{dateTime.Month}月{dateTime.Day}日";

    /// <summary>获取格式化字符串，格式："yyyy年MM月dd日"</summary>
    public static String ToChineseDateString(this DateTime? dateTime) => dateTime == null ? String.Empty : ToChineseDateString(dateTime.Value);

    /// <summary>获取格式化字符串，格式："yyyy年MM月dd日 HH时mm分"</summary>
    public static String ToChineseDateTimeString(this DateTime dateTime, Boolean isRemoveSecond = false)
    {
        var result = new StringBuilder();
        result.AppendFormat("{0}年{1}月{2}日", dateTime.Year, dateTime.Month, dateTime.Day);
        result.AppendFormat(" {0}时{1}分", dateTime.Hour, dateTime.Minute);
        if (isRemoveSecond == false) result.AppendFormat("{0}秒", dateTime.Second);
        return result.ToString();
    }

    /// <summary>获取格式化字符串，格式："yyyy年MM月dd日 HH时mm分"</summary>
    public static String ToChineseDateTimeString(this DateTime? dateTime, Boolean isRemoveSecond = false) => dateTime == null ? String.Empty : ToChineseDateTimeString(dateTime.Value, isRemoveSecond);

    /// <summary>获取 TimeSpan 描述</summary>
    public static String Description(this TimeSpan span)
    {
        var result = new StringBuilder();
        if (span.Days > 0) result.AppendFormat("{0}天", span.Days);
        if (span.Hours > 0) result.AppendFormat("{0}小时", span.Hours);
        if (span.Minutes > 0) result.AppendFormat("{0}分", span.Minutes);
        if (span.Seconds > 0) result.AppendFormat("{0}秒", span.Seconds);
        if (span.Milliseconds > 0) result.AppendFormat("{0}毫秒", span.Milliseconds);
        if (result.Length > 0) return result.ToString();
        return $"{span.TotalSeconds * 1000}毫秒";
    }
}
