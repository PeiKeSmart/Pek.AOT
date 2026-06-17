using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

using Pek.Helpers;

namespace Pek;

/// <summary>参数检查扩展方法。AOT 安全版，转发到 Check 助手类</summary>
public static class CheckExtensions
{
    #region IsNull(是否为空)
    /// <summary>判断目标对象是否为空</summary>
    /// <param name="target">目标对象</param>
    public static Boolean IsNull(this Object target) => target.IsNull<Object>();

    /// <summary>判断目标对象是否为空</summary>
    /// <typeparam name="T">目标对象类型</typeparam>
    /// <param name="target">目标对象</param>
    public static Boolean IsNull<T>(this T target) => ReferenceEquals(target, null);
    #endregion

    #region Required(断言)
    /// <summary>验证指定值的断言表达式是否为真，不为真抛出 Exception 异常</summary>
    public static void Required<T>(this T value, Func<T, Boolean> assertionFunc, String message)
        => Check.Required(value, assertionFunc, message);

    /// <summary>验证指定值的断言表达式是否为真，不为真通过工厂创建指定异常并抛出。AOT 安全</summary>
    public static void Required<T, TException>(this T value, Func<T, Boolean> assertionFunc, String message, Func<String, TException> factory)
        where TException : Exception
        => Check.Required(value, assertionFunc, message, factory);
    #endregion

    #region CheckNotNull(不可空检查)
    /// <summary>检查参数不能为空引用，否则抛出 ArgumentNullException 异常</summary>
    public static void CheckNotNull<T>(this T value, String paramName) where T : class
        => Check.NotNull(value, paramName);

    /// <summary>检查字符串不能为空引用或空字符串，否则抛出异常</summary>
    public static void CheckNotNullOrEmpty(this String value, String paramName)
        => Check.NotNullOrEmpty(value, paramName);

    /// <summary>检查 Guid 值不能为 Guid.Empty，否则抛出异常</summary>
    public static void CheckNotEmpty(this Guid value, String paramName)
        => Check.NotEmpty(value, paramName);

    /// <summary>检查集合不能为空引用或空集合，否则抛出异常</summary>
    public static void CheckNotNullOrEmpty<T>(this IEnumerable<T> collection, String paramName)
        => Check.NotNullOrEmpty(collection, paramName);
    #endregion

    #region CheckBetween(范围检查)
    /// <summary>检查参数必须小于[或可等于]指定值，否则抛出 ArgumentOutOfRangeException 异常</summary>
    public static void CheckLessThan<T>(this T value, String paramName, T target, Boolean canEqual = false) where T : IComparable<T>
        => Check.LessThan(value, paramName, target, canEqual);

    /// <summary>检查参数必须大于[或可等于]指定值，否则抛出 ArgumentOutOfRangeException 异常</summary>
    public static void CheckGreaterThan<T>(this T value, String paramName, T target, Boolean canEqual = false) where T : IComparable<T>
        => Check.GreaterThan(value, paramName, target, canEqual);

    /// <summary>检查参数必须在指定范围之间，否则抛出 ArgumentOutOfRangeException 异常</summary>
    public static void CheckBetween<T>(this T value, String paramName, T start, T end, Boolean startEqual = false, Boolean endEqual = false) where T : IComparable<T>
        => Check.Between(value, paramName, start, end, startEqual, endEqual);
    #endregion

    #region CheckIO(文件检查)
    /// <summary>检查指定路径的文件夹必须存在，否则抛出 DirectoryNotFoundException 异常</summary>
    public static void CheckDirectoryExists(this String directory, String? paramName = null)
        => Check.DirectoryExists(directory, paramName);

    /// <summary>检查指定路径的文件必须存在，否则抛出 FileNotFoundException 异常</summary>
    public static void CheckFileExists(this String fileName, String? paramName = null)
        => Check.FileExists(fileName, paramName);
    #endregion

    #region 验证通用参数
    /// <summary>快速验证一个字符串是否符合指定的正则表达式</summary>
    /// <param name="_value">需验证的字符串</param>
    /// <param name="_express">正则表达式</param>
    public static Boolean QuickValidate(this Object _value, String _express) => QuickValidate(_value, _express, true);

    /// <summary>快速验证一个字符串是否符合指定的正则表达式</summary>
    /// <param name="_value">需验证的字符串</param>
    /// <param name="_express">正则表达式</param>
    /// <param name="_bool">True 区分大小写，False 不区分大小写</param>
    public static Boolean QuickValidate(this Object _value, String _express, Boolean _bool)
    {
        if (ObjIsNull(_value)) return false;
        if (_bool)
            return Regex.IsMatch(_value.ToString() ?? String.Empty, _express);

        return Regex.IsMatch(_value.ToString() ?? String.Empty, _express, RegexOptions.IgnoreCase);
    }
    #endregion

    #region CheckNull(检查对象是否为null)
    /// <summary>检查对象是否为 null，为 null 则抛出 ArgumentNullException 异常</summary>
    public static void CheckNull(this Object obj, String parameterName)
    {
        if (obj == null) throw new ArgumentNullException(parameterName);
    }
    #endregion

    #region IsEmpty(是否为空)
    /// <summary>判断字符串是否为空、null 或空白字符串</summary>
    public static Boolean IsEmpty(this String value) => String.IsNullOrWhiteSpace(value);

    /// <summary>判断 Guid 是否为 Guid.Empty</summary>
    public static Boolean IsEmpty(this Guid value) => value == Guid.Empty;

    /// <summary>判断 Guid? 是否为 null 或 Guid.Empty</summary>
    public static Boolean IsEmpty(this Guid? value) => value == null || IsEmpty(value.Value);

    /// <summary>判断 StringBuilder 是否为空</summary>
    public static Boolean IsEmpty(this StringBuilder sb) => sb == null || sb.Length == 0 || sb.ToString().IsEmpty();

    /// <summary>判断迭代集合是否为空</summary>
    public static Boolean IsEmpty<T>(this IEnumerable<T> list) => null == list || !list.Any();

    /// <summary>判断字典是否为空</summary>
    public static Boolean IsEmpty<TKey, TValue>(this IDictionary<TKey, TValue> dictionary) => null == dictionary || dictionary.Count == 0;

    /// <summary>判断字典是否为空（非泛型）</summary>
    public static Boolean IsEmpty(this IDictionary dictionary) => null == dictionary || dictionary.Count == 0;
    #endregion

    #region 判断对象是否为空
    /// <summary>字符串是否为 Null 或为空</summary>
    public static Boolean StrIsNullOrEmpty(this String str)
    {
        if (str == null || str.Trim() == String.Empty) return true;
        return false;
    }

    /// <summary>判断对象是否为空（null、DBNull、空字符串、空白）</summary>
    public static Boolean ObjIsNull(this Object Value) => Value == null || Value == DBNull.Value || Value.ToString() == String.Empty || Value.ToString()?.Trim() == "";
    #endregion

    #region 判断是否是IP地址格式
    /// <summary>判断一个字符串是否为 IP 地址</summary>
    public static Boolean IsIPAddress(this String _value) => QuickValidate(_value, @"^(((2[0-4]{1}[0-9]{1})|(25[0-5]{1}))|(1[0-9]{2})|([1-9]{1}[0-9]{1})|([0-9]{1})).(((2[0-4]{1}[0-9]{1})|(25[0-5]{1}))|(1[0-9]{2})|([1-9]{1}[0-9]{1})|([0-9]{1})).(((2[0-4]{1}[0-9]{1})|(25[0-5]{1}))|(1[0-9]{2})|([1-9]{1}[0-9]{1})|([0-9]{1})).(((2[0-4]{1}[0-9]{1})|(25[0-5]{1}))|(1[0-9]{2})|([1-9]{1}[0-9]{1})|([0-9]{1}))$", false);

    /// <summary>是否为 IP</summary>
    public static Boolean IsIP(this String ip) => Regex.IsMatch(ip, @"^((2[0-4]\d|25[0-5]|[01]?\d\d?)\.){3}(2[0-4]\d|25[0-5]|[01]?\d\d?)$");

    /// <summary>是否为 IP 段</summary>
    public static Boolean IsIPSect(this String ip) => Regex.IsMatch(ip, @"^((2[0-4]\d|25[0-5]|[01]?\d\d?)\.){2}((2[0-4]\d|25[0-5]|[01]?\d\d?|\*)\.)(2[0-4]\d|25[0-5]|[01]?\d\d?|\*)$");
    #endregion

    #region NotEmpty(是否非空)
    /// <summary>判断字符串是否非空</summary>
    public static Boolean NotEmpty(this String value) => !String.IsNullOrWhiteSpace(value);

    /// <summary>判断 Guid 是否非空</summary>
    public static Boolean NotEmpty(this Guid value) => value != Guid.Empty;

    /// <summary>判断 Guid? 是否非空</summary>
    public static Boolean NotEmpty(this Guid? value) => value != null && value != Guid.Empty;

    /// <summary>判断 StringBuilder 是否非空</summary>
    public static Boolean NotEmpty(this StringBuilder sb) => sb != null && sb.Length != 0 && sb.ToString().NotEmpty();

    /// <summary>判断迭代集合是否非空</summary>
    public static Boolean NotEmpty<T>(this IEnumerable<T> enumerable)
    {
        if (enumerable == null) return false;
        if (enumerable.Any()) return true;
        return false;
    }
    #endregion

    #region 检测是否有Sql危险字符
    /// <summary>检测是否有 Sql 危险字符</summary>
    public static Boolean IsSafeSqlString(this String str) => !QuickValidate(str, @"[-|;|,|\/|\(|\)|\[|\]|\}|\{|%|@|\*|!|\']");
    #endregion

    #region 判断对象是否为布尔值
    /// <summary>判断对象是否为布尔值</summary>
    public static Boolean IsBool(this Object Value)
    {
        var array = new String[] { "true", "false", "yes", "no", "1", "0" };
        return Array.IndexOf(array, (Value?.ToString() ?? String.Empty).ToLower()) >= 0;
    }
    #endregion

    #region 数字判断
    /// <summary>判断对象是否为整型数值（正整数和0）</summary>
    public static Boolean IsInt(this Object Value) => QuickValidate(Value, "[0-9]*$");

    /// <summary>判断字符串是否为整型数值</summary>
    public static Boolean IsInt(this String str) => Regex.IsMatch(str, @"^[0-9]*$");

    /// <summary>判断一个字符串是否为 Int（含正负）</summary>
    public static Boolean IsInt1(this String _value)
    {
        var regex = new Regex(@"^(-){0,1}\d+$");
        if (regex.Match(_value).Success)
        {
            if (Int64.Parse(_value) > 0x7fffffffL || Int64.Parse(_value) < -2147483648L) return false;
            return true;
        }
        return false;
    }

    /// <summary>判断字符串是否为数值（含小数）</summary>
    public static Boolean IsNumeric(this String expression)
    {
        if (!StrIsNullOrEmpty(expression))
        {
            var str = expression;
            if (str.Length > 0 && str.Length <= 11 && Regex.IsMatch(str, @"^[-]?[0-9]*[.]?[0-9]*$"))
            {
                if (str.Length < 10 || (str.Length == 10 && str[0] == '1') || (str.Length == 11 && str[0] == '-' && str[1] == '1'))
                    return true;
            }
        }
        return false;
    }

    /// <summary>判断对象是否为数值</summary>
    public static Boolean IsNumeric(this Object expression)
    {
        if (!ObjIsNull(expression)) return IsNumeric(expression.ToString()!);
        return false;
    }

    /// <summary>是否是浮点数</summary>
    public static Boolean IsDecimal(this String _value) => QuickValidate(_value, @"^[0-9]+[.]?[0-9]+$");

    /// <summary>是否是浮点数（可带正负号）</summary>
    public static Boolean IsDecimalSign(this String _value) => QuickValidate(_value, @"^[+-]?[0-9]+[.]?[0-9]+$");

    /// <summary>判断对象是否为浮点型数值</summary>
    public static Boolean IsFloat(this Object Value) => QuickValidate(Value, "^(-?[0-9]*[.]*[0-9]*)$");

    /// <summary>是否为 Double 类型</summary>
    public static Boolean IsDouble(this Object expression)
    {
        if (!ObjIsNull(expression)) return expression.QuickValidate(@"^[+-]?\d+(\.\d+)?([eE][+-]?\d+)?$");
        return false;
    }
    #endregion

    #region 邮件地址
    /// <summary>检测是否符合 email 格式</summary>
    public static Boolean IsEmail(this String strEmail) => Regex.IsMatch(strEmail, @"^([a-zA-Z0-9_\-\.]+)@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([a-zA-Z0-9\-]+\.)+))([a-zA-Z]{2,4}|[0-9]{1,3})(\]?)$");

    /// <summary>检测是否符合 email 格式</summary>
    public static Boolean IsValidEmail(this String strEmail) => Regex.IsMatch(strEmail, @"^[\w\.]+([-]\w+)*@[A-Za-z0-9-_]+[\.][A-Za-z0-9-_]");

    /// <summary>检测是否符合域名 email 格式</summary>
    public static Boolean IsValidDoEmail(this String strEmail) => Regex.IsMatch(strEmail, @"^@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([\w-]+\.)+))([a-zA-Z]{2,4}|[0-9]{1,3})(\]?)$");
    #endregion

    #region 字符串是否可以转化为日期
    /// <summary>检查一个字符串是否可以转化为日期</summary>
    public static Boolean IsDateTime(this String _value)
    {
        return DateTime.TryParse(_value, out _);
    }

    /// <summary>判断是否是时间格式</summary>
    public static Boolean IsTime(this String timeval) => QuickValidate(timeval, @"^((([0-1]?[0-9])|(2[0-3])):([0-5]?[0-9])(:[0-5]?[0-9])?)$");

    /// <summary>判断字符串是否是 yyyy-MM-dd 格式</summary>
    public static Boolean IsDateString(this String str) => QuickValidate(str, @"(\d{4})-(\d{1,2})-(\d{1,2})");
    #endregion

    #region IsZeroOrMinus(是否为0或负数)
    public static Boolean IsZeroOrMinus(this Int16 value) => value <= 0;
    public static Boolean IsZeroOrMinus(this Int32 value) => value <= 0;
    public static Boolean IsZeroOrMinus(this Int64 value) => value <= 0;
    public static Boolean IsZeroOrMinus(this Single value) => value <= 0;
    public static Boolean IsZeroOrMinus(this Double value) => value <= 0;
    public static Boolean IsZeroOrMinus(this Decimal value) => value <= 0;
    #endregion

    #region IsPercentage(是否为百分数)
    public static Boolean IsPercentage(this Single value) => value > 0 && value <= 1;
    public static Boolean IsPercentage(this Double value) => value > 0 && value <= 1;
    public static Boolean IsPercentage(this Decimal value) => value > 0 && value <= 1;
    #endregion

    #region IsZeroOrPercentage(是否为0或百分数)
    public static Boolean IsZeroOrPercentage(this Single value) => value.IsPercentage() || value.Equals(0f);
    public static Boolean IsZeroOrPercentage(this Double value) => value.IsPercentage() || value.Equals(0d);
    public static Boolean IsZeroOrPercentage(this Decimal value) => value.IsPercentage() || value.Equals(0m);
    #endregion
}