namespace Pek.Extension;

/// <summary>数据位助手</summary>
public static class BitHelper
{
    /// <summary>设置数据位</summary>
    /// <param name="value">数值</param>
    /// <param name="position">位位置</param>
    /// <param name="flag">是否置位</param>
    /// <returns>设置后的数值</returns>
    public static UInt16 SetBit(this UInt16 value, Int32 position, Boolean flag)
    {
        return SetBits(value, position, 1, flag ? (Byte)1 : (Byte)0);
    }

    /// <summary>设置数据位</summary>
    /// <param name="value">数值</param>
    /// <param name="position">起始位位置</param>
    /// <param name="length">位长度</param>
    /// <param name="bits">位数据</param>
    /// <returns>设置后的数值</returns>
    public static UInt16 SetBits(this UInt16 value, Int32 position, Int32 length, UInt16 bits)
    {
        if (length <= 0 || position >= 16) return value;

        var mask = (2 << (length - 1)) - 1;

        value &= (UInt16)~(mask << position);
        value |= (UInt16)((bits & mask) << position);

        return value;
    }

    /// <summary>设置数据位</summary>
    /// <param name="value">数值</param>
    /// <param name="position">位位置</param>
    /// <param name="flag">是否置位</param>
    /// <returns>设置后的数值</returns>
    public static Byte SetBit(this Byte value, Int32 position, Boolean flag)
    {
        if (position >= 8) return value;

        var mask = (2 << (1 - 1)) - 1;

        value &= (Byte)~(mask << position);
        value |= (Byte)(((flag ? 1 : 0) & mask) << position);

        return value;
    }

    /// <summary>获取数据位</summary>
    /// <param name="value">数值</param>
    /// <param name="position">位位置</param>
    /// <returns>是否置位</returns>
    public static Boolean GetBit(this UInt16 value, Int32 position)
    {
        return GetBits(value, position, 1) == 1;
    }

    /// <summary>获取数据位</summary>
    /// <param name="value">数值</param>
    /// <param name="position">起始位位置</param>
    /// <param name="length">位长度</param>
    /// <returns>获取到的位数据</returns>
    public static UInt16 GetBits(this UInt16 value, Int32 position, Int32 length)
    {
        if (length <= 0 || position >= 16) return 0;

        var mask = (2 << (length - 1)) - 1;

        return (UInt16)((value >> position) & mask);
    }

    /// <summary>获取数据位</summary>
    /// <param name="value">数值</param>
    /// <param name="position">位位置</param>
    /// <returns>是否置位</returns>
    public static Boolean GetBit(this Byte value, Int32 position)
    {
        if (position >= 8) return false;

        var mask = (2 << (1 - 1)) - 1;

        return ((Byte)((value >> position) & mask)) == 1;
    }
}