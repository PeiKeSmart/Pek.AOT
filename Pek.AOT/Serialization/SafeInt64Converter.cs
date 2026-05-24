using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pek.Serialization;

/// <summary>安全的 Int64 转换器</summary>
public sealed class SafeInt64Converter : JsonConverter<Int64>
{
    private const Int64 JsSafeMax = 9_007_199_254_740_991;
    private const Int64 JsSafeMin = -9_007_199_254_740_991;

    /// <summary>读取 Int64</summary>
    /// <param name="reader">Json读取器</param>
    /// <param name="typeToConvert">目标类型</param>
    /// <param name="options">序列化选项</param>
    /// <returns>整数值</returns>
    public override Int64 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
    {
        JsonTokenType.Number => reader.GetInt64(),
        JsonTokenType.String => Int64.Parse(reader.GetString() ?? String.Empty, CultureInfo.InvariantCulture),
        _ => throw new JsonException("Invalid token for Int64"),
    };

    /// <summary>写入 Int64</summary>
    /// <param name="writer">Json写入器</param>
    /// <param name="value">整数值</param>
    /// <param name="options">序列化选项</param>
    public override void Write(Utf8JsonWriter writer, Int64 value, JsonSerializerOptions options)
    {
        if (value > JsSafeMax || value < JsSafeMin)
            writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
        else
            writer.WriteNumberValue(value);
    }
}

/// <summary>安全的 UInt64 转换器</summary>
public sealed class SafeUInt64Converter : JsonConverter<UInt64>
{
    private const UInt64 JsSafeMax = 9_007_199_254_740_991;

    /// <summary>读取 UInt64</summary>
    /// <param name="reader">Json读取器</param>
    /// <param name="typeToConvert">目标类型</param>
    /// <param name="options">序列化选项</param>
    /// <returns>整数值</returns>
    public override UInt64 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
    {
        JsonTokenType.Number => reader.GetUInt64(),
        JsonTokenType.String => UInt64.Parse(reader.GetString() ?? String.Empty, CultureInfo.InvariantCulture),
        _ => throw new JsonException("Invalid token for UInt64"),
    };

    /// <summary>写入 UInt64</summary>
    /// <param name="writer">Json写入器</param>
    /// <param name="value">整数值</param>
    /// <param name="options">序列化选项</param>
    public override void Write(Utf8JsonWriter writer, UInt64 value, JsonSerializerOptions options)
    {
        if (value > JsSafeMax)
            writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
        else
            writer.WriteNumberValue(value);
    }
}