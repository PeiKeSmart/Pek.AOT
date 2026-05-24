using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Pek.Serialization;

/// <summary>序列化助手</summary>
public static class SerialHelper
{
    private static readonly ConcurrentDictionary<PropertyInfo, String> _names = new();

    /// <summary>获取序列化名称</summary>
    /// <param name="property">属性</param>
    /// <returns>序列化名称</returns>
    public static String GetName(PropertyInfo property)
    {
        if (property == null) throw new ArgumentNullException(nameof(property));
        if (_names.TryGetValue(property, out var name)) return name;

        name = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name;
        if (String.IsNullOrEmpty(name)) name = property.GetCustomAttribute<DataMemberAttribute>()?.Name;
        if (String.IsNullOrEmpty(name)) name = property.GetCustomAttribute<XmlElementAttribute>()?.ElementName;
        if (String.IsNullOrEmpty(name)) name = property.Name;

        _names[property] = name!;
        return name!;
    }
}