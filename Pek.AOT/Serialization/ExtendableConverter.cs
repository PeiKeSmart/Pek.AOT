using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using Pek.Data;

namespace Pek.Serialization;

/// <summary>支持 IExtend 的可扩展对象序列化转换器</summary>
/// <remarks>
/// AOT 版本不再自己做通用反射读写，而是委托当前 JsonHelper 主链：
/// 已注册 JsonTypeInfo 的强类型部分按主链处理，扩展字段由 SystemJson 内部的 IExtend 兼容逻辑补齐。
/// </remarks>
public class ExtendableConverter : JsonConverter<Object>
{
    /// <summary>是否可以转换</summary>
    /// <param name="typeToConvert">待转换类型</param>
    /// <returns>是否支持</returns>
    public override Boolean CanConvert(Type typeToConvert) => typeof(IExtend).IsAssignableFrom(typeToConvert);

    /// <summary>读取</summary>
    /// <param name="reader">Json读取器</param>
    /// <param name="typeToConvert">目标类型</param>
    /// <param name="options">序列化选项</param>
    /// <returns>转换结果</returns>
    public override Object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return JsonHelper.Default.Read(document.RootElement.GetRawText(), typeToConvert);
    }

    /// <summary>写入</summary>
    /// <param name="writer">Json写入器</param>
    /// <param name="value">对象</param>
    /// <param name="options">序列化选项</param>
    public override void Write(Utf8JsonWriter writer, Object value, JsonSerializerOptions options)
    {
        var json = JsonHelper.Default.Write(value, JsonHelper.Default.Options);
        var node = JsonNode.Parse(json);
        if (node == null)
            writer.WriteNullValue();
        else
            node.WriteTo(writer, options);
    }
}