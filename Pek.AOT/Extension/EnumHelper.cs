using System.ComponentModel;
using System.Reflection;
using System.Collections.Concurrent;

using Pek.Extension;
using System.Diagnostics.CodeAnalysis;

namespace NewLife;

/// <summary>枚举类型助手类</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class EnumHelper
{
    private static readonly ConcurrentDictionary<Type, Dictionary<String, String?>> _nameDescriptionCache = [];
    private static readonly ConcurrentDictionary<Type, Dictionary<Int32, String>> _valueDescriptionCache = [];

    /// <summary>枚举变量是否包含指定标识</summary>
    /// <param name="value">枚举变量</param>
    /// <param name="flag">要判断的标识</param>
    /// <returns>如果枚举变量包含指定标识则返回 true，否则返回 false</returns>
    /// <exception cref="ArgumentException">当两个枚举类型不匹配时</exception>
    public static Boolean Has(this Enum value, Enum flag)
    {
        if (value.GetType() != flag.GetType()) throw new ArgumentException("Enumeration identification judgment must be of the same type", nameof(flag));

        var num = Convert.ToUInt64(flag);
        if (num == 0) return Convert.ToUInt64(value) == 0;

        return (Convert.ToUInt64(value) & num) == num;
    }

    /// <summary>设置枚举标识位</summary>
    /// <typeparam name="T">枚举类型</typeparam>
    /// <param name="source">源枚举值</param>
    /// <param name="flag">要设置的标识</param>
    /// <param name="value">是否设置该标识</param>
    /// <returns>设置后的枚举值</returns>
    /// <exception cref="ArgumentException">当枚举类型不匹配时</exception>
    public static T Set<T>(this Enum source, T flag, Boolean value)
    {
        if (source is not T) throw new ArgumentException("Enumeration identification judgment must be of the same type", nameof(source));

        var s = Convert.ToUInt64(source);
        var f = Convert.ToUInt64(flag);

        if (value)
            s |= f;
        else
            s &= ~f;

        return (T)Enum.ToObject(typeof(T), s);
    }

    /// <summary>获取枚举字段的描述</summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="value">枚举值</param>
    /// <returns>如果存在 DescriptionAttribute 则返回其描述，否则返回 null</returns>
    public static String? GetDescription<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum>(this TEnum value)
        where TEnum : struct, Enum
        => GetDescriptionCore(typeof(TEnum), value.ToString());

    /// <summary>获取枚举字段的描述</summary>
    /// <param name="value">枚举值</param>
    /// <returns>如果存在 DescriptionAttribute 则返回其描述，否则返回 null</returns>
    public static String? GetDescription(this Enum value)
    {
        if (value == null) return null;

        return GetDescriptionCore(value.GetType(), value.ToString());
    }

    /// <summary>获取枚举类型的所有字段描述</summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <returns>包含枚举值与其描述的字典</returns>
    public static Dictionary<TEnum, String> GetDescriptions<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum>() where TEnum : notnull
    {
        var result = new Dictionary<TEnum, String>();
        var descriptions = GetDescriptions(typeof(TEnum));

        foreach (var kvp in descriptions)
        {
            result.Add((TEnum)Enum.ToObject(typeof(TEnum), kvp.Key), kvp.Value);
        }

        return result;
    }

    /// <summary>获取枚举类型的所有字段描述</summary>
    /// <param name="enumType">枚举类型</param>
    /// <returns>包含枚举值（Int32）与其描述的字典</returns>
    public static Dictionary<Int32, String> GetDescriptions([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] Type enumType)
    {
        if (!enumType.IsEnum) return [];

        if (!_valueDescriptionCache.TryGetValue(enumType, out var cache))
        {
            cache = BuildValueDescriptionCache(enumType);
            _valueDescriptionCache[enumType] = cache;
        }

        return new Dictionary<Int32, String>(cache);
    }

    private static String? GetDescriptionCore([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] Type enumType, String name)
    {
        if (!enumType.IsEnum || name.IsNullOrEmpty()) return null;

        if (!_nameDescriptionCache.TryGetValue(enumType, out var descriptions))
        {
            descriptions = BuildNameDescriptionCache(enumType);
            _nameDescriptionCache[enumType] = descriptions;
        }

        return descriptions.TryGetValue(name, out var description) ? description : null;
    }

    private static Dictionary<String, String?> BuildNameDescriptionCache([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] Type enumType)
    {
        var result = new Dictionary<String, String?>(StringComparer.Ordinal);
        foreach (var field in enumType.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (!field.IsStatic) continue;

            var displayName = field.GetCustomAttribute<DisplayNameAttribute>(false);
            var description = displayName?.DisplayName;

            var descriptionAttr = field.GetCustomAttribute<DescriptionAttribute>(false);
            if (description.IsNullOrEmpty()) description = descriptionAttr?.Description;

            result[field.Name] = description;
        }

        return result;
    }

    private static Dictionary<Int32, String> BuildValueDescriptionCache([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] Type enumType)
    {
        var result = new Dictionary<Int32, String>();
        foreach (var field in enumType.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (!field.IsStatic) continue;

            var enumValue = Convert.ToInt32(field.GetValue(null));

            var displayName = field.GetCustomAttribute<DisplayNameAttribute>(false);
            var description = displayName?.DisplayName;

            var descriptionAttr = field.GetCustomAttribute<DescriptionAttribute>(false);
            if (description.IsNullOrEmpty()) description = descriptionAttr?.Description;

            if (description.IsNullOrEmpty()) description = field.Name;
            result[enumValue] = description ?? field.Name;
        }

        return result;
    }
}