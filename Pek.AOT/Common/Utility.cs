using System.ComponentModel;
using System.Globalization;

using Pek.Extension;

namespace Pek;

/// <summary>工具类</summary>
public static class Utility
{
    /// <summary>类型转换提供者</summary>
    public static DefaultConvert Convert { get; set; } = new();

    /// <summary>转为整数，转换失败时返回默认值</summary>
    public static Int32 ToInt(this Object? value, Int32 defaultValue = 0) => Convert.ToInt(value, defaultValue);

    /// <summary>转为浮点数，转换失败时返回默认值</summary>
    public static Double ToDouble(this Object? value, Double defaultValue = 0) => Convert.ToDouble(value, defaultValue);

    /// <summary>转为布尔型，转换失败时返回默认值</summary>
    public static Boolean ToBoolean(this Object? value, Boolean defaultValue = false) => Convert.ToBoolean(value, defaultValue);

    /// <summary>转为时间日期，转换失败时返回最小时间</summary>
    public static DateTime ToDateTime(this Object? value) => Convert.ToDateTime(value, DateTime.MinValue);

    /// <summary>转为时间日期，转换失败时返回默认值</summary>
    public static DateTime ToDateTime(this Object? value, DateTime defaultValue) => Convert.ToDateTime(value, defaultValue);
}

/// <summary>默认转换器</summary>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public class DefaultConvert
{
    private static readonly DateTime _dt1970 = new(1970, 1, 1);

    /// <summary>转为整数，转换失败时返回默认值</summary>
    public virtual Int32 ToInt(Object? value, Int32 defaultValue)
    {
        if (value is Int32 n) return n;
        if (value == null || value == DBNull.Value) return defaultValue;

        if (value is Byte[] buffer)
        {
            if (buffer.Length >= 4) return BitConverter.ToInt32(buffer, 0);
            if (buffer.Length > 0)
            {
                Span<Byte> temp = stackalloc Byte[4];
                buffer.AsSpan().CopyTo(temp);
                return BitConverter.ToInt32(temp);
            }
            return defaultValue;
        }

        if (value is ReadOnlyMemory<Byte> memory)
            return ToInt(memory.ToArray(), defaultValue);

        if (value is String str)
        {
            if (str.Length == 0) return defaultValue;
            if (Int32.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) return n;
            if (Int32.TryParse(str, NumberStyles.Integer, CultureInfo.CurrentCulture, out n)) return n;
            if (Double.TryParse(str, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var d1)) return (Int32)d1;
            if (Double.TryParse(str, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var d2)) return (Int32)d2;

            return defaultValue;
        }

        if (value is Boolean b) return b ? 1 : 0;
        if (value is DateTime dt)
        {
            var seconds = (dt - _dt1970).TotalSeconds;
            return seconds >= Int32.MaxValue ? Int32.MaxValue : seconds <= Int32.MinValue ? Int32.MinValue : (Int32)seconds;
        }

        try
        {
            return System.Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            try
            {
                return System.Convert.ToInt32(value, CultureInfo.CurrentCulture);
            }
            catch
            {
                return defaultValue;
            }
        }
    }

    /// <summary>转为浮点数，转换失败时返回默认值</summary>
    public virtual Double ToDouble(Object? value, Double defaultValue)
    {
        if (value is Double n) return n;
        if (value == null || value == DBNull.Value) return defaultValue;

        if (value is Byte[] buffer)
        {
            if (buffer.Length >= 8) return BitConverter.ToDouble(buffer, 0);
            if (buffer.Length > 0)
            {
                Span<Byte> temp = stackalloc Byte[8];
                buffer.AsSpan().CopyTo(temp);
                return BitConverter.ToDouble(temp);
            }
            return defaultValue;
        }

        if (value is ReadOnlyMemory<Byte> memory)
            return ToDouble(memory.ToArray(), defaultValue);

        if (value is String str)
        {
            if (str.Length == 0) return defaultValue;
            if (Double.TryParse(str, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out n)) return n;
            if (Double.TryParse(str, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out n)) return n;
            return defaultValue;
        }

        try
        {
            return System.Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            try
            {
                return System.Convert.ToDouble(value, CultureInfo.CurrentCulture);
            }
            catch
            {
                return defaultValue;
            }
        }
    }

    /// <summary>转为布尔型，转换失败时返回默认值</summary>
    public virtual Boolean ToBoolean(Object? value, Boolean defaultValue)
    {
        if (value is Boolean b) return b;
        if (value == null || value == DBNull.Value) return defaultValue;

        if (value is String str)
        {
            if (str.IsNullOrWhiteSpace()) return defaultValue;
            if (Boolean.TryParse(str, out b)) return b;
            if (Int32.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) return n != 0;
            if (str.EqualIgnoreCase("y", "yes", "on", "true")) return true;
            if (str.EqualIgnoreCase("n", "no", "off", "false")) return false;
            return defaultValue;
        }

        try
        {
            return System.Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>转为时间日期，转换失败时返回默认值</summary>
    public virtual DateTime ToDateTime(Object? value, DateTime defaultValue)
    {
        if (value is DateTime dt) return dt;
        if (value == null || value == DBNull.Value) return defaultValue;

        if (value is String str)
        {
            if (str.IsNullOrWhiteSpace()) return defaultValue;
            if (DateTime.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out dt)) return dt;
            if (DateTime.TryParse(str, CultureInfo.CurrentCulture, DateTimeStyles.RoundtripKind, out dt)) return dt;
            if (Int64.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                return n > 10_000_000_000L ? _dt1970.AddMilliseconds(n) : _dt1970.AddSeconds(n);

            return defaultValue;
        }

        if (value is Int64 longValue) return longValue > 10_000_000_000L ? _dt1970.AddMilliseconds(longValue) : _dt1970.AddSeconds(longValue);
        if (value is Int32 intValue) return _dt1970.AddSeconds(intValue);

        try
        {
            return System.Convert.ToDateTime(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            try
            {
                return System.Convert.ToDateTime(value, CultureInfo.CurrentCulture);
            }
            catch
            {
                return defaultValue;
            }
        }
    }
}