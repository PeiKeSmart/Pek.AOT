using System.Diagnostics.CodeAnalysis;

using Pek.Helpers;

namespace Pek;

/// <summary>参数检查扩展方法。AOT 安全版，转发到 Check 助手类</summary>
public static class CheckExtensions
{
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
}