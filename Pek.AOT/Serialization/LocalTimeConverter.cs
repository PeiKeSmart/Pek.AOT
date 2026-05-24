using System.Text.Json;
using System.Text.Json.Serialization;

using Pek;

namespace Pek.Serialization;

/// <summary>本地时间转换器</summary>
/// <remarks>
/// 符合标准格式 yyyy-MM-dd HH:mm:ss。
/// 序列化时忽略时区信息；反序列化时如果输入带有时区，则统一转换为本地时间，避免跨框架默认行为差异。
/// </remarks>
public class LocalTimeConverter : JsonConverter<DateTime>
{
    /// <summary>时间日期格式</summary>
    public String DateTimeFormat { get; set; } = "O";

    /// <summary>读取</summary>
    /// <param name="reader">Json读取器</param>
    /// <param name="typeToConvert">目标类型</param>
    /// <param name="options">序列化选项</param>
    /// <returns>本地时间</returns>
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var unixTime))
            return unixTime.ToDateTime().ToLocalTime();

        var value = reader.GetString();
        if (String.IsNullOrWhiteSpace(value)) return DateTime.MinValue;
        if (DateTimeOffset.TryParse(value, out var dto)) return dto.LocalDateTime;

        var utc = false;
        if (value.EndsWith("UTC", StringComparison.OrdinalIgnoreCase))
        {
            value = value[..^3].Trim();
            utc = true;
        }

        if (!DateTime.TryParse(value, out var dateTime)) return DateTime.MinValue;
        return utc ? dateTime.ToLocalTime() : dateTime;
    }

    /// <summary>写入</summary>
    /// <param name="writer">Json写入器</param>
    /// <param name="value">时间值</param>
    /// <param name="options">序列化选项</param>
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString(DateTimeFormat, System.Globalization.CultureInfo.InvariantCulture));
}