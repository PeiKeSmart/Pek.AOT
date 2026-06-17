namespace Pek;

/// <summary>
/// 日期时间设置扩展方法（上游 Pek.Common Extensions.DateTime.Set 迁移）
/// </summary>
/// <remarks>
/// 本文件所有方法已迁移至同目录下的 DateTimeExtensions.cs（#region 日期设置），此处不再重复定义。
/// 上游使用 DateTimeFactory.Create，AOT 版已替换为 new DateTime(...) 直接构造。
/// 包括：
/// - SetTime / SetHour / SetMinute / SetSecond / SetMillisecond
/// - Midnight（上游抛 NotImplementedException，AOT 保持一致）
/// - Noon
/// - SetDate / SetYear / SetMonth / SetDay / SetKind
/// 
/// AOT 兼容说明：全部使用 new DateTime(...) 直接构造，无任何外部依赖，完全 AOT 安全。
/// </remarks>
public static class DateTimeSetExtensions
{
    // 所有方法已迁移至 DateTimeExtensions.cs，参见该文件 #region 日期设置
}
