using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Pek;

public static class ConvertibleExtensions
{
    /// <summary>
    /// 类型直转
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="value"></param>
    /// <returns></returns>
    public static T? ConvertTo<T>(this IConvertible value) where T : IConvertible
    {
        return (T?)ConvertTo(value, typeof(T));
    }

    /// <summary>
    /// 类型直转
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="value"></param>
    /// <param name="defaultValue">转换失败的默认值</param>
    /// <returns></returns>
    public static T? TryConvertTo<T>(this IConvertible value, T? defaultValue = default) where T : IConvertible
    {
        try
        {
            return (T?)ConvertTo(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// 类型直转
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="value"></param>
    /// <param name="result">转换失败的默认值</param>
    /// <returns></returns>
    public static Boolean TryConvertTo<T>(this IConvertible value, [MaybeNullWhen(false)] out T result) where T : IConvertible
    {
        result = default;
        try
        {
            if (ConvertTo(value, typeof(T)) is T t)
            {
                result = t;
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    /// <summary>
    /// 类型直转
    /// </summary>
    /// <param name="value"></param>
    /// <param name="type">目标类型</param>
    /// <param name="result">转换失败的默认值</param>
    /// <returns></returns>
    public static Boolean TryConvertTo(this IConvertible value, Type type, [MaybeNullWhen(false)] out Object result)
    {
        result = default;
        try
        {
            var obj = ConvertTo(value, type);
            if (obj != null)
            {
                result = obj;
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    /// <summary>
    /// 类型直转
    /// </summary>
    /// <param name="value"></param>
    /// <param name="type">目标类型</param>
    /// <returns></returns>
    public static Object? ConvertTo(this IConvertible value, Type type)
    {
        if (null == value)
        {
            return default;
        }

        if (type.IsEnum)
        {
            return Enum.Parse(type, value.ToString(CultureInfo.InvariantCulture));
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            var underlyingType = Nullable.GetUnderlyingType(type);
            return underlyingType!.IsEnum ? Enum.Parse(underlyingType, value.ToString(CultureInfo.CurrentCulture)) : Convert.ChangeType(value, underlyingType);
        }

        return Convert.ChangeType(value, type);
    }
}
