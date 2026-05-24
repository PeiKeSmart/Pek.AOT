using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Pek.Serialization;

/// <summary>Json 反序列化时进行类型绑定。用于指定接口的实现类。</summary>
/// <typeparam name="TService">服务抽象类型</typeparam>
/// <typeparam name="TImplementation">实现类型</typeparam>
public class JsonConverter<TService, TImplementation> : JsonConverter<TService> where TImplementation : TService
{
    /// <summary>读取</summary>
    /// <param name="reader">Json读取器</param>
    /// <param name="typeToConvert">目标类型</param>
    /// <param name="options">序列化选项</param>
    /// <returns>实现对象</returns>
    public override TService? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return (TService?)JsonHelper.Default.Read(document.RootElement.GetRawText(), typeof(TImplementation));
    }

    /// <summary>写入</summary>
    /// <param name="writer">Json写入器</param>
    /// <param name="value">对象</param>
    /// <param name="options">序列化选项</param>
    public override void Write(Utf8JsonWriter writer, TService value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        var json = JsonHelper.Default.Write(value!, JsonHelper.Default.Options);
        var node = JsonNode.Parse(json);
        if (node == null)
            writer.WriteNullValue();
        else
            node.WriteTo(writer, options);
    }
}