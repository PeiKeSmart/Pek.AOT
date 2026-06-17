using System.Globalization;

namespace Pek;

/// <summary>
/// 系统扩展 - 类型转换扩展（上游 Pek.Common DHExtensions.Convert 迁移，BCL 等价替换）
/// </summary>
/// <remarks>
/// AOT 兼容说明：
/// - 上游依赖 Pek.Helpers.Conv 类的 ToDGXxx 系列方法，AOT 版使用 BCL 等价替换
/// - ToSnakeCase / ToCamelCase 依赖 Pek.Helpers.Str，AOT 版内联实现
/// - 所有方法均使用 String/Int32/Boolean 等 .NET 正式类型名
/// </remarks>
public static partial class DHExtensions
{
    #region ToDate(转换为日期)

    /// <summary>转换为日期</summary>
    /// <param name="obj">数据</param>
    public static DateTime ToDate(this String obj) => DateTime.Parse(obj);

    /// <summary>转换为可空日期</summary>
    /// <param name="obj">数据</param>
    public static DateTime? ToDateOrNull(this String obj) => DateTime.TryParse(obj, out var result) ? result : null;

    #endregion

    #region ToBool(转换为bool)

    /// <summary>转换为bool</summary>
    /// <param name="obj">数据</param>
    public static Boolean ToBool(this String obj) => Boolean.Parse(obj);

    /// <summary>转换为可空bool</summary>
    /// <param name="obj">数据</param>
    public static Boolean? ToBoolOrNull(this String obj) => Boolean.TryParse(obj, out var result) ? result : null;

    #endregion

    #region ToInt(转换为int)

    /// <summary>转换为int</summary>
    /// <param name="obj">数据</param>
    public static Int32 ToInt(this String obj) => Int32.Parse(obj);

    /// <summary>转换为可空int</summary>
    /// <param name="obj">数据</param>
    public static Int32? ToIntOrNull(this String obj) => Int32.TryParse(obj, out var result) ? result : null;

    #endregion

    #region ToLong(转换为long)

    /// <summary>转换为long</summary>
    /// <param name="obj">数据</param>
    public static Int64 ToLong(this String obj) => Int64.Parse(obj);

    /// <summary>转换为可空long</summary>
    /// <param name="obj">数据</param>
    public static Int64? ToLongOrNull(this String obj) => Int64.TryParse(obj, out var result) ? result : null;

    #endregion

    #region ToDouble(转换为double)

    /// <summary>转换为double</summary>
    /// <param name="obj">数据</param>
    public static Double ToDouble(this String obj) => Double.Parse(obj);

    /// <summary>转换为double（指定小数位数）</summary>
    /// <param name="obj">数据</param>
    /// <param name="digits">小数位数</param>
    public static Double ToDouble(this String obj, Int32? digits = null)
    {
        var value = Double.Parse(obj);
        if (digits.HasValue)
            value = Math.Round(value, digits.Value);
        return value;
    }

    /// <summary>转换为可空double</summary>
    /// <param name="obj">数据</param>
    public static Double? ToDoubleOrNull(this String obj) => Double.TryParse(obj, out var result) ? result : null;

    #endregion

    #region ToDecimal(转换为decimal)

    /// <summary>转换为decimal</summary>
    /// <param name="obj">数据</param>
    public static Decimal ToDecimal(this String obj) => Decimal.Parse(obj);

    /// <summary>转换为可空decimal</summary>
    /// <param name="obj">数据</param>
    public static Decimal? ToDecimalOrNull(this String obj) => Decimal.TryParse(obj, out var result) ? result : null;

    #endregion

    #region ToGuid(转换为Guid)

    /// <summary>转化为Guid</summary>
    /// <param name="obj">数据</param>
    public static Guid ToGuid(this String obj) => Guid.Parse(obj);

    /// <summary>转换为可空Guid</summary>
    /// <param name="obj">数据</param>
    public static Guid? ToGuidOrNull(this String obj) => Guid.TryParse(obj, out var result) ? result : null;

    /// <summary>转换为Guid集合，范例："83B0233C-A24F-49FD-8083-1337209EBC9A,EAB523C6-2FE7-47BE-89D5-C6D440C3033A"</summary>
    /// <param name="obj">数据</param>
    public static List<Guid> ToGuidList(this String obj)
    {
        if (String.IsNullOrEmpty(obj)) return [];

        return obj.Split(',')
            .Select(s => s.Trim())
            .Where(s => !String.IsNullOrEmpty(s))
            .Select(s => Guid.Parse(s))
            .ToList();
    }

    /// <summary>转换为Guid集合</summary>
    /// <param name="obj">字符串集合</param>
    public static List<Guid> ToGuidList(this IList<String> obj)
        => obj == null ? [] : obj.Select(t => t.ToGuid()).ToList();

    #endregion

    #region ToSnakeCase(将字符串转换为蛇形策略)

    /// <summary>将字符串转换为蛇形策略（snake_case）</summary>
    /// <param name="str">字符串</param>
    public static String ToSnakeCase(this String str)
    {
        if (String.IsNullOrEmpty(str)) return str;

        var sb = new System.Text.StringBuilder();
        var len = str.Length;
        for (var i = 0; i < len; i++)
        {
            var c = str[i];
            if (Char.IsUpper(c))
            {
                // 前一个字符是小写字母或（非首字符且下一个字符是小写）时添加下划线
                if (i > 0 && (Char.IsLower(str[i - 1]) || (i + 1 < len && Char.IsLower(str[i + 1]))))
                    sb.Append('_');
                sb.Append(Char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    #endregion

    #region ToCamelCase(将字符串转换为骆驼策略)

    /// <summary>将字符串转换为骆驼策略（camelCase）</summary>
    /// <param name="str">字符串</param>
    public static String ToCamelCase(this String str)
    {
        if (String.IsNullOrEmpty(str)) return str;

        return Char.ToLowerInvariant(str[0]) + str.Substring(1);
    }

    #endregion
}
