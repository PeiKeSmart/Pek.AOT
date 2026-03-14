using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Pek.Collections;
using Pek.Configuration;

namespace Pek.Serialization;

/// <summary>Json序列化接口</summary>
public interface IJsonHost
{
    /// <summary>服务提供者。用于反序列化时构造内部成员对象</summary>
    IServiceProvider ServiceProvider { get; set; }

    /// <summary>配置项</summary>
    JsonOptions Options { get; set; }

    /// <summary>写入对象，得到Json字符串</summary>
    /// <param name="value">对象</param>
    /// <param name="indented">是否缩进</param>
    /// <param name="nullValue">是否写空值</param>
    /// <param name="camelCase">是否驼峰命名</param>
    /// <returns>Json字符串</returns>
    String Write(Object value, Boolean indented = false, Boolean nullValue = true, Boolean camelCase = false);

    /// <summary>写入对象，得到Json字符串</summary>
    /// <param name="value">对象</param>
    /// <param name="jsonOptions">序列化选项</param>
    /// <returns>Json字符串</returns>
    String Write(Object value, JsonOptions jsonOptions);

    /// <summary>从Json字符串中读取对象</summary>
    /// <param name="json">Json字符串</param>
    /// <param name="type">目标类型</param>
    /// <returns>对象</returns>
    Object? Read(String json, Type type);

    /// <summary>类型转换</summary>
    /// <param name="obj">源对象</param>
    /// <param name="targetType">目标类型</param>
    /// <returns>转换结果</returns>
    Object? Convert(Object obj, Type targetType);

    /// <summary>分析Json字符串得到字典或列表</summary>
    /// <param name="json">Json字符串</param>
    /// <returns>解析结果</returns>
    Object? Parse(String json);

    /// <summary>分析Json字符串得到字典</summary>
    /// <param name="json">Json字符串</param>
    /// <returns>字典结果</returns>
    IDictionary<String, Object?>? Decode(String json);
}

/// <summary>Json助手</summary>
public static class JsonHelper
{
    private static readonly ConcurrentDictionary<Type, JsonTypeInfo> _typeInfos = new();

    /// <summary>默认实现</summary>
    public static IJsonHost Default { get; set; } = new SystemJson();

    /// <summary>注册类型信息</summary>
    /// <param name="typeInfo">类型信息</param>
    public static void Register(JsonTypeInfo typeInfo)
    {
        if (typeInfo == null) throw new ArgumentNullException(nameof(typeInfo));

        _typeInfos[typeInfo.Type] = typeInfo;
    }

    /// <summary>注册类型信息</summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="typeInfo">类型信息</param>
    public static void Register<T>(JsonTypeInfo<T> typeInfo) => Register((JsonTypeInfo)typeInfo);

    internal static Boolean TryGetTypeInfo(Type type, out JsonTypeInfo typeInfo)
    {
        if (_typeInfos.TryGetValue(type, out typeInfo!)) return true;

        if (ConfigManager.TryGetSerializerOptions(type, out var options))
        {
            typeInfo = ConfigManager.GetJsonTypeInfo(type, options);
            _typeInfos[type] = typeInfo;
            return true;
        }

        typeInfo = null!;
        return false;
    }

    /// <summary>写入对象，得到Json字符串</summary>
    /// <param name="value">对象</param>
    /// <param name="indented">是否缩进</param>
    /// <returns>Json字符串</returns>
    public static String ToJson(this Object value, Boolean indented = false) => Default.Write(value, indented);

    /// <summary>写入对象，得到Json字符串</summary>
    /// <param name="value">对象</param>
    /// <param name="indented">是否缩进</param>
    /// <param name="nullValue">是否写空值</param>
    /// <param name="camelCase">是否驼峰命名</param>
    /// <returns>Json字符串</returns>
    public static String ToJson(this Object value, Boolean indented, Boolean nullValue, Boolean camelCase) => Default.Write(value, indented, nullValue, camelCase);

    /// <summary>写入对象，得到Json字符串</summary>
    /// <param name="value">对象</param>
    /// <param name="jsonOptions">序列化选项</param>
    /// <returns>Json字符串</returns>
    public static String ToJson(this Object value, JsonOptions jsonOptions) => Default.Write(value, jsonOptions);

    /// <summary>从Json字符串中读取对象</summary>
    /// <param name="json">Json字符串</param>
    /// <param name="type">目标类型</param>
    /// <returns>对象</returns>
    public static Object? ToJsonEntity(this String json, Type type)
    {
        if (String.IsNullOrWhiteSpace(json)) return null;

        return Default.Read(json, type);
    }

    /// <summary>从Json字符串中读取对象</summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="json">Json字符串</param>
    /// <returns>对象</returns>
    public static T? ToJsonEntity<T>(this String json)
    {
        if (String.IsNullOrWhiteSpace(json)) return default;

        return (T?)Default.Read(json, typeof(T));
    }

    /// <summary>从Json字符串中反序列化对象</summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="jsonHost">Json主机</param>
    /// <param name="json">Json字符串</param>
    /// <returns>对象</returns>
    public static T? Read<T>(this IJsonHost jsonHost, String json) => (T?)jsonHost.Read(json, typeof(T));

    /// <summary>格式化Json文本</summary>
    /// <param name="json">Json字符串</param>
    /// <returns>格式化后的Json</returns>
    public static String Format(String json)
    {
        var builder = Pool.StringBuilder.Get();

        var escaping = false;
        var inQuotes = false;
        var indentation = 0;

        foreach (var ch in json)
        {
            if (escaping)
            {
                escaping = false;
                builder.Append(ch);
                continue;
            }

            if (ch == '\\')
            {
                escaping = true;
                builder.Append(ch);
            }
            else if (ch == '"')
            {
                inQuotes = !inQuotes;
                builder.Append(ch);
            }
            else if (!inQuotes)
            {
                if (ch == ',')
                {
                    builder.Append(ch);
                    builder.Append("\r\n");
                    builder.Append(' ', indentation * 2);
                }
                else if (ch is '[' or '{')
                {
                    builder.Append(ch);
                    builder.Append("\r\n");
                    builder.Append(' ', ++indentation * 2);
                }
                else if (ch is ']' or '}')
                {
                    builder.Append("\r\n");
                    builder.Append(' ', --indentation * 2);
                    builder.Append(ch);
                }
                else if (ch == ':')
                {
                    builder.Append(ch);
                    builder.Append(' ', 2);
                }
                else
                {
                    builder.Append(ch);
                }
            }
            else
            {
                builder.Append(ch);
            }
        }

        return builder.Return(true);
    }

    /// <summary>Json类型对象转换实体类</summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="obj">源对象</param>
    /// <returns>转换结果</returns>
    public static T? Convert<T>(Object obj) => Default.Convert<T>(obj);

    /// <summary>Json类型对象转换实体类</summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="jsonHost">Json主机</param>
    /// <param name="obj">源对象</param>
    /// <returns>转换结果</returns>
    public static T? Convert<T>(this IJsonHost jsonHost, Object obj)
    {
        if (obj == null) return default;
        if (obj is T result) return result;

        return (T?)jsonHost.Convert(obj, typeof(T));
    }

    /// <summary>分析Json字符串得到字典或列表</summary>
    /// <param name="json">Json字符串</param>
    /// <returns>解析结果</returns>
    public static Object? Parse(String json) => Default.Parse(json);

    /// <summary>分析Json字符串得到字典</summary>
    /// <param name="json">Json字符串</param>
    /// <returns>字典结果</returns>
    public static IDictionary<String, Object?>? DecodeJson(this String json) => Default.Decode(json);
}

/// <summary>System.Text.Json标准序列化</summary>
public class SystemJson : IJsonHost
{
    /// <summary>服务提供者。用于反序列化时构造内部成员对象</summary>
    public IServiceProvider ServiceProvider { get; set; } = NullServiceProvider.Instance;

    /// <summary>配置项</summary>
    public JsonOptions Options { get; set; } = new();

    /// <summary>写入对象，得到Json字符串</summary>
    /// <param name="value">对象</param>
    /// <param name="indented">是否缩进</param>
    /// <param name="nullValue">是否写空值</param>
    /// <param name="camelCase">是否驼峰命名</param>
    /// <returns>Json字符串</returns>
    public String Write(Object value, Boolean indented = false, Boolean nullValue = true, Boolean camelCase = false)
        => Write(value, new JsonOptions { WriteIndented = indented, IgnoreNullValues = !nullValue, CamelCase = camelCase });

    /// <summary>写入对象，得到Json字符串</summary>
    /// <param name="value">对象</param>
    /// <param name="jsonOptions">序列化选项</param>
    /// <returns>Json字符串</returns>
    public String Write(Object value, JsonOptions jsonOptions)
    {
        if (value == null) return "null";

        if (JsonHelper.TryGetTypeInfo(value.GetType(), out var typeInfo))
            return JsonSerializer.Serialize(value, typeInfo);

        return SerializeKnownValue(value, jsonOptions ?? Options);
    }

    /// <summary>从Json字符串中读取对象</summary>
    /// <param name="json">Json字符串</param>
    /// <param name="type">目标类型</param>
    /// <returns>对象</returns>
    public Object? Read(String json, Type type)
    {
        if (String.IsNullOrWhiteSpace(json)) return null;
        if (type == null) throw new ArgumentNullException(nameof(type));

        if (JsonHelper.TryGetTypeInfo(type, out var typeInfo))
            return JsonSerializer.Deserialize(json, typeInfo);

        return DeserializeKnownValue(json, type);
    }

    /// <summary>类型转换</summary>
    /// <param name="obj">源对象</param>
    /// <param name="targetType">目标类型</param>
    /// <returns>转换结果</returns>
    public Object? Convert(Object obj, Type targetType)
    {
        if (obj == null) return null;
        if (targetType == null) throw new ArgumentNullException(nameof(targetType));
        if (targetType.IsInstanceOfType(obj)) return obj;

        var nullableType = Nullable.GetUnderlyingType(targetType);
        var actualType = nullableType ?? targetType;

        if (obj is JsonNode node) return Read(node.ToJsonString(), actualType);
        if (obj is String str)
        {
            if (actualType == typeof(String)) return str;

            var trimmed = str.Trim();
            if (trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal) || trimmed == "null")
                return Read(trimmed, actualType);

            return ChangeScalarValue(trimmed, actualType);
        }

        if (actualType.IsEnum)
        {
            if (obj is String enumText) return Enum.Parse(actualType, enumText, true);
            return Enum.ToObject(actualType, System.Convert.ChangeType(obj, Enum.GetUnderlyingType(actualType), CultureInfo.InvariantCulture)!);
        }

        if (actualType == typeof(Guid)) return obj is Guid guid ? guid : Guid.Parse(System.Convert.ToString(obj, CultureInfo.InvariantCulture) ?? String.Empty);
        if (actualType == typeof(TimeSpan)) return obj is TimeSpan span ? span : TimeSpan.Parse(System.Convert.ToString(obj, CultureInfo.InvariantCulture) ?? String.Empty, CultureInfo.InvariantCulture);
        if (actualType == typeof(JsonNode) || actualType == typeof(JsonObject) || actualType == typeof(JsonArray))
            return obj as JsonNode ?? JsonNode.Parse(SerializeKnownValue(obj, Options));

        if (obj is IDictionary<String, Object?> or IList<Object?>)
        {
            if (actualType.IsInstanceOfType(obj)) return obj;
            throw new NotSupportedException($"Type {actualType.FullName} is not registered for AOT-safe JSON conversion. Register a JsonTypeInfo first.");
        }

        return System.Convert.ChangeType(obj, actualType, CultureInfo.InvariantCulture);
    }

    /// <summary>分析Json字符串得到字典或列表</summary>
    /// <param name="json">Json字符串</param>
    /// <returns>解析结果</returns>
    public Object? Parse(String json)
    {
        if (String.IsNullOrWhiteSpace(json)) return null;

        var node = JsonNode.Parse(json);
        return ConvertNode(node);
    }

    /// <summary>分析Json字符串得到字典</summary>
    /// <param name="json">Json字符串</param>
    /// <returns>字典结果</returns>
    public IDictionary<String, Object?>? Decode(String json) => Parse(json) as IDictionary<String, Object?>;

    private static String SerializeKnownValue(Object value, JsonOptions jsonOptions)
    {
        if (value is String stringValue) return SerializeString(stringValue);
        if (value is Boolean booleanValue) return booleanValue ? "true" : "false";
        if (value is Byte byteValue) return byteValue.ToString(CultureInfo.InvariantCulture);
        if (value is SByte sbyteValue) return sbyteValue.ToString(CultureInfo.InvariantCulture);
        if (value is Int16 int16Value) return int16Value.ToString(CultureInfo.InvariantCulture);
        if (value is UInt16 uint16Value) return uint16Value.ToString(CultureInfo.InvariantCulture);
        if (value is Int32 int32Value) return int32Value.ToString(CultureInfo.InvariantCulture);
        if (value is UInt32 uint32Value) return uint32Value.ToString(CultureInfo.InvariantCulture);
        if (value is Int64 int64Value) return jsonOptions.Int64AsString ? SerializeString(int64Value.ToString(CultureInfo.InvariantCulture)) : int64Value.ToString(CultureInfo.InvariantCulture);
        if (value is UInt64 uint64Value) return jsonOptions.Int64AsString ? SerializeString(uint64Value.ToString(CultureInfo.InvariantCulture)) : uint64Value.ToString(CultureInfo.InvariantCulture);
        if (value is Single singleValue) return singleValue.ToString("R", CultureInfo.InvariantCulture);
        if (value is Double doubleValue) return doubleValue.ToString("R", CultureInfo.InvariantCulture);
        if (value is Decimal decimalValue) return decimalValue.ToString(CultureInfo.InvariantCulture);
        if (value is DateTime dateTimeValue) return SerializeString(FormatDateTime(dateTimeValue, jsonOptions));
        if (value is DateTimeOffset dateTimeOffsetValue) return SerializeString(dateTimeOffsetValue.ToString("O", CultureInfo.InvariantCulture));
        if (value is TimeSpan timeSpanValue) return SerializeString(timeSpanValue.ToString("c", CultureInfo.InvariantCulture));
        if (value is Guid guidValue) return SerializeString(guidValue.ToString());
        if (value is Byte[] buffer) return SerializeString(System.Convert.ToBase64String(buffer));
        if (value is JsonNode node) return node.ToJsonString(CreateSerializerOptions(jsonOptions));
        if (value.GetType().IsEnum)
            return jsonOptions.EnumString ? SerializeString(value.ToString() ?? String.Empty) : System.Convert.ChangeType(value, Enum.GetUnderlyingType(value.GetType()), CultureInfo.InvariantCulture)?.ToString() ?? "0";

        throw new NotSupportedException($"Type {value.GetType().FullName} is not registered for AOT-safe JSON serialization. Register a JsonTypeInfo first.");
    }

    private static Object? DeserializeKnownValue(String json, Type type)
    {
        var nullableType = Nullable.GetUnderlyingType(type);
        var actualType = nullableType ?? type;

        if (actualType == typeof(JsonNode) || actualType == typeof(JsonObject) || actualType == typeof(JsonArray))
        {
            var jsonNode = JsonNode.Parse(json);
            if (actualType == typeof(JsonNode)) return jsonNode;
            if (actualType == typeof(JsonObject)) return jsonNode as JsonObject;
            if (actualType == typeof(JsonArray)) return jsonNode as JsonArray;
        }

        var node = JsonNode.Parse(json);
        if (node == null) return null;
        if (node is not JsonValue jsonValue)
            throw new NotSupportedException($"Type {actualType.FullName} is not registered for AOT-safe JSON deserialization. Register a JsonTypeInfo first.");

        if (actualType == typeof(String)) return jsonValue.TryGetValue<String>(out var stringValue) ? stringValue : node.ToJsonString();
        if (actualType == typeof(Boolean)) return jsonValue.GetValue<Boolean>();
        if (actualType == typeof(Byte)) return Byte.Parse(GetJsonScalarText(node), CultureInfo.InvariantCulture);
        if (actualType == typeof(SByte)) return SByte.Parse(GetJsonScalarText(node), CultureInfo.InvariantCulture);
        if (actualType == typeof(Int16)) return Int16.Parse(GetJsonScalarText(node), CultureInfo.InvariantCulture);
        if (actualType == typeof(UInt16)) return UInt16.Parse(GetJsonScalarText(node), CultureInfo.InvariantCulture);
        if (actualType == typeof(Int32)) return Int32.Parse(GetJsonScalarText(node), CultureInfo.InvariantCulture);
        if (actualType == typeof(UInt32)) return UInt32.Parse(GetJsonScalarText(node), CultureInfo.InvariantCulture);
        if (actualType == typeof(Int64)) return Int64.Parse(GetJsonScalarText(node), CultureInfo.InvariantCulture);
        if (actualType == typeof(UInt64)) return UInt64.Parse(GetJsonScalarText(node), CultureInfo.InvariantCulture);
        if (actualType == typeof(Single)) return Single.Parse(GetJsonScalarText(node), CultureInfo.InvariantCulture);
        if (actualType == typeof(Double)) return Double.Parse(GetJsonScalarText(node), CultureInfo.InvariantCulture);
        if (actualType == typeof(Decimal)) return Decimal.Parse(GetJsonScalarText(node), CultureInfo.InvariantCulture);
        if (actualType == typeof(DateTime)) return DateTime.Parse(GetJsonScalarText(node), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        if (actualType == typeof(DateTimeOffset)) return DateTimeOffset.Parse(GetJsonScalarText(node), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        if (actualType == typeof(TimeSpan)) return TimeSpan.Parse(GetJsonScalarText(node), CultureInfo.InvariantCulture);
        if (actualType == typeof(Guid)) return Guid.Parse(GetJsonScalarText(node));
        if (actualType == typeof(Byte[])) return System.Convert.FromBase64String(GetJsonScalarText(node));
        if (actualType.IsEnum)
        {
            if (jsonValue.TryGetValue<String>(out var enumText) && !String.IsNullOrWhiteSpace(enumText))
                return Enum.Parse(actualType, enumText, true);

            return Enum.ToObject(actualType, Int64.Parse(GetJsonScalarText(node), CultureInfo.InvariantCulture));
        }

        throw new NotSupportedException($"Type {actualType.FullName} is not registered for AOT-safe JSON deserialization. Register a JsonTypeInfo first.");
    }

    private static Object? ChangeScalarValue(String value, Type type)
    {
        if (type == typeof(String)) return value;
        if (type == typeof(Boolean)) return Boolean.Parse(value);
        if (type == typeof(Byte)) return Byte.Parse(value, CultureInfo.InvariantCulture);
        if (type == typeof(SByte)) return SByte.Parse(value, CultureInfo.InvariantCulture);
        if (type == typeof(Int16)) return Int16.Parse(value, CultureInfo.InvariantCulture);
        if (type == typeof(UInt16)) return UInt16.Parse(value, CultureInfo.InvariantCulture);
        if (type == typeof(Int32)) return Int32.Parse(value, CultureInfo.InvariantCulture);
        if (type == typeof(UInt32)) return UInt32.Parse(value, CultureInfo.InvariantCulture);
        if (type == typeof(Int64)) return Int64.Parse(value, CultureInfo.InvariantCulture);
        if (type == typeof(UInt64)) return UInt64.Parse(value, CultureInfo.InvariantCulture);
        if (type == typeof(Single)) return Single.Parse(value, CultureInfo.InvariantCulture);
        if (type == typeof(Double)) return Double.Parse(value, CultureInfo.InvariantCulture);
        if (type == typeof(Decimal)) return Decimal.Parse(value, CultureInfo.InvariantCulture);
        if (type == typeof(DateTime)) return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        if (type == typeof(DateTimeOffset)) return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        if (type == typeof(TimeSpan)) return TimeSpan.Parse(value, CultureInfo.InvariantCulture);
        if (type == typeof(Guid)) return Guid.Parse(value);
        if (type.IsEnum) return Enum.Parse(type, value, true);

        throw new NotSupportedException($"Type {type.FullName} is not supported by scalar conversion.");
    }

    private static JsonSerializerOptions CreateSerializerOptions(JsonOptions jsonOptions)
    {
        var options = new JsonSerializerOptions()
        {
            WriteIndented = jsonOptions.WriteIndented,
        };

        if (jsonOptions.IgnoreNullValues)
            options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;

        return options;
    }

    private static String SerializeString(String value) => $"\"{JsonEncodedText.Encode(value).ToString()}\"";

    private static String FormatDateTime(DateTime value, JsonOptions jsonOptions)
        => jsonOptions.FullTime ? value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : value.ToString("O", CultureInfo.InvariantCulture);

    private static String GetJsonScalarText(JsonNode node)
    {
        if (node is JsonValue jsonValue && jsonValue.TryGetValue<String>(out var stringValue)) return stringValue;
        if (node is JsonValue booleanValue && booleanValue.TryGetValue<Boolean>(out var boolValue)) return boolValue ? "true" : "false";
        if (node is JsonValue int32Value && int32Value.TryGetValue<Int32>(out var number32)) return number32.ToString(CultureInfo.InvariantCulture);
        if (node is JsonValue int64Value && int64Value.TryGetValue<Int64>(out var number64)) return number64.ToString(CultureInfo.InvariantCulture);
        if (node is JsonValue decimalValue && decimalValue.TryGetValue<Decimal>(out var numberDecimal)) return numberDecimal.ToString(CultureInfo.InvariantCulture);
        if (node is JsonValue doubleValue && doubleValue.TryGetValue<Double>(out var numberDouble)) return numberDouble.ToString("R", CultureInfo.InvariantCulture);

        return node.ToJsonString();
    }

    private static Object? ConvertNode(JsonNode? node)
    {
        if (node == null) return null;

        if (node is JsonObject jsonObject)
        {
            var dictionary = new Dictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in jsonObject)
            {
                dictionary[item.Key] = ConvertNode(item.Value);
            }

            return dictionary;
        }

        if (node is JsonArray jsonArray)
        {
            var list = new List<Object?>(jsonArray.Count);
            foreach (var item in jsonArray)
            {
                list.Add(ConvertNode(item));
            }

            return list;
        }

        if (node is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<String>(out var stringValue)) return stringValue;
            if (jsonValue.TryGetValue<Boolean>(out var booleanValue)) return booleanValue;
            if (jsonValue.TryGetValue<Int32>(out var int32Value)) return int32Value;
            if (jsonValue.TryGetValue<Int64>(out var int64Value)) return int64Value;
            if (jsonValue.TryGetValue<Double>(out var doubleValue)) return doubleValue;
            if (jsonValue.TryGetValue<Decimal>(out var decimalValue)) return decimalValue;
        }

        return node.ToJsonString();
    }

    private sealed class NullServiceProvider : IServiceProvider
    {
        public static readonly NullServiceProvider Instance = new();

        public Object? GetService(Type serviceType) => null;
    }
}