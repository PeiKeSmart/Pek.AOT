using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Xml;
using System.Xml.Linq;

namespace Pek.Xml;

/// <summary>Xml 辅助类</summary>
public static class XmlHelper
{
    /// <summary>序列化为 Xml 字符串</summary>
    /// <param name="obj">目标对象</param>
    /// <param name="type">对象类型</param>
    /// <param name="options">Json 序列化选项</param>
    /// <param name="encoding">编码</param>
    /// <param name="rootName">根节点名称</param>
    /// <param name="omitXmlDeclaration">是否忽略 XML 声明</param>
    /// <param name="attachComment">是否写入 Description/DisplayName 注释</param>
    /// <returns>Xml 字符串</returns>
    public static String ToXml(this Object obj, Type type, JsonSerializerOptions options, Encoding? encoding = null, String? rootName = null, Boolean omitXmlDeclaration = false, Boolean attachComment = true)
    {
        if (obj == null) return String.Empty;
        if (type == null) throw new ArgumentNullException(nameof(type));
        if (options == null) throw new ArgumentNullException(nameof(options));

        encoding ??= new UTF8Encoding(false);
        var typeInfo = GetTypeInfo(type, options);
        var node = JsonSerializer.SerializeToNode(obj, typeInfo);
        var elementName = String.IsNullOrWhiteSpace(rootName) ? type.Name : rootName;
        var root = BuildXmlElement(elementName, node, typeInfo, options, attachComment, type);
        var typeComment = attachComment ? SanitizeComment(GetComment(type)) : null;
        var document = String.IsNullOrWhiteSpace(typeComment)
            ? new XDocument(new XDeclaration("1.0", "utf-8", null), root)
            : new XDocument(new XDeclaration("1.0", "utf-8", null), new XComment(typeComment), root);

        using var stream = new MemoryStream();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Encoding = encoding,
            Indent = true,
            OmitXmlDeclaration = omitXmlDeclaration
        });
        document.Save(writer);
        writer.Flush();
        return encoding.GetString(stream.ToArray());
    }

    /// <summary>序列化为 Xml 文件</summary>
    /// <param name="obj">目标对象</param>
    /// <param name="type">对象类型</param>
    /// <param name="options">Json 序列化选项</param>
    /// <param name="file">目标文件</param>
    /// <param name="encoding">编码</param>
    /// <param name="rootName">根节点名称</param>
    /// <param name="attachComment">是否写入 Description/DisplayName 注释</param>
    public static void ToXmlFile(this Object obj, Type type, JsonSerializerOptions options, String file, Encoding? encoding = null, String? rootName = null, Boolean attachComment = true)
    {
        if (String.IsNullOrWhiteSpace(file)) throw new ArgumentNullException(nameof(file));

        file.EnsureDirectory(true);
        var xml = obj.ToXml(type, options, encoding, rootName, false, attachComment);
        File.WriteAllText(file, xml, encoding ?? new UTF8Encoding(false));
    }

    /// <summary>Xml 字符串转对象</summary>
    /// <param name="xml">Xml 字符串</param>
    /// <param name="type">目标类型</param>
    /// <param name="options">Json 序列化选项</param>
    /// <returns>对象实例</returns>
    public static Object? ToXmlEntity(this String xml, Type type, JsonSerializerOptions options)
    {
        if (String.IsNullOrWhiteSpace(xml)) throw new ArgumentNullException(nameof(xml));
        if (type == null) throw new ArgumentNullException(nameof(type));
        if (options == null) throw new ArgumentNullException(nameof(options));

        var doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        if (doc.Root == null) return null;

        var node = BuildJsonNode(doc.Root, type, options);
        var typeInfo = GetTypeInfo(type, options);
        return JsonSerializer.Deserialize(node.ToJsonString(), typeInfo);
    }

    /// <summary>Xml 流转对象</summary>
    /// <param name="stream">数据流</param>
    /// <param name="type">目标类型</param>
    /// <param name="options">Json 序列化选项</param>
    /// <param name="encoding">编码</param>
    /// <returns>对象实例</returns>
    public static Object? ToXmlEntity(this Stream stream, Type type, JsonSerializerOptions options, Encoding? encoding = null)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        using var reader = new StreamReader(stream, encoding ?? Encoding.UTF8, true, leaveOpen: true);
        return reader.ReadToEnd().ToXmlEntity(type, options);
    }

    /// <summary>简单 Xml 转字典</summary>
    /// <param name="xml">Xml 字符串</param>
    /// <returns>字典</returns>
    public static Dictionary<String, String>? ToXmlDictionary(this String xml)
    {
        if (String.IsNullOrEmpty(xml)) return null;

        var doc = XDocument.Parse(xml);
        var root = doc.Root;
        if (root == null) return null;

        var dic = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in root.Elements())
        {
            dic[node.Name.LocalName] = node.HasElements ? node.ToString(SaveOptions.DisableFormatting) : node.Value;
        }

        return dic;
    }

    /// <summary>字典转 Xml</summary>
    /// <param name="dic">字典</param>
    /// <param name="rootName">根节点名称</param>
    /// <returns>Xml 字符串</returns>
    public static String ToXml(this IDictionary<String, String> dic, String? rootName = null)
    {
        rootName = String.IsNullOrWhiteSpace(rootName) ? "xml" : rootName;
        var root = new XElement(rootName);
        if (dic != null)
        {
            foreach (var item in dic)
            {
                root.Add(new XElement(item.Key, item.Value));
            }
        }

        return new XDocument(root).ToString(SaveOptions.DisableFormatting);
    }

    private static JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        var typeInfo = options.GetTypeInfo(type);
        if (typeInfo == null) throw new InvalidOperationException($"类型 {type.FullName} 未注册 JsonTypeInfo。");
        return typeInfo;
    }

    private static XElement BuildXmlElement(String name, JsonNode? node, JsonTypeInfo? typeInfo, JsonSerializerOptions options, Boolean attachComment, Type? declaredType)
    {
        var element = new XElement(name);
        if (node == null) return element;

        if (node is JsonObject obj)
        {
            if (typeInfo?.Kind == JsonTypeInfoKind.Object)
            {
                foreach (var property in typeInfo.Properties)
                {
                    if (!obj.TryGetPropertyValue(property.Name, out var propertyNode)) continue;

                    var propertyComment = attachComment ? SanitizeComment(GetComment(property.AttributeProvider)) : null;
                    if (!String.IsNullOrWhiteSpace(propertyComment)) element.Add(new XComment(propertyComment));

                    var childTypeInfo = TryGetTypeInfo(property.PropertyType, options);
                    element.Add(BuildXmlElement(GetXmlPropertyName(property), propertyNode, childTypeInfo, options, attachComment, property.PropertyType));
                }

                return element;
            }

            foreach (var property in obj)
            {
                element.Add(BuildXmlElement(property.Key, property.Value, null, options, attachComment, null));
            }

            return element;
        }

        if (node is JsonArray array)
        {
            var itemType = TryGetEnumerableItemType(declaredType);
            foreach (var item in array)
            {
                element.Add(BuildXmlElement("Item", item, TryGetTypeInfo(itemType ?? typeof(Object), options), options, attachComment, itemType));
            }

            return element;
        }

        if (node is JsonValue value)
        {
            element.Value = GetScalarText(value, declaredType);
            return element;
        }

        element.Value = node.ToJsonString();
        return element;
    }

    private static JsonNode BuildJsonNode(XElement element, Type type, JsonSerializerOptions options)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (TryBuildScalarNode(element.Value, type, out var scalarNode)) return scalarNode;

        if (IsDictionaryType(type, out var valueType))
        {
            var obj = new JsonObject();
            foreach (var child in element.Elements())
            {
                obj[child.Name.LocalName] = BuildJsonNode(child, valueType, options);
            }

            return obj;
        }

        if (IsEnumerableType(type, out var itemType))
        {
            var array = new JsonArray();
            foreach (var child in element.Elements())
            {
                array.Add(BuildJsonNode(child, itemType, options));
            }

            return array;
        }

        var typeInfo = GetTypeInfo(type, options);
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return JsonValue.Create(element.Value) ?? JsonValue.Create(String.Empty)!;
        }

        var result = new JsonObject();
        foreach (var property in typeInfo.Properties)
        {
            var child = element.Element(GetXmlPropertyName(property)) ?? element.Element(property.Name);
            if (child == null) continue;

            result[property.Name] = BuildJsonNode(child, property.PropertyType, options);
        }

        return result;
    }

    private static Boolean TryBuildScalarNode(String text, Type type, out JsonNode node)
    {
        if (type == typeof(String))
        {
            node = JsonValue.Create(text)!;
            return true;
        }

        if (type.IsEnum)
        {
            if (Int64.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var enumNumber))
                node = JsonValue.Create(enumNumber)!;
            else if (Enum.TryParse(type, text, true, out var enumValue) && enumValue != null)
            {
                var underlyingType = Enum.GetUnderlyingType(type);
                node = underlyingType == typeof(Byte) ? JsonValue.Create((Byte)enumValue)! :
                    underlyingType == typeof(SByte) ? JsonValue.Create((SByte)enumValue)! :
                    underlyingType == typeof(Int16) ? JsonValue.Create((Int16)enumValue)! :
                    underlyingType == typeof(UInt16) ? JsonValue.Create((UInt16)enumValue)! :
                    underlyingType == typeof(Int32) ? JsonValue.Create((Int32)enumValue)! :
                    underlyingType == typeof(UInt32) ? JsonValue.Create((UInt32)enumValue)! :
                    underlyingType == typeof(Int64) ? JsonValue.Create((Int64)enumValue)! :
                    JsonValue.Create((UInt64)enumValue)!;
            }
            else
                node = JsonValue.Create(text)!;

            return true;
        }

        if (type == typeof(Boolean)) { node = JsonValue.Create(XmlConvert.ToBoolean(text))!; return true; }
        if (type == typeof(Byte)) { node = JsonValue.Create(XmlConvert.ToByte(text))!; return true; }
        if (type == typeof(SByte)) { node = JsonValue.Create(XmlConvert.ToSByte(text))!; return true; }
        if (type == typeof(Int16)) { node = JsonValue.Create(XmlConvert.ToInt16(text))!; return true; }
        if (type == typeof(UInt16)) { node = JsonValue.Create(XmlConvert.ToUInt16(text))!; return true; }
        if (type == typeof(Int32)) { node = JsonValue.Create(XmlConvert.ToInt32(text))!; return true; }
        if (type == typeof(UInt32)) { node = JsonValue.Create(XmlConvert.ToUInt32(text))!; return true; }
        if (type == typeof(Int64)) { node = JsonValue.Create(XmlConvert.ToInt64(text))!; return true; }
        if (type == typeof(UInt64)) { node = JsonValue.Create(XmlConvert.ToUInt64(text))!; return true; }
        if (type == typeof(Single)) { node = JsonValue.Create(XmlConvert.ToSingle(text))!; return true; }
        if (type == typeof(Double)) { node = JsonValue.Create(XmlConvert.ToDouble(text))!; return true; }
        if (type == typeof(Decimal)) { node = JsonValue.Create(XmlConvert.ToDecimal(text))!; return true; }
        if (type == typeof(DateTime)) { node = JsonValue.Create(XmlConvert.ToString(XmlConvert.ToDateTime(text, XmlDateTimeSerializationMode.RoundtripKind), XmlDateTimeSerializationMode.RoundtripKind))!; return true; }
        if (type == typeof(DateTimeOffset)) { node = JsonValue.Create(XmlConvert.ToString(XmlConvert.ToDateTimeOffset(text)))!; return true; }
        if (type == typeof(TimeSpan)) { node = JsonValue.Create(XmlConvert.ToString(XmlConvert.ToTimeSpan(text)))!; return true; }
        if (type == typeof(Guid)) { node = JsonValue.Create(Guid.Parse(text).ToString())!; return true; }

        node = null!;
        return false;
    }

    private static String GetScalarText(JsonValue value, Type? declaredType)
    {
        var effectiveType = Nullable.GetUnderlyingType(declaredType ?? typeof(Object)) ?? declaredType;
        if (effectiveType?.IsEnum == true)
        {
            var raw = value.ToJsonString().Trim('"');
            if (String.IsNullOrWhiteSpace(raw)) return raw;

            if (Int64.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var enumNumber))
                return Enum.GetName(effectiveType, Enum.ToObject(effectiveType, enumNumber)) ?? raw;

            return raw;
        }

        if (value.TryGetValue<Boolean>(out var booleanValue)) return XmlConvert.ToString(booleanValue);
        if (value.TryGetValue<Byte>(out var byteValue)) return XmlConvert.ToString(byteValue);
        if (value.TryGetValue<SByte>(out var sbyteValue)) return XmlConvert.ToString(sbyteValue);
        if (value.TryGetValue<Int16>(out var int16Value)) return XmlConvert.ToString(int16Value);
        if (value.TryGetValue<UInt16>(out var uint16Value)) return XmlConvert.ToString(uint16Value);
        if (value.TryGetValue<Int32>(out var int32Value)) return XmlConvert.ToString(int32Value);
        if (value.TryGetValue<UInt32>(out var uint32Value)) return XmlConvert.ToString(uint32Value);
        if (value.TryGetValue<Int64>(out var int64Value)) return XmlConvert.ToString(int64Value);
        if (value.TryGetValue<UInt64>(out var uint64Value)) return XmlConvert.ToString(uint64Value);
        if (value.TryGetValue<Single>(out var singleValue)) return XmlConvert.ToString(singleValue);
        if (value.TryGetValue<Double>(out var doubleValue)) return XmlConvert.ToString(doubleValue);
        if (value.TryGetValue<Decimal>(out var decimalValue)) return XmlConvert.ToString(decimalValue);
        if (value.TryGetValue<DateTime>(out var dateTimeValue)) return XmlConvert.ToString(dateTimeValue, XmlDateTimeSerializationMode.RoundtripKind);
        if (value.TryGetValue<DateTimeOffset>(out var dateTimeOffsetValue)) return XmlConvert.ToString(dateTimeOffsetValue);
        if (value.TryGetValue<TimeSpan>(out var timeSpanValue)) return XmlConvert.ToString(timeSpanValue);
        if (value.TryGetValue<Guid>(out var guidValue)) return guidValue.ToString();
        if (value.TryGetValue<String>(out var stringValue)) return stringValue ?? String.Empty;

        return value.ToJsonString().Trim('"');
    }

    private static String GetXmlPropertyName(JsonPropertyInfo property)
    {
        if (property.AttributeProvider is MemberInfo member) return member.Name;
        return property.Name;
    }

    private static Type? TryGetEnumerableItemType(Type? type)
    {
        if (type == null) return null;
        return IsEnumerableType(type, out var itemType) ? itemType : null;
    }

    private static Boolean IsEnumerableType(Type type, out Type itemType)
    {
        if (type == typeof(String))
        {
            itemType = typeof(String);
            return false;
        }

        if (type.IsArray)
        {
            itemType = type.GetElementType() ?? typeof(Object);
            return true;
        }

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            if (definition == typeof(List<>) || definition == typeof(IList<>) || definition == typeof(IEnumerable<>) || definition == typeof(ICollection<>))
            {
                itemType = type.GenericTypeArguments[0];
                return true;
            }
        }

        itemType = typeof(Object);
        return false;
    }

    private static Boolean IsDictionaryType(Type type, out Type valueType)
    {
        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            if ((definition == typeof(Dictionary<,>) || definition == typeof(IDictionary<,>)) && type.GenericTypeArguments[0] == typeof(String))
            {
                valueType = type.GenericTypeArguments[1];
                return true;
            }
        }

        valueType = typeof(Object);
        return false;
    }

    private static JsonTypeInfo? TryGetTypeInfo(Type type, JsonSerializerOptions options)
    {
        try
        {
            return options.GetTypeInfo(type);
        }
        catch
        {
            return null;
        }
    }

    private static String? GetComment(MemberInfo member)
    {
        var description = member.GetCustomAttribute<DescriptionAttribute>()?.Description;
        if (!String.IsNullOrWhiteSpace(description)) return description;

        var displayName = member.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName;
        return String.IsNullOrWhiteSpace(displayName) ? null : displayName;
    }

    private static String? GetComment(ICustomAttributeProvider? attributeProvider)
    {
        if (attributeProvider == null) return null;

        if (attributeProvider is MemberInfo member) return GetComment(member);

        var descriptions = attributeProvider.GetCustomAttributes(typeof(DescriptionAttribute), true);
        if (descriptions.Length > 0 && descriptions[0] is DescriptionAttribute description && !String.IsNullOrWhiteSpace(description.Description))
            return description.Description;

        var displayNames = attributeProvider.GetCustomAttributes(typeof(DisplayNameAttribute), true);
        if (displayNames.Length > 0 && displayNames[0] is DisplayNameAttribute displayName && !String.IsNullOrWhiteSpace(displayName.DisplayName))
            return displayName.DisplayName;

        return null;
    }

    private static String? SanitizeComment(String? comment)
    {
        if (String.IsNullOrWhiteSpace(comment)) return comment;

        comment = comment.Replace("--", "- -", StringComparison.Ordinal);
        if (comment.EndsWith("-", StringComparison.Ordinal)) comment += " ";

        return comment;
    }
}