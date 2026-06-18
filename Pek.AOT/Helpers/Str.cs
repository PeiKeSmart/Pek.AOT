using System.Text;
using System.Text.RegularExpressions;

namespace Pek.Helpers;

/// <summary>字符串操作工具类</summary>
public partial class Str
{
    #region Join(将集合连接为带分隔符的字符串)

    /// <summary>将集合连接为带分隔符的字符串</summary>
    /// <typeparam name="T">集合元素类型</typeparam>
    /// <param name="list">集合</param>
    /// <param name="quotes">引号，默认不带引号，范例：单引号"'"</param>
    /// <param name="separator">分隔符，默认使用逗号分隔</param>
    public static String Join<T>(IEnumerable<T> list, String quotes = "", String separator = ",")
    {
        if (list == null) return String.Empty;
        var result = new StringBuilder();
        foreach (var each in list)
            result.AppendFormat("{0}{1}{0}{2}", quotes, each, separator);
        if (separator == "") return result.ToString();
        return result.ToString().TrimEnd(separator.ToCharArray());
    }

    #endregion

    #region ToUnicode(字符串转Unicode)

    /// <summary>字符串转Unicode</summary>
    /// <param name="value">值</param>
    public static String ToUnicode(String value)
    {
        var bytes = Encoding.Unicode.GetBytes(value);
        var sb = new StringBuilder();
        for (var i = 0; i < bytes.Length; i += 2)
            sb.AppendFormat("\\u{0}{1}", bytes[i + 1].ToString("x").PadLeft(2, '0'),
                bytes[i].ToString("x").PadLeft(2, '0'));
        return sb.ToString();
    }

    #endregion

    #region ToUnicodeByCn(中文字符串转Unicode)

    /// <summary>中文字符串转Unicode</summary>
    /// <param name="value">值</param>
    public static String ToUnicodeByCn(String value)
    {
        var sb = new StringBuilder();
        if (!String.IsNullOrWhiteSpace(value))
        {
            var chars = value.ToCharArray();
            for (var i = 0; i < value.Length; i++)
            {
                sb.Append(Regex.IsMatch(chars[i].ToString(), "([\u4e00-\u9fa5])")
                    ? ToUnicode(chars[i].ToString())
                    : chars[i].ToString());
            }
        }
        return sb.ToString();
    }

    #endregion

    #region UnicodeToStr(Unicode转字符串)

    /// <summary>Unicode转字符串</summary>
    /// <param name="value">值</param>
    public static String UnicodeToStr(String value) =>
        new Regex(@"\\u([0-9A-F]{4})", RegexOptions.IgnoreCase | RegexOptions.Compiled).Replace(value,
            x => Convert.ToChar(Convert.ToUInt16(x.Result("$1"), 16)).ToString());

    #endregion

    #region PinYin(获取汉字的拼音简码)

    /// <summary>获取汉字的拼音简码，即首字母缩写。范例：中国，返回zg</summary>
    /// <param name="chineseText">汉字文本。范例： 中国</param>
    public static String PinYin(String chineseText)
    {
        if (String.IsNullOrWhiteSpace(chineseText)) return String.Empty;
        var result = new StringBuilder();
        foreach (var text in chineseText)
            result.AppendFormat("{0}", ResolvePinYin(text));
        return result.ToString().ToLower();
    }

    /// <summary>解析单个汉字的拼音简码</summary>
    private static String ResolvePinYin(Char text)
    {
        var charBytes = Encoding.Default.GetBytes(text.ToString());
        if (charBytes[0] < 127) return text.ToString();
        var unicode = (UInt16)(charBytes[0] * 256 + charBytes[1]);
        var pinYin = ResolveByCode(unicode);
        if (!String.IsNullOrWhiteSpace(pinYin)) return pinYin;
        return ResolveByConst(text.ToString());
    }

    /// <summary>使用字符编码方式获取拼音简码</summary>
    private static String ResolveByCode(UInt16 unicode)
    {
        if (unicode >= '\uB0A1' && unicode <= '\uB0C4') return "A";
        if (unicode >= '\uB0C5' && unicode <= '\uB2C0' && unicode != 45464) return "B";
        if (unicode >= '\uB2C1' && unicode <= '\uB4ED') return "C";
        if (unicode >= '\uB4EE' && unicode <= '\uB6E9') return "D";
        if (unicode >= '\uB6EA' && unicode <= '\uB7A1') return "E";
        if (unicode >= '\uB7A2' && unicode <= '\uB8C0') return "F";
        if (unicode >= '\uB8C1' && unicode <= '\uB9FD') return "G";
        if (unicode >= '\uB9FE' && unicode <= '\uBBF6') return "H";
        if (unicode >= '\uBBF7' && unicode <= '\uBFA5') return "J";
        if (unicode >= '\uBFA6' && unicode <= '\uC0AB') return "K";
        if (unicode >= '\uC0AC' && unicode <= '\uC2E7') return "L";
        if (unicode >= '\uC2E8' && unicode <= '\uC4C2') return "M";
        if (unicode >= '\uC4C3' && unicode <= '\uC5B5') return "N";
        if (unicode >= '\uC5B6' && unicode <= '\uC5BD') return "O";
        if (unicode >= '\uC5BE' && unicode <= '\uC6D9') return "P";
        if (unicode >= '\uC6DA' && unicode <= '\uC8BA') return "Q";
        if (unicode >= '\uC8BB' && unicode <= '\uC8F5') return "R";
        if (unicode >= '\uC8F6' && unicode <= '\uCBF9') return "S";
        if (unicode >= '\uCBFA' && unicode <= '\uCDD9') return "T";
        if (unicode >= '\uCDDA' && unicode <= '\uCEF3') return "W";
        if (unicode >= '\uCEF4' && unicode <= '\uD188') return "X";
        if (unicode >= '\uD1B9' && unicode <= '\uD4D0') return "Y";
        if (unicode >= '\uD4D1' && unicode <= '\uD7F9') return "Z";
        return String.Empty;
    }

    /// <summary>通过拼音简码常量获取</summary>
    private static String ResolveByConst(String text)
    {
        var index = Const.ChinesePinYin.IndexOf(text, StringComparison.Ordinal);
        if (index < 0) return String.Empty;
        return Const.ChinesePinYin.Substring(index + 1, 1);
    }

    #endregion

    #region FullPinYin(获取汉字的全拼)

    /// <summary>将汉字转换成拼音(全拼)</summary>
    /// <param name="text">汉字字符串</param>
    public static String FullPinYin(String text)
    {
        var regex = new Regex("^[\u4e00-\u9fa5]$");
        var array = new Byte[2];
        var pyString = "";
        Int32 chrAsc;
        Int32 i1;
        Int32 i2;
        var nowChar = text.ToCharArray();
        for (var j = 0; j < nowChar.Length; j++)
        {
            if (regex.IsMatch(nowChar[j].ToString()))
            {
                array = Encoding.Default.GetBytes(nowChar[j].ToString());
                i1 = (Int16)array[0];
                i2 = (Int16)array[1];
                chrAsc = i1 * 256 + i2 - 65536;
                if (chrAsc > 0 && chrAsc < 160)
                {
                    pyString += nowChar[j];
                }
                else
                {
                    switch (chrAsc)
                    {
                        case -9254: pyString += "Zhen"; break;
                        case -8985: pyString += "Qian"; break;
                        case -5463: pyString += "Jia"; break;
                        case -8274: pyString += "Ge"; break;
                        case -5448: pyString += "Ga"; break;
                        case -5447: pyString += "La"; break;
                        case -4649: pyString += "Chen"; break;
                        case -5436: pyString += "Mao"; break;
                        case -5213: pyString += "Mao"; break;
                        case -3597: pyString += "Die"; break;
                        case -5659: pyString += "Tian"; break;
                        default:
                            for (var i = Const.SpellCode.Length - 1; i >= 0; i--)
                            {
                                if (Const.SpellCode[i] <= chrAsc)
                                {
                                    pyString += Const.SpellLetter[i];
                                    break;
                                }
                            }
                            break;
                    }
                }
            }
            else
            {
                pyString += nowChar[j].ToString();
            }
        }
        return pyString;
    }

    #endregion

    #region FirstLower(首字母小写)

    /// <summary>首字母小写</summary>
    /// <param name="value">值</param>
    public static String FirstLower(String value)
    {
        if (String.IsNullOrWhiteSpace(value)) return String.Empty;
        return $"{value.Substring(0, 1).ToLower()}{value.Substring(1)}";
    }

    #endregion

    #region FirstUpper(首字母大写)

    /// <summary>首字母大写</summary>
    /// <param name="value">值</param>
    public static String FirstUpper(String value)
    {
        if (String.IsNullOrWhiteSpace(value)) return String.Empty;
        return $"{value.Substring(0, 1).ToUpper()}{value.Substring(1)}";
    }

    #endregion

    #region Empty(空字符串)

    /// <summary>空字符串</summary>
    public static String Empty => String.Empty;

    #endregion

    #region Distinct(去除重复)

    /// <summary>去除重复字符串</summary>
    /// <param name="value">值，范例1："5555"，返回"5"，范例2："4545"，返回"45"</param>
    public static String Distinct(String value)
    {
        var array = value.ToCharArray();
        return new String(array.Distinct().ToArray());
    }

    #endregion

    #region Truncate(截断字符串)

    /// <summary>截断字符串</summary>
    /// <param name="text">文本</param>
    /// <param name="length">返回长度</param>
    /// <param name="endCharCount">添加结束符号的个数，默认0，不添加</param>
    /// <param name="endChar">结束符号，默认为省略号</param>
    public static String Truncate(String text, Int32 length, Int32 endCharCount = 0, String endChar = ".")
    {
        if (String.IsNullOrWhiteSpace(text)) return String.Empty;
        if (text.Length < length) return text;
        return $"{text.Substring(0, length)}{GetEndString(endCharCount, endChar)}";
    }

    /// <summary>获取结束字符串</summary>
    private static String GetEndString(Int32 endCharCount, String endChar)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < endCharCount; i++)
            sb.Append(endChar);
        return sb.ToString();
    }

    #endregion

    #region GetLastProperty(获取最后一个属性)

    /// <summary>获取最后一个属性</summary>
    /// <param name="propertyName">属性名，范例，A.B.C,返回"C"</param>
    public static String GetLastProperty(String propertyName)
    {
        if (String.IsNullOrWhiteSpace(propertyName)) return String.Empty;
        var lastIndex = propertyName.LastIndexOf(".", StringComparison.Ordinal) + 1;
        return propertyName.Substring(lastIndex);
    }

    #endregion

    #region GetHideMobile(获取隐藏中间几位后的手机号码)

    /// <summary>获取隐藏中间几位后的手机号码</summary>
    /// <param name="value">手机号码</param>
    public static String GetHideMobile(String value)
    {
        if (String.IsNullOrWhiteSpace(value)) return String.Empty;
        return $"{value.Substring(0, 3)}******{value.Substring(value.Length - 3)}";
    }

    #endregion

    #region GetStringLength(获取字符串的字节数)

    /// <summary>获取字符串的字节数</summary>
    /// <param name="value">值</param>
    public static Int32 GetStringLength(String value)
    {
        if (String.IsNullOrWhiteSpace(value)) return 0;
        var strLength = 0;
        var encoding = new ASCIIEncoding();
        var bytes = encoding.GetBytes(value);
        for (var i = 0; i <= bytes.Length - 1; i++)
        {
            if (bytes[i] == 63) strLength++;
            strLength++;
        }
        return strLength;
    }

    #endregion

    #region ToSnakeCase(将字符串转换为蛇形策略)

    /// <summary>将字符串转换为蛇形策略</summary>
    /// <param name="str">字符串</param>
    public static String ToSnakeCase(String str)
    {
        if (String.IsNullOrEmpty(str)) return str;

        var sb = new StringBuilder();
        var state = SnakeCaseState.Start;
        for (var i = 0; i < str.Length; i++)
        {
            if (str[i] == ' ')
            {
                if (state != SnakeCaseState.Start) state = SnakeCaseState.NewWord;
            }
            else if (Char.IsUpper(str[i]))
            {
                switch (state)
                {
                    case SnakeCaseState.Upper:
                        var hasNext = i + 1 < str.Length;
                        if (i > 0 && hasNext)
                        {
                            var nextChar = str[i + 1];
                            if (!Char.IsUpper(nextChar) && nextChar != '_') sb.Append('_');
                        }
                        break;

                    case SnakeCaseState.Lower:
                    case SnakeCaseState.NewWord:
                        sb.Append('_');
                        break;
                }

                sb.Append(Char.ToLowerInvariant(str[i]));
                state = SnakeCaseState.Upper;
            }
            else if (str[i] == '_')
            {
                sb.Append('_');
                state = SnakeCaseState.Start;
            }
            else
            {
                if (state == SnakeCaseState.NewWord) sb.Append('_');
                sb.Append(str[i]);
                state = SnakeCaseState.Lower;
            }
        }

        return sb.ToString();
    }

    #endregion

    #region ToCamelCase(将字符串转换为骆驼策略)

    /// <summary>将字符串转换为骆驼策略</summary>
    /// <param name="str">字符串</param>
    public static String ToCamelCase(String str)
    {
        if (String.IsNullOrEmpty(str) || !Char.IsUpper(str[0])) return str;
        var chars = str.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (i == 1 && !Char.IsUpper(chars[i])) break;
            var hasNext = i + 1 < chars.Length;
            if (i > 0 && hasNext && !Char.IsUpper(chars[i + 1]))
            {
                if (Char.IsSeparator(chars[i + 1])) chars[i] = Char.ToLowerInvariant(chars[i]);
                break;
            }
            chars[i] = Char.ToLowerInvariant(chars[i]);
        }
        return new String(chars);
    }

    #endregion

    #region GenerateNonceStr(生成随机字符串)

    /// <summary>生成随机字符串</summary>
    public static String GenerateNonceStr() => Guid.NewGuid().ToString("N");

    #endregion

    #region SplitWordGroup(分隔词组)

    /// <summary>分隔词组</summary>
    /// <param name="value">值</param>
    /// <param name="separator">分隔符。默认使用"-"分隔</param>
    public static String SplitWordGroup(String value, Char separator = '-')
    {
        var pattern = @"([A-Z])(?=[a-z])|(?<=[a-z])([A-Z]|[0-9]+)";
        return String.IsNullOrWhiteSpace(value) ? String.Empty : Regex.Replace(value, pattern, $"{separator}$1$2").TrimStart(separator).ToLower();
    }

    #endregion
}

/// <summary>字符串策略</summary>
public enum StringCase
{
    /// <summary>蛇形策略</summary>
    Snake,

    /// <summary>骆驼策略</summary>
    Camel,

    /// <summary>不执行策略</summary>
    None,
}

/// <summary>蛇形策略状态</summary>
internal enum SnakeCaseState
{
    /// <summary>开头</summary>
    Start,

    /// <summary>小写</summary>
    Lower,

    /// <summary>大写</summary>
    Upper,

    /// <summary>单词</summary>
    NewWord
}
