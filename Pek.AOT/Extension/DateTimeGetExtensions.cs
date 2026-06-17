namespace Pek;

/// <summary>
/// 日期时间获取扩展方法（上游 Pek.Common Extensions.DateTime.Get 迁移）
/// </summary>
/// <remarks>
/// 本文件所有方法已迁移至同目录下的 DateTimeExtensions.cs（#region 日期获取），此处不再重复定义。
/// 包括：
/// - FirstDayOfYear / FirstDayOfQuarter / FirstDayOfMonth / FirstDayOfWeek
/// - LastDayOfYear / LastDayOfQuarter / LastDayOfMonth / LastDayOfWeek
/// 
/// AOT 兼容说明：全部使用纯 BCL DateTime API，无反射、无表达式树编译，完全 AOT 安全。
/// </remarks>
public static class DateTimeGetExtensions
{
    // 所有方法已迁移至 DateTimeExtensions.cs，参见该文件 #region 日期获取
}
