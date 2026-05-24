using System.Text.Json;
using System.Text.Json.Serialization;

using NewLife.Reflection;

namespace Pek.Serialization;

/// <summary>面向 Type 的 Json 序列化转换器</summary>
/// <remarks>使用已注册类型集合中的全名进行序列化与反序列化，不走裸 Type.GetType。</remarks>
public class TypeConverter : JsonConverter<Type>
{
    /// <summary>读取类型</summary>
    /// <param name="reader">Json读取器</param>
    /// <param name="typeToConvert">目标类型</param>
    /// <param name="options">序列化选项</param>
    /// <returns>类型对象</returns>
    public override Type? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var typeName = reader.GetString();
        return String.IsNullOrEmpty(typeName) ? null : AssemblyX.GetType(typeName!, false);
    }

    /// <summary>写入类型</summary>
    /// <param name="writer">Json写入器</param>
    /// <param name="value">类型对象</param>
    /// <param name="options">序列化选项</param>
    public override void Write(Utf8JsonWriter writer, Type value, JsonSerializerOptions options) => writer.WriteStringValue(value.FullName);
}