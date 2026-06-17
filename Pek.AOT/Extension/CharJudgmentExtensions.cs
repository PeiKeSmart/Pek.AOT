namespace Pek;

/// <summary>
/// 基础类型扩展 - Char判断
/// </summary>
public static class CharJudgmentExtensions
{
    /// <summary>
    /// 判断当前字符是否在目标字符数组中
    /// </summary>
    /// <param name="this">字符</param>
    /// <param name="values">字符数组</param>
    public static Boolean In(this Char @this, params Char[] values) => Array.IndexOf(values, @this) != -1;

    /// <summary>
    /// 判断当前字符是否不在目标字符数组中
    /// </summary>
    /// <param name="this">字符</param>
    /// <param name="values">字符数组</param>
    public static Boolean NotIn(this Char @this, params Char[] values) => Array.IndexOf(values, @this) == -1;

    /// <summary>
    /// 判断当前字符是否空格字符
    /// </summary>
    /// <param name="c">字符</param>
    public static Boolean IsWhiteSpace(this Char c) => Char.IsWhiteSpace(c);

    /// <summary>
    /// 判断当前字符是否控制字符
    /// </summary>
    /// <param name="c">字符</param>
    public static Boolean IsControl(this Char c) => Char.IsControl(c);

    /// <summary>
    /// 判断当前字符是否十进制数字字符
    /// </summary>
    /// <param name="c">字符</param>
    public static Boolean IsDigit(this Char c) => Char.IsDigit(c);

    /// <summary>
    /// 判断当前字符是否英文字符
    /// </summary>
    /// <param name="c">字符</param>
    public static Boolean IsLetter(this Char c) => Char.IsLetter(c);

    /// <summary>
    /// 判断当前字符是否英文或十进制数字字符
    /// </summary>
    /// <param name="c">字符</param>
    public static Boolean IsLetterOrDigit(this Char c) => Char.IsLetterOrDigit(c);

    /// <summary>
    /// 判断当前字符是否小写英文字符
    /// </summary>
    /// <param name="c">字符</param>
    public static Boolean IsLower(this Char c) => Char.IsLower(c);

    /// <summary>
    /// 判断当前字符是否数字字符
    /// </summary>
    /// <param name="c">字符</param>
    public static Boolean IsNumber(this Char c) => Char.IsNumber(c);

    /// <summary>
    /// 判断当前字符是否标点符号
    /// </summary>
    /// <param name="c">字符</param>
    public static Boolean IsPunctuation(this Char c) => Char.IsPunctuation(c);

    /// <summary>
    /// 判断当前字符是否分隔符
    /// </summary>
    /// <param name="c">字符</param>
    public static Boolean IsSeparator(this Char c) => Char.IsSeparator(c);

    /// <summary>
    /// 判断当前字符是否符号字符
    /// </summary>
    /// <param name="c">字符</param>
    public static Boolean IsSymbol(this Char c) => Char.IsSymbol(c);
}
