using System.Text.Json.Serialization;

namespace Pek.Configuration;

/// <summary>
/// 通用配置JSON序列化上下文基类
/// 包含常用基本类型的序列化支持
/// </summary>
[JsonSerializable(typeof(String))]
[JsonSerializable(typeof(Boolean))]
[JsonSerializable(typeof(Int32))]
[JsonSerializable(typeof(Int64))]
[JsonSerializable(typeof(Double))]
[JsonSerializable(typeof(Decimal))]
[JsonSerializable(typeof(DateTime))]
[JsonSerializable(typeof(DateTimeOffset))]
[JsonSerializable(typeof(TimeSpan))]
[JsonSerializable(typeof(Guid))]
[JsonSerializable(typeof(List<String>))]
[JsonSerializable(typeof(List<Int32>))]
[JsonSerializable(typeof(List<Int64>))]
[JsonSerializable(typeof(List<Double>))]
[JsonSerializable(typeof(List<Decimal>))]
[JsonSerializable(typeof(List<Boolean>))]
[JsonSerializable(typeof(List<DateTime>))]
[JsonSerializable(typeof(List<Guid>))]
[JsonSerializable(typeof(Dictionary<String, String>))]
[JsonSerializable(typeof(Dictionary<String, Int32>))]
[JsonSerializable(typeof(Dictionary<String, Boolean>))]
[JsonSerializable(typeof(Dictionary<String, Object>))]
// 可空值类型支持
[JsonSerializable(typeof(Boolean?))]
[JsonSerializable(typeof(Int32?))]
[JsonSerializable(typeof(Int64?))]
[JsonSerializable(typeof(Double?))]
[JsonSerializable(typeof(Decimal?))]
[JsonSerializable(typeof(DateTime?))]
[JsonSerializable(typeof(DateTimeOffset?))]
[JsonSerializable(typeof(TimeSpan?))]
[JsonSerializable(typeof(Guid?))]
public partial class ConfigJsonContext : JsonSerializerContext
{
}