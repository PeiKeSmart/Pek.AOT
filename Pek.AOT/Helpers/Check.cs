using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Pek.Extension;

namespace Pek.Helpers;

/// <summary>参数验证助手类。AOT 安全版，使用工厂模式替代反射创建异常</summary>
[DebuggerStepThrough]
public static class Check
{
    #region 私有辅助
    private static void Require<TException>(Boolean assertion, String message, Func<String, TException> factory) where TException : Exception
    {
        if (assertion) return;
        if (String.IsNullOrWhiteSpace(message))
            throw new ArgumentNullException(nameof(message));
        throw factory(message);
    }
    #endregion

    #region Required(断言)
    public static void Required<T>(T value, Func<T, Boolean> assertionFunc, String message)
    {
        if (assertionFunc == null)
            throw new ArgumentNullException(nameof(assertionFunc));
        Require(assertionFunc(value), message, m => new Exception(m));
    }

    public static void Required<T, TException>(T value, Func<T, Boolean> assertionFunc, String message, Func<String, TException> factory)
        where TException : Exception
    {
        if (assertionFunc == null)
            throw new ArgumentNullException(nameof(assertionFunc));
        Require(assertionFunc(value), message, factory);
    }
    #endregion

    #region NotNull(不可空检查)
    public static T NotNull<T>(T value, [NotNull] String parameterName)
    {
        if (value == null) throw new ArgumentNullException(parameterName);
        return value;
    }

    public static T NotNull<T>(T value, [NotNull] String parameterName, String message)
    {
        if (value == null) throw new ArgumentNullException(parameterName, message);
        return value;
    }

    public static String NotNull(String value, [NotNull] String parameterName, Int32 maxLength = Int32.MaxValue, Int32 minLength = 0)
    {
        if (value == null) throw new ArgumentException($"{parameterName}不能为空!", parameterName);
        if (value.Length > maxLength) throw new ArgumentException($"{parameterName}长度必须等于或小于{maxLength}!", parameterName);
        if (minLength > 0 && value.Length < minLength) throw new ArgumentException($"{parameterName}长度必须等于或大于{minLength}!", parameterName);
        return value;
    }

    public static String NotNullOrWhiteSpace(String value, [NotNull] String parameterName, Int32 maxLength = Int32.MaxValue, Int32 minLength = 0)
    {
        if (value.IsNullOrWhiteSpace()) throw new ArgumentException($"{parameterName}不能为空或空白!", parameterName);
        if (value.Length > maxLength) throw new ArgumentException($"{parameterName}长度必须等于或小于{maxLength}!", parameterName);
        if (minLength > 0 && value.Length < minLength) throw new ArgumentException($"{parameterName}长度必须等于或大于{minLength}!", parameterName);
        return value;
    }

    public static String NotNullOrEmpty(String value, [NotNull] String parameterName, Int32 maxLength = Int32.MaxValue, Int32 minLength = 0)
    {
        if (value.IsNullOrEmpty()) throw new ArgumentException($"{parameterName}不能为null或空!", parameterName);
        if (value.Length > maxLength) throw new ArgumentException($"{parameterName}长度必须等于或小于{maxLength}!", parameterName);
        if (minLength > 0 && value.Length < minLength) throw new ArgumentException($"{parameterName}长度必须等于或大于{minLength}!", parameterName);
        return value;
    }

    public static ICollection<T> NotNullOrEmpty<T>(ICollection<T> value, [NotNull] String parameterName)
    {
        if (value == null || value.Count == 0) throw new ArgumentException($"{parameterName}不能为null或空!", parameterName);
        return value;
    }

    public static void NotNullOrEmpty(String value, String paramName)
    {
        NotNull(value, paramName);
        Require(!String.IsNullOrEmpty(value), String.Format("参数 {0} 不能为空引用或空字符串。", paramName), m => new ArgumentException(m));
    }

    public static void NotEmpty(Guid value, String paramName)
    {
        Require(value != Guid.Empty, String.Format("参数 {0} 的值不能为Guid.Empty", paramName), m => new ArgumentException(m));
    }

    public static void NotNullOrEmpty<T>(IEnumerable<T> collection, String paramName)
    {
        NotNull(collection, paramName);
        Require(collection.Any(), String.Format("参数 {0} 不能为空引用或空集合。", paramName), m => new ArgumentException(m));
    }

    public static void NotNullOrEmpty<T>(IDictionary<String, T> dictionary, String paramName)
    {
        NotNull(dictionary, paramName);
        Require(dictionary.Any(), $"参数 {paramName} 不能为空引用或空集合。", m => new ArgumentException(m));
    }

    public static Type AssignableTo<TBaseType>(Type type, [NotNull] String parameterName)
    {
        NotNull(type, parameterName);
        if (!type.IsAssignableTo(typeof(TBaseType))) throw new ArgumentException($"{parameterName} (type of {type.FullName}) 应分配给 {typeof(TBaseType).FullName}!");
        return type;
    }
    #endregion

    #region Between(范围检查)
    public static String Length(String value, [NotNull] String parameterName, Int32 maxLength, Int32 minLength = 0)
    {
        if (minLength > 0)
        {
            if (String.IsNullOrEmpty(value)) throw new ArgumentException($"{parameterName}不能为null或空!", parameterName);
            if (value.Length < minLength) throw new ArgumentException($"{parameterName}长度必须等于或大于{minLength}!", parameterName);
        }
        if (value != null && value.Length > maxLength) throw new ArgumentException($"{parameterName}长度必须等于或小于{maxLength}!", parameterName);
        return value!;
    }

    public static Int16 Positive(Int16 value, [NotNull] String parameterName)
    {
        if (value == 0) throw new ArgumentException($"{parameterName}等于零");
        if (value < 0) throw new ArgumentException($"{parameterName}小于零");
        return value;
    }

    public static Int32 Positive(Int32 value, [NotNull] String parameterName)
    {
        if (value == 0) throw new ArgumentException($"{parameterName}等于零");
        if (value < 0) throw new ArgumentException($"{parameterName}小于零");
        return value;
    }

    public static Int64 Positive(Int64 value, [NotNull] String parameterName)
    {
        if (value == 0) throw new ArgumentException($"{parameterName}等于零");
        if (value < 0) throw new ArgumentException($"{parameterName}小于零");
        return value;
    }

    public static Single Positive(Single value, [NotNull] String parameterName)
    {
        if (value == 0) throw new ArgumentException($"{parameterName}等于零");
        if (value < 0) throw new ArgumentException($"{parameterName}小于零");
        return value;
    }

    public static Double Positive(Double value, [NotNull] String parameterName)
    {
        if (value == 0) throw new ArgumentException($"{parameterName}等于零");
        if (value < 0) throw new ArgumentException($"{parameterName}小于零");
        return value;
    }

    public static Decimal Positive(Decimal value, [NotNull] String parameterName)
    {
        if (value == 0) throw new ArgumentException($"{parameterName}等于零");
        if (value < 0) throw new ArgumentException($"{parameterName}小于零");
        return value;
    }

    public static Int16 Range(Int16 value, [NotNull] String parameterName, Int16 minimumValue, Int16 maximumValue = Int16.MaxValue)
    {
        if (value < minimumValue || value > maximumValue) throw new ArgumentException($"{parameterName}超出范围最小值：{minimumValue}-最大值：{maximumValue}");
        return value;
    }

    public static Int32 Range(Int32 value, [NotNull] String parameterName, Int32 minimumValue, Int32 maximumValue = Int32.MaxValue)
    {
        if (value < minimumValue || value > maximumValue) throw new ArgumentException($"{parameterName}超出范围最小值：{minimumValue}-最大值：{maximumValue}");
        return value;
    }

    public static Int64 Range(Int64 value, [NotNull] String parameterName, Int64 minimumValue, Int64 maximumValue = Int64.MaxValue)
    {
        if (value < minimumValue || value > maximumValue) throw new ArgumentException($"{parameterName}超出范围最小值：{minimumValue}-最大值：{maximumValue}");
        return value;
    }

    public static Single Range(Single value, [NotNull] String parameterName, Single minimumValue, Single maximumValue = Single.MaxValue)
    {
        if (value < minimumValue || value > maximumValue) throw new ArgumentException($"{parameterName}超出范围最小值：{minimumValue}-最大值：{maximumValue}");
        return value;
    }

    public static Double Range(Double value, [NotNull] String parameterName, Double minimumValue, Double maximumValue = Double.MaxValue)
    {
        if (value < minimumValue || value > maximumValue) throw new ArgumentException($"{parameterName}超出范围最小值：{minimumValue}-最大值：{maximumValue}");
        return value;
    }

    public static Decimal Range(Decimal value, [NotNull] String parameterName, Decimal minimumValue, Decimal maximumValue = Decimal.MaxValue)
    {
        if (value < minimumValue || value > maximumValue) throw new ArgumentException($"{parameterName}超出范围最小值：{minimumValue}-最大值：{maximumValue}");
        return value;
    }

    public static T NotDefaultOrNull<T>(T? value, [NotNull] String parameterName) where T : struct
    {
        if (value == null) throw new ArgumentException($"{parameterName}空值!", parameterName);
        if (value.Value.Equals(default(T))) throw new ArgumentException($"{parameterName}具有默认值!", parameterName);
        return value.Value;
    }

    public static void LessThan<T>(T value, String paramName, T target, Boolean canEqual = false) where T : IComparable<T>
    {
        var flag = canEqual ? value.CompareTo(target) <= 0 : value.CompareTo(target) < 0;
        var format = canEqual ? "参数 {0} 的值必须小于或等于 {1}。" : "参数 {0} 的值必须小于 {1}。";
        Require(flag, String.Format(format, paramName, target), m => new ArgumentOutOfRangeException(m));
    }

    public static void GreaterThan<T>(T value, String paramName, T target, Boolean canEqual = false) where T : IComparable<T>
    {
        var flag = canEqual ? value.CompareTo(target) >= 0 : value.CompareTo(target) > 0;
        var format = canEqual ? "参数 {0} 的值必须大于或等于 {1}。" : "参数 {0} 的值必须大于 {1}。";
        Require(flag, String.Format(format, paramName, target), m => new ArgumentOutOfRangeException(m));
    }

    public static void Between<T>(T value, String paramName, T start, T end, Boolean startEqual = false, Boolean endEqual = false) where T : IComparable<T>
    {
        var flag = startEqual ? value.CompareTo(start) >= 0 : value.CompareTo(start) > 0;
        var message = startEqual
            ? String.Format("参数 {0} 的值必须在 {1} 与 {2} 之间。", paramName, start, end)
            : String.Format("参数 {0} 的值必须在 {1} 与 {2} 之间，且不能等于 {3}。", paramName, start, end, start);
        Require(flag, message, m => new ArgumentOutOfRangeException(m));

        flag = endEqual ? value.CompareTo(end) <= 0 : value.CompareTo(end) < 0;
        message = endEqual
            ? String.Format("参数 {0} 的值必须在 {1} 与 {2} 之间。", paramName, start, end)
            : String.Format("参数 {0} 的值必须在 {1} 与 {2} 之间，且不能等于 {3}。", paramName, start, end, end);
        Require(flag, message, m => new ArgumentOutOfRangeException(m));
    }

    public static void NotNegativeOrZero(TimeSpan timeSpan, String paramName)
    {
        Require(timeSpan > TimeSpan.Zero, paramName, m => new ArgumentOutOfRangeException(m));
    }
    #endregion

    #region IO(文件检查)
    public static void DirectoryExists(String directory, String? paramName = null)
    {
        NotNull(directory, paramName ?? nameof(directory));
        Require(Directory.Exists(directory), String.Format("指定的目录路径 {0} 不存在。", directory), m => new DirectoryNotFoundException(m));
    }

    public static void FileExists(String fileName, String? paramName = null)
    {
        NotNull(fileName, paramName ?? nameof(fileName));
        Require(File.Exists(fileName), String.Format("指定的文件路径 {0} 不存在。", fileName), m => new FileNotFoundException(m));
    }
    #endregion
}
