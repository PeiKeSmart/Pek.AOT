namespace Pek;

/// <summary>
/// 星期(<see cref="DayOfWeek"/>) 扩展方法
/// </summary>
public static class DayOfWeekExtensions
{
    /// <summary>
    /// 将 <see cref="DayOfWeek"/> 转换为 <see cref="Int32"/>
    /// </summary>
    /// <param name="dayOfWeek">星期</param>
    public static Int32 ToInt(this DayOfWeek dayOfWeek)
    {
        switch (dayOfWeek)
        {
            case DayOfWeek.Sunday:
                return 1;

            case DayOfWeek.Monday:
                return 2;

            case DayOfWeek.Tuesday:
                return 3;

            case DayOfWeek.Wednesday:
                return 4;

            case DayOfWeek.Thursday:
                return 5;

            case DayOfWeek.Friday:
                return 6;

            case DayOfWeek.Saturday:
                return 7;

            default:
                return 0;
        }
    }
}
