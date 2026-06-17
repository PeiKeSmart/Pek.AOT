using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Pek;

/// <summary>
/// 字符(<see cref="Char"/>) 扩展
/// </summary>
public static class CharExtensions
{
    #region In(判断当前字符是否在目标字符数组中)

    /// <summary>
    /// 判断当前字符是否在目标字符数组中
    /// </summary>
    /// <param name="this">字符</param>
    /// <param name="values">字符数组</param>
    /// <returns></returns>
    public static Boolean In(this Char @this, params Char[] values)
    {
        return Array.IndexOf(values, @this) != -1;
    }

    #endregion

    #region NotIn(判断当前字符是否不在目标字符数组中)

    /// <summary>
    /// 判断当前字符是否不在目标字符数组中
    /// </summary>
    /// <param name="this">字符</param>
    /// <param name="values">字符数组</param>
    /// <returns></returns>
    public static Boolean NotIn(this Char @this, params Char[] values)
    {
        return Array.IndexOf(values, @this) == -1;
    }

    #endregion

    #region Repeat(重复拼接字符)

    /// <summary>
    /// 重复拼接字符
    /// </summary>
    /// <param name="this">字符</param>
    /// <param name="repeatCount">重复数</param>
    /// <returns></returns>
    public static String Repeat(this Char @this, Int32 repeatCount)
    {
        return new String(@this, repeatCount);
    }

    #endregion

    #region GetAscii(获取ASCII编码)

    /// <summary>
    /// 获取ASCII编码
    /// </summary>
    /// <param name="value">值</param>
    /// <returns></returns>
    public static Int32 GetAsciiCode(this Char value)
    {
        var bytes = Encoding.GetEncoding(0).GetBytes(value.ToString());
        if (bytes.Length == 1)
        {
            return bytes[0];
        }

        return (((bytes[0] * 0x100) + bytes[1]) - 0x10000);
    }

    #endregion

    #region IsChinese(是否中文字符串)

    /// <summary>
    /// 是否中文字符串
    /// </summary>
    /// <param name="value">值</param>
    /// <returns></returns>
    public static Boolean IsChinese(this Char value)
    {
        return Regex.IsMatch(value.ToString(), "^[一-龥]$");
    }

    #endregion

    #region IsLine(是否行标识)

    /// <summary>
    /// 是否行标识
    /// </summary>
    /// <param name="value">值</param>
    /// <returns></returns>
    public static Boolean IsLine(this Char value)
    {
        if (value != '\r')
        {
            return (value == '\n');
        }

        return true;
    }

    #endregion

    #region IsDoubleByte(是否双字节字符)

    /// <summary>
    /// 是否双字节字符
    /// </summary>
    /// <param name="value">值</param>
    /// <returns></returns>
    public static Boolean IsDoubleByte(this Char value)
    {
        return Regex.IsMatch(value.ToString(), @"[^\x00-\xff]");
    }

    #endregion

    #region ToDBC(转换为半角字符)

    /// <summary>
    /// 转换为半角字符
    /// </summary>
    /// <param name="value">值</param>
    /// <returns></returns>
    // ReSharper disable once InconsistentNaming
    public static Char ToDBC(this Char value)
    {
        if (value == 12288)
        {
            value = (Char)32;
        }

        if (value > 65280 && value < 65375)
        {
            value = (Char)(value - 65248);
        }

        return value;
    }

    #endregion

    #region ToSBC(转换为全角字符)

    /// <summary>
    /// 转换为全角字符
    /// </summary>
    /// <param name="value">值</param>
    /// <returns></returns>
    // ReSharper disable once InconsistentNaming
    public static Char ToSBC(this Char value)
    {
        if (value == 32)
        {
            value = (Char)12288;
        }

        if (value < 127)
        {
            value = (Char)(value + 65248);
        }

        return value;
    }

    #endregion

    /// <summary>
    /// Converts a given character from the hex representation (0-9A-Fa-f)
    /// to an integer.
    /// </summary>
    /// <param name="c">The character to convert.</param>
    /// <returns>
    /// The integer value or undefined behavior if invalid.
    /// </returns>
    public static Int32 FromHex(this Char c) => c is >= '0' and <= '9' ? c - 0x30 : c - (c is >= 'a' and <= 'z' ? 0x57 : 0x37);

    /// <summary>
    /// Transforms the given character to a hexadecimal string.
    /// </summary>
    /// <param name="character">The single character.</param>
    /// <returns>A minimal digit lower case hexadecimal string.</returns>
    public static String ToHex(this Char character) => ((Int32)character).ToString("x");

    /// <summary>
    /// Determines if the given character is in the given range.
    /// </summary>
    /// <param name="c">The character to examine.</param>
    /// <param name="lower">The lower bound of the range.</param>
    /// <param name="upper">The upper bound of the range.</param>
    /// <returns>The result of the test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Boolean IsInRange(this Char c, Int32 lower, Int32 upper) => c >= lower && c <= upper;
}
