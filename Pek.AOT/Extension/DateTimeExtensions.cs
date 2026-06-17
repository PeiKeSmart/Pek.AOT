using System.Globalization;
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

    #region 日期获取（上游 Pek.Common Extensions.DateTime.Get 迁移）

    /// <summary>
    /// 获取年份的第一天
    /// </summary>
    /// <param name="dt">时间</param>
    public static DateTime FirstDayOfYear(this DateTime dt) => dt.SetDate(dt.Year, 1, 1);

    /// <summary>
    /// 获取季度的第一天
    /// </summary>
    /// <param name="dt">时间</param>
    public static DateTime FirstDayOfQuarter(this DateTime dt)
    {
        var currentQuarter = (dt.Month - 1) / 3 + 1;
        var firstDay = new DateTime(dt.Year, 3 * currentQuarter - 2, 1);
        return dt.SetDate(firstDay.Year, firstDay.Month, firstDay.Day);
    }

    /// <summary>
    /// 获取月份的第一天
    /// </summary>
    /// <param name="dt">时间</param>
    public static DateTime FirstDayOfMonth(this DateTime dt) => dt.SetDay(1);

    /// <summary>
    /// 获取星期的第一天
    /// </summary>
    /// <param name="dt">时间</param>
    public static DateTime FirstDayOfWeek(this DateTime dt)
    {
        var currentCulture = CultureInfo.CurrentCulture;
        var firstDayOfWeek = currentCulture.DateTimeFormat.FirstDayOfWeek;
        var offset = dt.DayOfWeek - firstDayOfWeek < 0 ? 7 : 0;
        var numberOfDaysSinceBeginningOfTheWeek = dt.DayOfWeek + offset - firstDayOfWeek;
        return dt.AddDays(-numberOfDaysSinceBeginningOfTheWeek);
    }

    /// <summary>
    /// 获取年份的最后一天
    /// </summary>
    /// <param name="dt">时间</param>
    public static DateTime LastDayOfYear(this DateTime dt) => dt.SetDate(dt.Year, 12, 31);

    /// <summary>
    /// 获取季度的最后一天
    /// </summary>
    /// <param name="dt">时间</param>
    public static DateTime LastDayOfQuarter(this DateTime dt)
    {
        var currentQuarter = (dt.Month - 1) / 3 + 1;
        var firstDay = new DateTime(dt.Year, 3 * currentQuarter - 2, 1);
        return firstDay.SetMonth(firstDay.Month + 2).LastDayOfMonth();
    }

    /// <summary>
    /// 获取月份的最后一天
    /// </summary>
    /// <param name="dt">时间</param>
    public static DateTime LastDayOfMonth(this DateTime dt) => dt.SetDay(DateTime.DaysInMonth(dt.Year, dt.Month));

    /// <summary>
    /// 获取星期的最后一天
    /// </summary>
    /// <param name="dt">时间</param>
    public static DateTime LastDayOfWeek(this DateTime dt) => dt.FirstDayOfWeek().AddDays(6);

    #endregion

    #region 日期设置（上游 Pek.Common Extensions.DateTime.Set 迁移，DateTimeFactory.Create 替换为 new DateTime）

    /// <summary>
    /// 设置时间
    /// </summary>
    /// <param name="dt">时间</param>
    /// <param name="hour">时</param>
    public static DateTime SetTime(this DateTime dt, Int32 hour) => new DateTime(dt.Year, dt.Month, dt.Day, hour, dt.Minute, dt.Second, dt.Millisecond, dt.Kind);

    /// <summary>
    /// 设置时间
    /// </summary>
    /// <param name="dt">时间</param>
    /// <param name="hour">时</param>
    /// <param name="minute">分</param>
    public static DateTime SetTime(this DateTime dt, Int32 hour, Int32 minute) => new DateTime(dt.Year, dt.Month, dt.Day, hour, minute, dt.Second, dt.Millisecond, dt.Kind);

    /// <summary>
    /// 设置时间
    /// </summary>
    /// <param name="dt">时间</param>
    /// <param name="hour">时</param>
    /// <param name="minute">分</param>
    /// <param name="second">秒</param>
    public static DateTime SetTime(this DateTime dt, Int32 hour, Int32 minute, Int32 second) => new DateTime(dt.Year, dt.Month, dt.Day, hour, minute, second, dt.Millisecond, dt.Kind);

    /// <summary>
    /// 设置时间
    /// </summary>
    /// <param name="dt">时间</param>
    /// <param name="hour">时</param>
    /// <param name="minute">分</param>
    /// <param name="second">秒</param>
    /// <param name="millisecond">毫秒</param>
    public static DateTime SetTime(this DateTime dt, Int32 hour, Int32 minute, Int32 second, Int32 millisecond) => new DateTime(dt.Year, dt.Month, dt.Day, hour, minute, second, millisecond, dt.Kind);

    /// <summary>
    /// 设置时间 - 小时
    /// </summary>
    /// <param name="dt">时间</param>
    /// <param name="hour">时</param>
    public static DateTime SetHour(this DateTime dt, Int32 hour) => new DateTime(dt.Year, dt.Month, dt.Day, hour, dt.Minute, dt.Second, dt.Millisecond, dt.Kind);

    /// <summary>
    /// 设置时间 - 分钟
    /// </summary>
    /// <param name="dt">时间</param>
    /// <param name="minute">分</param>
    public static DateTime SetMinute(this DateTime dt, Int32 minute) => new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, minute, dt.Second, dt.Millisecond, dt.Kind);

    /// <summary>
    /// 设置时间 - 秒
    /// </summary>
    /// <param name="dt">时间</param>
    /// <param name="second">秒</param>
    public static DateTime SetSecond(this DateTime dt, Int32 second) => new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, second, dt.Millisecond, dt.Kind);

    /// <summary>
    /// 设置时间 - 毫秒
    /// </summary>
    /// <param name="dt">时间</param>
    /// <param name="millisecond">毫秒</param>
    public static DateTime SetMillisecond(this DateTime dt, Int32 millisecond) => new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, millisecond, dt.Kind);

    /// <summary>
    /// 设置时间为凌晨0点
    /// </summary>
    /// <param name="dt">时间</param>
    public static DateTime Midnight(this DateTime dt) => throw new NotImplementedException();

    /// <summary>
    /// 设置时间为中午12点
    /// </summary>
    /// <param name="dt">时间</param>
    public static DateTime Noon(this DateTime dt) => dt.SetTime(12, 0, 0, 0);

    /// <summary>
    /// 设置日期
    /// </summary>
    /// <param name="dt">时间</param>
    /// <param name="year">年</param>
    public static DateTime SetDate(this DateTime dt, Int32 year) => new DateTime(year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond, dt.Kind);

    /// <summary>
    /// 设置日期
    /// </summary>
    /// <param name="dt">时间</param>
    /// <param name="year">年</param>
    /// <param name="month">月</param>
    public static DateTime SetDate(this DateTime dt, Int32 year, Int32 month) => new DateTime(year, month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond, dt.Kind);

    /// <summary>
    /// 设置日期
    /// </summary>
    /// <param name="dt">时间</param>
    /// <param name="year">年</param>
    /// <param name="month">月</param>
    /// <param name="day">日</param>
    public static DateTime SetDate(this DateTime dt, Int32 year, Int32 month, Int32 day) => new DateTime(year, month, day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond, dt.Kind);

    /// <summary>
    /// 设置日期 - 年
    /// </summary>
    /// <param name="dt">时间</param>
    /// <param name="year">年</param>
    public static DateTime SetYear(this DateTime dt, Int32 year) => new DateTime(year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond, dt.Kind);

    /// <summary>
    /// 设置日期 - 月
    /// </summary>
    /// <param name="dt">时间</param>
    /// <param name="month">月</param>
    public static DateTime SetMonth(this DateTime dt, Int32 month) => new DateTime(dt.Year, month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond, dt.Kind);

    /// <summary>
    /// 设置日期 - 日
    /// </summary>
    /// <param name="dt">时间</param>
    /// <param name="day">日</param>
    public static DateTime SetDay(this DateTime dt, Int32 day) => new DateTime(dt.Year, dt.Month, day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond, dt.Kind);

    /// <summary>
    /// 设置日期种类。本地/UTC
    /// </summary>
    /// <param name="dt">时间</param>
    /// <param name="kind">日期种类</param>
    public static DateTime SetKind(this DateTime dt, DateTimeKind kind) => new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond, kind);

    #endregion
}
