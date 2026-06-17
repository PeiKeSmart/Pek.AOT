using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Pek;

/// <summary>
/// 通用核心扩展方法（上游 Pek.Common Common/CoreExtension 迁移，AOT 安全子集）
/// </summary>
/// <remarks>
/// AOT 兼容说明：
/// - Array 段已跳过（与现有 ArrayExtensions.cs 方法签名冲突）
/// - 所有方法均为纯 BCL API 包装，无 Expression.Compile、无 Activator.CreateInstance、无 MakeGenericType
/// - 使用 String/Int32/Boolean 等 .NET 正式类型名
/// </remarks>
public static class CoreExtensions
{
    #region Boolean

    /// <summary>如果值为 true 则执行指定操作</summary>
    /// <param name="this">布尔值</param>
    /// <param name="action">操作</param>
    public static void IfTrue(this Boolean @this, Action action)
    {
        if (@this)
            action();
    }

    /// <summary>如果值为 false 则执行指定操作</summary>
    /// <param name="this">布尔值</param>
    /// <param name="action">操作</param>
    public static void IfFalse(this Boolean @this, Action action)
    {
        if (!@this)
            action();
    }

    /// <summary>根据布尔值返回不同的字符串</summary>
    /// <param name="this">布尔值</param>
    /// <param name="trueValue">true 时的字符串</param>
    /// <param name="falseValue">false 时的字符串</param>
    /// <returns></returns>
    public static String ToString(this Boolean @this, String trueValue, String falseValue) => @this ? trueValue : falseValue;

    #endregion

    #region Byte

    /// <summary>返回两个 8 位无符号整数中较大的一个</summary>
    public static Byte Max(this Byte val1, Byte val2) => Math.Max(val1, val2);

    /// <summary>返回两个 8 位无符号整数中较小的一个</summary>
    public static Byte Min(this Byte val1, Byte val2) => Math.Min(val1, val2);

    #endregion

    #region ByteArray

    /// <summary>将字节数组转换为 Base64 字符串</summary>
    public static String ToBase64String(this Byte[] inArray) => Convert.ToBase64String(inArray);

    /// <summary>将字节数组转换为 Base64 字符串（带格式选项）</summary>
    public static String ToBase64String(this Byte[] inArray, Base64FormattingOptions options) => Convert.ToBase64String(inArray, options);

    /// <summary>将字节数组的子集转换为 Base64 字符串</summary>
    public static String ToBase64String(this Byte[] inArray, Int32 offset, Int32 length) => Convert.ToBase64String(inArray, offset, length);

    /// <summary>将字节数组的子集转换为 Base64 字符串（带格式选项）</summary>
    public static String ToBase64String(this Byte[] inArray, Int32 offset, Int32 length, Base64FormattingOptions options) => Convert.ToBase64String(inArray, offset, length, options);

    /// <summary>调整字节数组大小</summary>
    /// <param name="this">字节数组</param>
    /// <param name="newSize">新大小</param>
    /// <returns></returns>
    public static Byte[] Resize(this Byte[] @this, Int32 newSize)
    {
        Array.Resize(ref @this, newSize);
        return @this;
    }

    /// <summary>将字节数组转换为 MemoryStream</summary>
    public static MemoryStream ToMemoryStream(this Byte[] byteArray) => new(byteArray);

    /// <summary>使用 UTF-8 编码将字节数组转换为字符串</summary>
    public static String GetString(this Byte[] byteArray) => byteArray.GetString(Encoding.UTF8);

    /// <summary>使用指定编码将字节数组转换为字符串</summary>
    public static String GetString(this Byte[] byteArray, Encoding encoding) => encoding.GetString(byteArray);

    #endregion

    #region Char

    /// <summary>重复字符指定次数</summary>
    public static String Repeat(this Char @this, Int32 repeatCount) => new(@this, repeatCount);

    /// <summary>获取 Unicode 字符的数值</summary>
    public static Double GetNumericValue(this Char c) => Char.GetNumericValue(c);

    /// <summary>获取 Unicode 字符的分类</summary>
    public static UnicodeCategory GetUnicodeCategory(this Char c) => Char.GetUnicodeCategory(c);

    /// <summary>指示是否为控制字符</summary>
    public static Boolean IsControl(this Char c) => Char.IsControl(c);

    /// <summary>指示是否为字母或十进制数字</summary>
    public static Boolean IsLetterOrDigit(this Char c) => Char.IsLetterOrDigit(c);

    /// <summary>指示是否为小写字母</summary>
    public static Boolean IsLower(this Char c) => Char.IsLower(c);

    /// <summary>指示是否为大写字母</summary>
    public static Boolean IsUpper(this Char c) => Char.IsUpper(c);

    /// <summary>指示是否为数字</summary>
    public static Boolean IsNumber(this Char c) => Char.IsNumber(c);

    /// <summary>指示是否为分隔符</summary>
    public static Boolean IsSeparator(this Char c) => Char.IsSeparator(c);

    /// <summary>指示是否为符号字符</summary>
    public static Boolean IsSymbol(this Char c) => Char.IsSymbol(c);

    /// <summary>指示是否为空白字符</summary>
    public static Boolean IsWhiteSpace(this Char c) => Char.IsWhiteSpace(c);

    /// <summary>转换为小写（使用指定区域性）</summary>
    public static Char ToLower(this Char c, CultureInfo culture) => Char.ToLower(c, culture);

    /// <summary>转换为小写</summary>
    public static Char ToLower(this Char c) => Char.ToLower(c);

    /// <summary>转换为小写（使用固定区域性）</summary>
    public static Char ToLowerInvariant(this Char c) => Char.ToLowerInvariant(c);

    /// <summary>转换为大写（使用指定区域性）</summary>
    public static Char ToUpper(this Char c, CultureInfo culture) => Char.ToUpper(c, culture);

    /// <summary>转换为大写</summary>
    public static Char ToUpper(this Char c) => Char.ToUpper(c);

    /// <summary>转换为大写（使用固定区域性）</summary>
    public static Char ToUpperInvariant(this Char c) => Char.ToUpperInvariant(c);

    #endregion

    #region DateTime

    /// <summary>计算年龄</summary>
    public static Int32 Age(this DateTime @this)
    {
        if (DateTime.Today.Month < @this.Month ||
            DateTime.Today.Month == @this.Month &&
            DateTime.Today.Day < @this.Day)
        {
            return DateTime.Today.Year - @this.Year - 1;
        }
        return DateTime.Today.Year - @this.Year;
    }

    /// <summary>判断两个日期是否日期相等（忽略时间）</summary>
    public static Boolean IsDateEqual(this DateTime date, DateTime dateToCompare) => date.Date == dateToCompare.Date;

    /// <summary>判断是否是今天</summary>
    public static Boolean IsToday(this DateTime @this) => @this.Date == DateTime.Today;

    /// <summary>判断是否是工作日</summary>
    public static Boolean IsWeekDay(this DateTime @this) => !(@this.DayOfWeek == DayOfWeek.Saturday || @this.DayOfWeek == DayOfWeek.Sunday);

    /// <summary>判断是否是周末</summary>
    public static Boolean IsWeekendDay(this DateTime @this) => @this.DayOfWeek == DayOfWeek.Saturday || @this.DayOfWeek == DayOfWeek.Sunday;

    /// <summary>获取当天的开始时刻（00:00:00.000）</summary>
    public static DateTime StartOfDay(this DateTime @this) => new(@this.Year, @this.Month, @this.Day);

    /// <summary>获取当月的第一天</summary>
    public static DateTime StartOfMonth(this DateTime @this) => new(@this.Year, @this.Month, 1);

    /// <summary>获取当周的第一天</summary>
    public static DateTime StartOfWeek(this DateTime dt, DayOfWeek startDayOfWeek = DayOfWeek.Sunday)
    {
        var start = new DateTime(dt.Year, dt.Month, dt.Day);

        if (start.DayOfWeek != startDayOfWeek)
        {
            var d = startDayOfWeek - start.DayOfWeek;
            if (startDayOfWeek <= start.DayOfWeek)
                return start.AddDays(d);
            return start.AddDays(-7 + d);
        }

        return start;
    }

    /// <summary>获取当年的第一天</summary>
    public static DateTime StartOfYear(this DateTime @this) => new(@this.Year, 1, 1);

    /// <summary>转换为 Unix 时间戳时间跨度</summary>
    public static TimeSpan ToEpochTimeSpan(this DateTime @this) => @this.ToUniversalTime().Subtract(new DateTime(1970, 1, 1));

    /// <summary>判断值是否在指定范围内（含边界）</summary>
    public static Boolean InRange(this DateTime @this, DateTime minValue, DateTime maxValue) => @this.CompareTo(minValue) >= 0 && @this.CompareTo(maxValue) <= 0;

    /// <summary>将时间转换到指定时区</summary>
    public static DateTime ConvertTime(this DateTime dateTime, TimeZoneInfo destinationTimeZone) => TimeZoneInfo.ConvertTime(dateTime, destinationTimeZone);

    /// <summary>将时间从源时区转换到目标时区</summary>
    public static DateTime ConvertTime(this DateTime dateTime, TimeZoneInfo sourceTimeZone, TimeZoneInfo destinationTimeZone) => TimeZoneInfo.ConvertTime(dateTime, sourceTimeZone, destinationTimeZone);

    /// <summary>将 UTC 时间转换到指定时区</summary>
    public static DateTime ConvertTimeFromUtc(this DateTime dateTime, TimeZoneInfo destinationTimeZone) => TimeZoneInfo.ConvertTimeFromUtc(dateTime, destinationTimeZone);

    /// <summary>将时间转换为 UTC</summary>
    public static DateTime ConvertTimeToUtc(this DateTime dateTime) => TimeZoneInfo.ConvertTimeToUtc(dateTime);

    /// <summary>将指定时区的时间转换为 UTC</summary>
    public static DateTime ConvertTimeToUtc(this DateTime dateTime, TimeZoneInfo sourceTimeZone) => TimeZoneInfo.ConvertTimeToUtc(dateTime, sourceTimeZone);

    /// <summary>格式化日期字符串 "yyyy-MM-dd"</summary>
    public static String ToStandardDateString(this DateTime @this) => @this.ToString("yyyy-MM-dd");

    /// <summary>格式化日期时间字符串 "yyyy-MM-dd HH:mm:ss"</summary>
    public static String ToStandardTimeString(this DateTime @this) => @this.ToString("yyyy-MM-dd HH:mm:ss");

    #endregion

    #region Decimal

    /// <summary>判断值是否在指定范围内（含边界）</summary>
    public static Boolean InRange(this Decimal @this, Decimal minValue, Decimal maxValue) => @this.CompareTo(minValue) >= 0 && @this.CompareTo(maxValue) <= 0;

    /// <summary>返回大于等于指定数值的最小整数</summary>
    public static Decimal Ceiling(this Decimal d) => Math.Ceiling(d);

    /// <summary>返回小于等于指定数值的最大整数</summary>
    public static Decimal Floor(this Decimal d) => Math.Floor(d);

    /// <summary>返回两个数值中较大的一个</summary>
    public static Decimal Max(this Decimal val1, Decimal val2) => Math.Max(val1, val2);

    /// <summary>返回两个数值中较小的一个</summary>
    public static Decimal Min(this Decimal val1, Decimal val2) => Math.Min(val1, val2);

    /// <summary>四舍五入到最接近的整数值</summary>
    public static Decimal Round(this Decimal d) => Math.Round(d);

    /// <summary>四舍五入到指定小数位数</summary>
    public static Decimal Round(this Decimal d, Int32 decimals) => Math.Round(d, decimals);

    /// <summary>四舍五入（指定舍入模式）</summary>
    public static Decimal Round(this Decimal d, MidpointRounding mode) => Math.Round(d, mode);

    /// <summary>四舍五入到指定小数位数（指定舍入模式）</summary>
    public static Decimal Round(this Decimal d, Int32 decimals, MidpointRounding mode) => Math.Round(d, decimals, mode);

    /// <summary>返回值的符号</summary>
    public static Int32 Sign(this Decimal value) => Math.Sign(value);

    /// <summary>截断小数部分</summary>
    public static Decimal Truncate(this Decimal d) => Math.Truncate(d);

    /// <summary>转换为货币格式（保留两位小数）</summary>
    public static Decimal ToMoney(this Decimal @this) => Math.Round(@this, 2);

    #endregion

    #region Delegate

    /// <summary>连接两个委托的调用列表</summary>
    public static Delegate Combine(this Delegate a, Delegate b) => Delegate.Combine(a, b);

    /// <summary>从委托的调用列表中移除最后一次出现的指定委托</summary>
    public static Delegate? Remove(this Delegate source, Delegate value) => Delegate.Remove(source, value);

    /// <summary>从委托的调用列表中移除所有出现的指定委托</summary>
    public static Delegate? RemoveAll(this Delegate source, Delegate value) => Delegate.RemoveAll(source, value);

    #endregion

    #region Double

    /// <summary>返回绝对值</summary>
    public static Double Abs(this Double value) => Math.Abs(value);

    /// <summary>返回反余弦值</summary>
    public static Double Acos(this Double d) => Math.Acos(d);

    /// <summary>返回反正弦值</summary>
    public static Double Asin(this Double d) => Math.Asin(d);

    /// <summary>返回反正切值</summary>
    public static Double Atan(this Double d) => Math.Atan(d);

    /// <summary>返回两个数值商的反正切值</summary>
    public static Double Atan2(this Double y, Double x) => Math.Atan2(y, x);

    /// <summary>返回大于等于指定数值的最小整数（返回 Int32）</summary>
    public static Int32 Ceiling(this Double a) => Convert.ToInt32(Math.Ceiling(a));

    /// <summary>返回余弦值</summary>
    public static Double Cos(this Double d) => Math.Cos(d);

    /// <summary>返回双曲余弦值</summary>
    public static Double Cosh(this Double value) => Math.Cosh(value);

    /// <summary>返回 e 的指定次幂</summary>
    public static Double Exp(this Double d) => Math.Exp(d);

    /// <summary>返回小于等于指定数值的最大整数（返回 Int32）</summary>
    public static Int32 Floor(this Double d) => Convert.ToInt32(Math.Floor(d));

    /// <summary>返回两数相除的余数</summary>
    public static Double IEEERemainder(this Double x, Double y) => Math.IEEERemainder(x, y);

    /// <summary>返回自然对数</summary>
    public static Double Log(this Double d) => Math.Log(d);

    /// <summary>返回指定底数的对数</summary>
    public static Double Log(this Double d, Double newBase) => Math.Log(d, newBase);

    /// <summary>返回以 10 为底的对数</summary>
    public static Double Log10(this Double d) => Math.Log10(d);

    /// <summary>返回两个数值中较大的一个</summary>
    public static Double Max(this Double val1, Double val2) => Math.Max(val1, val2);

    /// <summary>返回两个数值中较小的一个</summary>
    public static Double Min(this Double val1, Double val2) => Math.Min(val1, val2);

    /// <summary>返回指定数值的指定次幂</summary>
    public static Double Pow(this Double x, Double y) => Math.Pow(x, y);

    /// <summary>四舍五入到最接近的整数值</summary>
    public static Double Round(this Double a) => Math.Round(a);

    /// <summary>四舍五入到指定小数位数</summary>
    public static Double Round(this Double a, Int32 digits) => Math.Round(a, digits);

    /// <summary>四舍五入（指定舍入模式）</summary>
    public static Double Round(this Double a, MidpointRounding mode) => Math.Round(a, mode);

    /// <summary>四舍五入到指定小数位数（指定舍入模式）</summary>
    public static Double Round(this Double value, Int32 digits, MidpointRounding mode) => Math.Round(value, digits, mode);

    /// <summary>返回值的符号</summary>
    public static Int32 Sign(this Double value) => Math.Sign(value);

    /// <summary>返回正弦值</summary>
    public static Double Sin(this Double a) => Math.Sin(a);

    /// <summary>返回双曲正弦值</summary>
    public static Double Sinh(this Double value) => Math.Sinh(value);

    /// <summary>返回平方根</summary>
    public static Double Sqrt(this Double d) => Math.Sqrt(d);

    /// <summary>返回正切值</summary>
    public static Double Tan(this Double a) => Math.Tan(a);

    /// <summary>返回双曲正切值</summary>
    public static Double Tanh(this Double value) => Math.Tanh(value);

    /// <summary>截断小数部分</summary>
    public static Double Truncate(this Double d) => Math.Truncate(d);

    /// <summary>转换为货币格式（保留两位小数）</summary>
    public static Double ToMoney(this Double @this) => Math.Round(@this, 2);

    #endregion

    #region Enum

    /// <summary>判断枚举值是否在指定值列表中</summary>
    public static Boolean In(this Enum @this, params Enum[] values) => Array.IndexOf(values, @this) >= 0;

    #endregion

    #region EventHandler

    /// <summary>触发事件</summary>
    public static void RaiseEvent(this EventHandler @this, Object sender) => @this?.Invoke(sender, EventArgs.Empty);

    /// <summary>触发事件（带参数）</summary>
    public static void RaiseEvent(this EventHandler handler, Object sender, EventArgs e) => handler?.Invoke(sender, e);

    /// <summary>触发泛型事件</summary>
    public static void RaiseEvent<TEventArgs>(this EventHandler<TEventArgs> @this, Object sender) where TEventArgs : EventArgs => @this?.Invoke(sender, default!);

    /// <summary>触发泛型事件（带参数）</summary>
    public static void RaiseEvent<TEventArgs>(this EventHandler<TEventArgs> @this, Object sender, TEventArgs e) where TEventArgs : EventArgs => @this?.Invoke(sender, e);

    #endregion

    #region Guid

    /// <summary>判断可空 Guid 是否为空或 Guid.Empty</summary>
    public static Boolean IsNullOrEmpty(this Guid? @this) => !@this.HasValue || @this == Guid.Empty;

    /// <summary>判断可空 Guid 是否非空且非 Guid.Empty</summary>
    public static Boolean IsNotNullOrEmpty(this Guid? @this) => @this.HasValue && @this.Value != Guid.Empty;

    /// <summary>判断 Guid 是否为 Guid.Empty</summary>
    public static Boolean IsEmpty(this Guid @this) => @this == Guid.Empty;

    /// <summary>判断 Guid 是否非 Guid.Empty</summary>
    public static Boolean IsNotEmpty(this Guid @this) => @this != Guid.Empty;

    #endregion

    #region Int16

    /// <summary>判断值是否在指定范围内（含边界）</summary>
    public static Boolean InRange(this Int16 @this, Int16 minValue, Int16 maxValue) => @this.CompareTo(minValue) >= 0 && @this.CompareTo(maxValue) <= 0;

    /// <summary>判断是否为指定数的因子</summary>
    public static Boolean FactorOf(this Int16 @this, Int16 factorNumer) => factorNumer % @this == 0;

    /// <summary>判断是否为偶数</summary>
    public static Boolean IsEven(this Int16 @this) => @this % 2 == 0;

    /// <summary>判断是否为奇数</summary>
    public static Boolean IsOdd(this Int16 @this) => @this % 2 != 0;

    /// <summary>判断是否为质数</summary>
    public static Boolean IsPrime(this Int16 @this)
    {
        if (@this == 1 || @this == 2)
            return true;

        if (@this % 2 == 0)
            return false;

        var sqrt = (Int16)Math.Sqrt(@this);
        for (Int64 t = 3; t <= sqrt; t += 2)
        {
            if (@this % t == 0)
                return false;
        }

        return true;
    }

    /// <summary>获取字节数组表示</summary>
    public static Byte[] GetBytes(this Int16 value) => BitConverter.GetBytes(value);

    /// <summary>返回两个值中较大的一个</summary>
    public static Int16 Max(this Int16 val1, Int16 val2) => Math.Max(val1, val2);

    /// <summary>返回两个值中较小的一个</summary>
    public static Int16 Min(this Int16 val1, Int16 val2) => Math.Min(val1, val2);

    /// <summary>返回值的符号</summary>
    public static Int32 Sign(this Int16 value) => Math.Sign(value);

    /// <summary>将主机字节序转换为网络字节序</summary>
    public static Int16 HostToNetworkOrder(this Int16 host) => IPAddress.HostToNetworkOrder(host);

    /// <summary>将网络字节序转换为主机字节序</summary>
    public static Int16 NetworkToHostOrder(this Int16 network) => IPAddress.NetworkToHostOrder(network);

    #endregion

    #region Int32

    /// <summary>判断值是否在指定范围内（含边界）</summary>
    public static Boolean InRange(this Int32 @this, Int32 minValue, Int32 maxValue) => @this.CompareTo(minValue) >= 0 && @this.CompareTo(maxValue) <= 0;

    /// <summary>判断是否为指定数的因子</summary>
    public static Boolean FactorOf(this Int32 @this, Int32 factorNumer) => factorNumer % @this == 0;

    /// <summary>判断是否为偶数</summary>
    public static Boolean IsEven(this Int32 @this) => @this % 2 == 0;

    /// <summary>判断是否为奇数</summary>
    public static Boolean IsOdd(this Int32 @this) => @this % 2 != 0;

    /// <summary>判断是否为指定数的倍数</summary>
    public static Boolean IsMultipleOf(this Int32 @this, Int32 factor) => @this % factor == 0;

    /// <summary>判断是否为质数</summary>
    public static Boolean IsPrime(this Int32 @this)
    {
        if (@this == 1 || @this == 2)
            return true;

        if (@this % 2 == 0)
            return false;

        var sqrt = (Int32)Math.Sqrt(@this);
        for (var t = 3; t <= sqrt; t += 2)
        {
            if (@this % t == 0)
                return false;
        }

        return true;
    }

    /// <summary>获取字节数组表示</summary>
    public static Byte[] GetBytes(this Int32 value) => BitConverter.GetBytes(value);

    /// <summary>将 Unicode 代码点转换为 UTF-16 字符串</summary>
    public static String ConvertFromUtf32(this Int32 utf32) => Char.ConvertFromUtf32(utf32);

    /// <summary>返回指定年月的天数</summary>
    public static Int32 DaysInMonth(this Int32 year, Int32 month) => DateTime.DaysInMonth(year, month);

    /// <summary>判断是否为闰年</summary>
    public static Boolean IsLeapYear(this Int32 year) => DateTime.IsLeapYear(year);

    /// <summary>返回绝对值</summary>
    public static Int32 Abs(this Int32 value) => Math.Abs(value);

    /// <summary>返回两个 32 位数的完整乘积（64 位）</summary>
    public static Int64 BigMul(this Int32 a, Int32 b) => Math.BigMul(a, b);

    /// <summary>计算商和余数</summary>
    public static Int32 DivRem(this Int32 a, Int32 b, out Int32 result) => Math.DivRem(a, b, out result);

    /// <summary>返回两个值中较大的一个</summary>
    public static Int32 Max(this Int32 val1, Int32 val2) => Math.Max(val1, val2);

    /// <summary>返回两个值中较小的一个</summary>
    public static Int32 Min(this Int32 val1, Int32 val2) => Math.Min(val1, val2);

    /// <summary>返回值的符号</summary>
    public static Int32 Sign(this Int32 value) => Math.Sign(value);

    #endregion

    #region Int64

    /// <summary>从刻度数创建 TimeSpan</summary>
    public static TimeSpan FromTicks(this Int64 value) => TimeSpan.FromTicks(value);

    /// <summary>获取字节数组表示</summary>
    public static Byte[] GetBytes(this Int64 value) => BitConverter.GetBytes(value);

    /// <summary>将 64 位整数转换为双精度浮点数</summary>
    public static Double Int64BitsToDouble(this Int64 value) => BitConverter.Int64BitsToDouble(value);

    #endregion

    #region Object

    /// <summary>尝试转换为指定类型，失败返回默认值</summary>
    public static T? AsOrDefault<T>(this Object @this)
    {
        try
        {
            return (T)@this;
        }
        catch (Exception)
        {
            return default;
        }
    }

    /// <summary>尝试转换为指定类型，失败返回指定默认值</summary>
    public static T AsOrDefault<T>(this Object @this, T defaultValue)
    {
        try
        {
            return (T)@this;
        }
        catch (Exception)
        {
            return defaultValue;
        }
    }

    /// <summary>尝试转换为指定类型，失败调用默认值工厂</summary>
    public static T AsOrDefault<T>(this Object @this, Func<T> defaultValueFactory)
    {
        try
        {
            return (T)@this;
        }
        catch (Exception)
        {
            return defaultValueFactory();
        }
    }

    /// <summary>尝试转换为指定类型，失败调用默认值工厂</summary>
    public static T AsOrDefault<T>(this Object @this, Func<Object, T> defaultValueFactory)
    {
        try
        {
            return (T)@this;
        }
        catch (Exception)
        {
            return defaultValueFactory(@this);
        }
    }

    /// <summary>将对象转换为指定类型。AOT 安全版：使用 Convert.ChangeType 替代 TypeDescriptor</summary>
    public static T? To<T>(this Object @this)
    {
        if (@this == null || @this == DBNull.Value)
            return (T?)(Object?)null;

        var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        var sourceType = Nullable.GetUnderlyingType(@this.GetType()) ?? @this.GetType();
        if (sourceType == targetType)
            return (T)@this;

        if (@this is IConvertible)
            return (T)Convert.ChangeType(@this, targetType);

        return (T)@this;
    }

    /// <summary>将对象转换为指定类型。AOT 安全版：使用 Convert.ChangeType 替代 TypeDescriptor</summary>
    public static Object? To(this Object @this, Type type)
    {
        if (@this == null || @this == DBNull.Value)
            return null;

        var targetType = Nullable.GetUnderlyingType(type) ?? type;
        var sourceType = Nullable.GetUnderlyingType(@this.GetType()) ?? @this.GetType();

        if (sourceType == targetType)
            return @this;

        if (@this is IConvertible)
            return Convert.ChangeType(@this, targetType);

        return @this;
    }

    /// <summary>将对象转换为指定类型，失败调用默认值工厂</summary>
    public static T? ToOrDefault<T>(this Object @this, Func<Object, T> defaultValueFactory)
    {
        try
        {
            return @this.To<T>();
        }
        catch (Exception)
        {
            return defaultValueFactory(@this);
        }
    }

    /// <summary>将对象转换为指定类型，失败返回默认值</summary>
    public static T? ToOrDefault<T>(this Object @this, Func<T> defaultValueFactory) => @this.ToOrDefault(x => defaultValueFactory());

    /// <summary>将对象转换为指定类型，失败返回类型默认值</summary>
    public static Object? ToOrDefault(this Object @this, Type type)
    {
        try
        {
            return @this.To(type);
        }
        catch (Exception)
        {
            return null; // AOT: GetDefaultValue() 不可用，错误恢复路径统一返回 null
        }
    }

    /// <summary>将对象转换为指定类型，失败返回默认值</summary>
    public static T? ToOrDefault<T>(this Object @this) => @this.ToOrDefault(x => default(T));

    /// <summary>将对象转换为指定类型，失败返回指定默认值</summary>
    public static T? ToOrDefault<T>(this Object @this, T defaultValue) => @this.ToOrDefault(x => defaultValue);

    /// <summary>判断对象是否可赋值给指定类型</summary>
    public static Boolean IsAssignableFrom<T>(this Object @this)
    {
        var type = @this.GetType();
        return type.IsAssignableFrom(typeof(T));
    }

    /// <summary>判断对象是否可赋值给指定类型</summary>
    public static Boolean IsAssignableFrom(this Object @this, Type targetType)
    {
        var type = @this.GetType();
        return type.IsAssignableFrom(targetType);
    }

    /// <summary>链式操作：执行指定操作后返回自身</summary>
    public static T? Chain<T>(this T @this, Action<T> action)
    {
        action?.Invoke(@this);
        return @this;
    }

    /// <summary>如果满足条件则返回 null</summary>
    public static T? NullIf<T>(this T @this, Func<T, Boolean> predicate) where T : class
    {
        if (predicate(@this))
            return null;
        return @this;
    }

    /// <summary>获取值或默认值</summary>
    public static TResult? GetValueOrDefault<T, TResult>(this T @this, Func<T, TResult> func)
    {
        try
        {
            return func(@this);
        }
        catch (Exception)
        {
            return default;
        }
    }

    /// <summary>获取值或指定默认值</summary>
    public static TResult GetValueOrDefault<T, TResult>(this T @this, Func<T, TResult> func, TResult defaultValue)
    {
        try
        {
            return func(@this);
        }
        catch (Exception)
        {
            return defaultValue;
        }
    }

    /// <summary>尝试执行函数，失败返回指定值</summary>
    public static TResult Try<TType, TResult>(this TType @this, Func<TType, TResult> tryFunction, TResult catchValue)
    {
        try
        {
            return tryFunction(@this);
        }
        catch
        {
            return catchValue;
        }
    }

    /// <summary>尝试执行函数，失败调用默认值工厂</summary>
    public static TResult Try<TType, TResult>(this TType @this, Func<TType, TResult> tryFunction, Func<TType, TResult> catchValueFactory)
    {
        try
        {
            return tryFunction(@this);
        }
        catch
        {
            return catchValueFactory(@this);
        }
    }

    /// <summary>尝试执行函数，通过 out 参数返回结果</summary>
    public static Boolean Try<TType, TResult>(this TType @this, Func<TType, TResult> tryFunction, out TResult? result)
    {
        try
        {
            result = tryFunction(@this);
            return true;
        }
        catch
        {
            result = default;
            return false;
        }
    }

    /// <summary>尝试执行函数，失败返回指定值（通过 out 参数）</summary>
    public static Boolean Try<TType, TResult>(this TType @this, Func<TType, TResult> tryFunction, TResult catchValue, out TResult result)
    {
        try
        {
            result = tryFunction(@this);
            return true;
        }
        catch
        {
            result = catchValue;
            return false;
        }
    }

    /// <summary>尝试执行函数，失败调用默认值工厂（通过 out 参数）</summary>
    public static Boolean Try<TType, TResult>(this TType @this, Func<TType, TResult> tryFunction, Func<TType, TResult> catchValueFactory, out TResult result)
    {
        try
        {
            result = tryFunction(@this);
            return true;
        }
        catch
        {
            result = catchValueFactory(@this);
            return false;
        }
    }

    /// <summary>尝试执行操作</summary>
    public static Boolean Try<TType>(this TType @this, Action<TType> tryAction)
    {
        try
        {
            tryAction(@this);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>尝试执行操作，失败执行回退操作</summary>
    public static Boolean Try<TType>(this TType @this, Action<TType> tryAction, Action<TType> catchAction)
    {
        try
        {
            tryAction(@this);
            return true;
        }
        catch
        {
            catchAction(@this);
            return false;
        }
    }

    /// <summary>判断值是否在指定范围内（含边界）</summary>
    public static Boolean InRange<T>(this T @this, T minValue, T maxValue) where T : IComparable<T> => @this.CompareTo(minValue) >= 0 && @this.CompareTo(maxValue) <= 0;

    /// <summary>判断是否为默认值</summary>
    public static Boolean IsDefault<T>(this T source) => typeof(T).IsValueType ? source?.Equals(default(T)) == true : source == null;

    #endregion

    #region Object[]

    /// <summary>获取对象数组中各元素的类型</summary>
    public static Type[] GetTypeArray(this Object[] args) => Type.GetTypeArray(args);

    #endregion

    #region Random

    /// <summary>从指定值中随机选取一个</summary>
    public static T OneOf<T>(this Random @this, params T[] values) => values[@this.Next(values.Length)];

    /// <summary>抛硬币（50% 几率返回 true）</summary>
    public static Boolean CoinToss(this Random @this) => @this.Next(2) == 0;

    #endregion

    #region String

    /// <summary>判断字符串是否为 null</summary>
    public static Boolean IsNull(this String @this) => @this == null;

    /// <summary>创建具有相同值的新字符串实例</summary>
    public static String Copy(this String str) => new(str.ToCharArray());

    /// <summary>检索系统对指定字符串的引用（字符串池）</summary>
    public static String Intern(this String str) => String.Intern(str);

    /// <summary>检索对指定字符串的引用（如果已入池）</summary>
    public static String? IsInterned(this String str) => String.IsInterned(str);

    /// <summary>使用指定分隔符连接集合元素</summary>
    public static String Join<T>(this String separator, IEnumerable<T> values) => String.Join(separator, values);

    /// <summary>指示正则表达式是否在输入字符串中找到匹配项</summary>
    public static Boolean IsMatch(this String input, String pattern) => Regex.IsMatch(input, pattern);

    /// <summary>指示正则表达式是否在输入字符串中找到匹配项（指定选项）</summary>
    public static Boolean IsMatch(this String input, String pattern, RegexOptions options) => Regex.IsMatch(input, pattern, options);

    /// <summary>连接字符串集合</summary>
    public static String Concatenate(this IEnumerable<String> @this)
    {
        var sb = new StringBuilder();

        foreach (var s in @this)
            sb.Append(s);

        return sb.ToString();
    }

    /// <summary>连接集合元素（使用转换函数）</summary>
    public static String Concatenate<T>(this IEnumerable<T> source, Func<T, String> func)
    {
        var sb = new StringBuilder();
        foreach (var item in source)
            sb.Append(func(item));

        return sb.ToString();
    }

    /// <summary>提取满足条件的字符组成新字符串</summary>
    public static String Extract(this String @this, Func<Char, Boolean> predicate) => new(@this.ToCharArray().Where(predicate).ToArray());

    #endregion

    // 以下段已跳过（与现有 AOT 文件冲突或已迁移）：
    // #region Array → 已迁移至 ArrayExtensions.cs（方法签名冲突）
}
