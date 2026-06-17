namespace Pek;

/// <summary>
/// 字节值(<see cref="Byte"/>) 系统扩展
/// </summary>
/// <remarks>源自上游 System/Extensions.Byte.cs，因 Bases/ByteExtensions.cs 已占用 ByteExtensions 类名，故独立命名</remarks>
public static class ByteSysExtensions
{
    /// <summary>
    /// 获取最大值
    /// </summary>
    /// <param name="val1">值1</param>
    /// <param name="val2">值2</param>
    public static Byte Max(this Byte val1, Byte val2) => Math.Max(val1, val2);

    /// <summary>
    /// 获取最小值
    /// </summary>
    /// <param name="val1">值1</param>
    /// <param name="val2">值2</param>
    public static Byte Min(this Byte val1, Byte val2) => Math.Min(val1, val2);
}
