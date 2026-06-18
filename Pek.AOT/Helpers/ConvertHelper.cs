using System.Globalization;

namespace Pek.Helpers;

/// <summary>类型转换帮助类。AOT 安全版：仅支持已知基础类型的显式转换，不使用 TypeConverter</summary>
public class ConvertHelper
{
    /// <summary>将字符串转换为指定类型</summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="value"></param>
    /// <returns></returns>
    public static T? ToType<T>(String value)
    {
        Object? obj = default(T);
        T? result;
        if (String.IsNullOrEmpty(value))
        {
            result = (T?)obj;
        }
        else
        {
            obj = ToType(value, typeof(T));
            result = (T?)obj;
        }
        return result;
    }

    /// <summary>将字符串转换为指定类型，转换失败时返回默认值</summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="value"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    public static T? ToType<T>(String value, T defaultValue)
    {
        if (String.IsNullOrEmpty(value)) return defaultValue;

        try
        {
            return ToType<T>(value);
        }
        catch (Exception)
        {
            return defaultValue;
        }
    }

    private static Object? ToType(String value, Type conversionType)
    {
        if (conversionType == typeof(String)) return value;
        if (conversionType == typeof(Int32)) return value == null ? 0 : Int32.Parse(value, NumberStyles.Any);
        if (conversionType == typeof(Boolean)) return value.ToDGBool();
        if (conversionType == typeof(Single)) return value == null ? 0f : Single.Parse(value, NumberStyles.Any);
        if (conversionType == typeof(Double)) return value == null ? 0.0 : Double.Parse(value, NumberStyles.Any);
        if (conversionType == typeof(Decimal)) return value == null ? 0m : Decimal.Parse(value, NumberStyles.Any);
        if (conversionType == typeof(DateTime)) return value == null ? DateTime.MinValue : DateTime.Parse(value, CultureInfo.CurrentCulture, DateTimeStyles.None);
        if (conversionType == typeof(Char)) return Convert.ToChar(value);
        if (conversionType == typeof(SByte)) return SByte.Parse(value, NumberStyles.Any);
        if (conversionType == typeof(Byte)) return Byte.Parse(value, NumberStyles.Any);
        if (conversionType == typeof(Int16)) return value == null ? 0 : (Int32)Int16.Parse(value);
        if (conversionType == typeof(UInt16)) return value == null ? 0 : (Int32)UInt16.Parse(value, NumberStyles.Any);
        if (conversionType == typeof(UInt32)) return value == null ? 0U : UInt32.Parse(value, NumberStyles.Any);
        if (conversionType == typeof(Int64)) return value == null ? 0L : Int64.Parse(value, NumberStyles.Any);
        if (conversionType == typeof(UInt64)) return value == null ? 0UL : UInt64.Parse(value, NumberStyles.Any);
        if (conversionType == typeof(Guid)) return value == null ? Guid.Empty : new Guid(value);

        return null;
    }
}

/// <summary>ConvertHelper 内部使用的布尔转换扩展</summary>
file static class ConvertHelperExtensions
{
    /// <summary>字符串转布尔（支持 true/false, 1/0, yes/no, on/off, Y/N, T/F）</summary>
    public static Boolean ToDGBool(this String? value)
    {
        if (value == null) return false;
        value = value.Trim();
        if (value.Length == 0) return false;

        return value.ToUpperInvariant() switch
        {
            "TRUE" or "1" or "YES" or "ON" or "Y" or "T" or "是" => true,
            _ => false
        };
    }
}
