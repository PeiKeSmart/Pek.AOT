using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;

namespace NewLife;

/// <summary>特性辅助类</summary>
public static class AttributeX
{
    private static readonly ConcurrentDictionary<String, Object> _asmCache = [];

    /// <summary>获取自定义属性，带缓存功能</summary>
    /// <typeparam name="TAttribute">特性类型</typeparam>
    /// <param name="assembly">程序集</param>
    /// <returns>特性数组</returns>
    public static TAttribute[] GetCustomAttributes<TAttribute>(this Assembly assembly)
    {
        if (assembly == null) return [];

        var key = $"{assembly.FullName}_{typeof(TAttribute).FullName}";
        return (TAttribute[])_asmCache.GetOrAdd(key, _ => assembly.GetCustomAttributes<TAttribute>().ToArray());
    }

    /// <summary>获取成员绑定的显示名</summary>
    /// <param name="member">成员</param>
    /// <param name="inherit">是否递归</param>
    /// <returns>显示名</returns>
    public static String? GetDisplayName(this MemberInfo member, Boolean inherit = true)
    {
        if (member == null) return null;

        var attribute = member.GetCustomAttribute<DisplayNameAttribute>(inherit);
        return attribute != null && !String.IsNullOrWhiteSpace(attribute.DisplayName) ? attribute.DisplayName : null;
    }

    /// <summary>获取成员绑定的备注</summary>
    /// <param name="member">成员</param>
    /// <param name="inherit">是否递归</param>
    /// <returns>备注</returns>
    public static String? GetDescription(this MemberInfo member, Boolean inherit = true)
    {
        if (member == null) return null;

        var attribute = member.GetCustomAttribute<DescriptionAttribute>(inherit);
        return attribute != null && !String.IsNullOrWhiteSpace(attribute.Description) ? attribute.Description : null;
    }

    /// <summary>获取程序集自定义特性的值</summary>
    /// <typeparam name="TAttribute">特性类型</typeparam>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <param name="target">目标程序集</param>
    /// <returns>特性值</returns>
    public static TResult? GetCustomAttributeValue<TAttribute, TResult>(this Assembly target) where TAttribute : Attribute
    {
        if (target == null) return default;

        try
        {
            var attributes = CustomAttributeData.GetCustomAttributes(target);
            foreach (var attribute in attributes)
            {
                if (attribute.AttributeType != typeof(TAttribute)) continue;

                if (TryGetAttributeValue<TResult>(attribute, out var value)) return value;
            }
        }
        catch { }

        return default;
    }

    /// <summary>获取成员自定义特性的值</summary>
    /// <typeparam name="TAttribute">特性类型</typeparam>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <param name="target">目标成员</param>
    /// <param name="inherit">是否递归</param>
    /// <returns>特性值</returns>
    public static TResult? GetCustomAttributeValue<TAttribute, TResult>(this MemberInfo target, Boolean inherit = true) where TAttribute : Attribute
    {
        if (target == null) return default;

        try
        {
            var attributes = CustomAttributeData.GetCustomAttributes(target);
            foreach (var attribute in attributes)
            {
                if (attribute.AttributeType != typeof(TAttribute)) continue;

                if (TryGetAttributeValue<TResult>(attribute, out var value)) return value;
            }

            if (inherit && target is Type type && type.BaseType != null && type.BaseType != typeof(Object))
                return GetCustomAttributeValue<TAttribute, TResult>(type.BaseType, true);
        }
        catch { }

        return default;
    }

    private static Boolean TryGetAttributeValue<TResult>(CustomAttributeData attribute, out TResult? value)
    {
        foreach (var argument in attribute.ConstructorArguments)
        {
            if (TryConvertArgument(argument.Value, out value)) return true;
        }

        foreach (var argument in attribute.NamedArguments)
        {
            if (TryConvertArgument(argument.TypedValue.Value, out value)) return true;
        }

        value = default;
        return false;
    }

    private static Boolean TryConvertArgument<TResult>(Object? source, out TResult? value)
    {
        if (source is TResult result)
        {
            value = result;
            return true;
        }

        if (source == null)
        {
            value = default;
            return false;
        }

        try
        {
            var targetType = Nullable.GetUnderlyingType(typeof(TResult)) ?? typeof(TResult);
            if (targetType.IsEnum)
            {
                if (source is String text)
                {
                    value = (TResult)Enum.Parse(targetType, text, true);
                    return true;
                }

                value = (TResult)Enum.ToObject(targetType, Convert.ChangeType(source, Enum.GetUnderlyingType(targetType))!);
                return true;
            }

            value = (TResult?)Convert.ChangeType(source, targetType);
            return value != null || Nullable.GetUnderlyingType(typeof(TResult)) != null || typeof(TResult) == typeof(String);
        }
        catch
        {
            value = default;
            return false;
        }
    }
}