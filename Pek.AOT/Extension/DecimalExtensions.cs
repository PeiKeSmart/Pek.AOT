namespace Pek;

/// <summary>
/// <see cref="Decimal"/> 扩展
/// </summary>
public static class DecimalExtensions
{
    #region Rounding(将数值四舍五入，保留指定小数位数)

    /// <summary>
    /// 将数值四舍五入，保留两位小数
    /// </summary>
    /// <param name="value">值</param>
    /// <returns></returns>
    public static Decimal Rounding(this Decimal value)
    {
        return Math.Round(value, 2);
    }

    /// <summary>
    /// 将数值四舍五入，保留指定小数位数
    /// </summary>
    /// <param name="value">值</param>
    /// <param name="decimals">小数位数</param>
    /// <returns></returns>
    public static Decimal Rounding(this Decimal value, Int32 decimals)
    {
        return Math.Round(value, decimals);
    }

    #endregion

    #region Abs(返回数字的绝对值)

    /// <summary>
    /// 返回数字的绝对值
    /// </summary>
    /// <param name="value">值</param>
    /// <returns></returns>
    public static Decimal Abs(this Decimal value)
    {
        return Math.Abs(value);
    }

    /// <summary>
    /// 返回数字的绝对值
    /// </summary>
    /// <param name="values">值</param>
    /// <returns></returns>
    public static IEnumerable<Decimal> Abs(this IEnumerable<Decimal> values)
    {
        return values.Select(x => x.Abs());
    }

    #endregion
}
