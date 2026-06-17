namespace Pek;

/// <summary>数字扩展。AOT 安全版</summary>
public static class NumericExtensions
{
    /// <summary>保留小数位数。四舍五入</summary>
    public static Single KeepDigits(this Single value, Int32 digits) => (Single)Math.Round((Decimal)value, digits, MidpointRounding.AwayFromZero);

    /// <summary>保留小数位数。四舍五入</summary>
    public static Double KeepDigits(this Double value, Int32 digits) => (Double)Math.Round((Decimal)value, digits, MidpointRounding.AwayFromZero);

    /// <summary>保留小数位数。四舍五入</summary>
    public static Decimal KeepDigits(this Decimal value, Int32 digits) => Math.Round(value, digits, MidpointRounding.AwayFromZero);

    /// <summary>判断 Byte 是否在给定闭区间</summary>
    public static Boolean IsIn(this Byte value, Byte min, Byte max)
    {
        if (min > max) throw new ArgumentOutOfRangeException(nameof(min), @"最小值不可大于最大值！");
        return value >= min && value <= max;
    }

    /// <summary>判断 Int16 是否在给定闭区间</summary>
    public static Boolean IsIn(this Int16 value, Int16 min, Int16 max)
    {
        if (min > max) throw new ArgumentOutOfRangeException(nameof(min), @"最小值不可大于最大值！");
        return value >= min && value <= max;
    }

    /// <summary>判断 Int32 是否在给定闭区间</summary>
    public static Boolean IsIn(this Int32 value, Int32 min, Int32 max)
    {
        if (min > max) throw new ArgumentOutOfRangeException(nameof(min), @"最小值不可大于最大值！");
        return value >= min && value <= max;
    }

    /// <summary>判断 Int64 是否在给定闭区间</summary>
    public static Boolean IsIn(this Int64 value, Int64 min, Int64 max)
    {
        if (min > max) throw new ArgumentOutOfRangeException(nameof(min), @"最小值不可大于最大值！");
        return value >= min && value <= max;
    }

    /// <summary>判断 Single 是否在给定闭区间</summary>
    public static Boolean IsIn(this Single value, Single min, Single max)
    {
        if (min > max) throw new ArgumentOutOfRangeException(nameof(min), @"最小值不可大于最大值！");
        return value >= min && value <= max;
    }

    /// <summary>判断 Double 是否在给定闭区间</summary>
    public static Boolean IsIn(this Double value, Double min, Double max)
    {
        if (min > max) throw new ArgumentOutOfRangeException(nameof(min), @"最小值不可大于最大值！");
        return value >= min && value <= max;
    }

    /// <summary>判断 Decimal 是否在给定闭区间</summary>
    public static Boolean IsIn(this Decimal value, Decimal min, Decimal max)
    {
        if (min > max) throw new ArgumentOutOfRangeException(nameof(min), @"最小值不可大于最大值！");
        return value >= min && value <= max;
    }
}
