using System.Reflection;
using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace Pek.Serialization;

/// <summary>数据成员修饰器。让现有 JsonTypeInfo 支持 DataMember 与 IgnoreDataMember。</summary>
public static class DataMemberResolver
{
    /// <summary>按 DataMember/IgnoreDataMember 约定调整类型信息</summary>
    /// <param name="typeInfo">类型信息</param>
    public static void Modifier(System.Text.Json.Serialization.Metadata.JsonTypeInfo typeInfo)
    {
        if (typeInfo == null) throw new ArgumentNullException(nameof(typeInfo));
        if (typeInfo.Kind != System.Text.Json.Serialization.Metadata.JsonTypeInfoKind.Object || typeInfo.Type.IsArray) return;

        foreach (var propertyInfo in typeInfo.Properties)
        {
            var provider = propertyInfo.AttributeProvider;
            if (provider == null) continue;

            if (provider.IsDefined(typeof(IgnoreDataMemberAttribute), true) || provider.IsDefined(typeof(XmlIgnoreAttribute), false))
            {
                propertyInfo.Get = null;
                propertyInfo.Set = null;
                continue;
            }

            if (provider is PropertyInfo property)
            {
                var name = SerialHelper.GetName(property);
                if (!String.IsNullOrEmpty(name) && name != property.Name) propertyInfo.Name = name;
            }
        }
    }
}