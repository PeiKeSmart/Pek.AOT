namespace Pek;

/// <summary>
/// 基础类型扩展 - Buffer
/// </summary>
public static class BufferExtensions
{
    /// <summary>
    /// 块复制
    /// </summary>
    /// <param name="src">源数组</param>
    /// <param name="srcOffset">源数组偏移量</param>
    /// <param name="dst">目标数组</param>
    /// <param name="dstOffset">目标数组偏移量</param>
    /// <param name="count">数量</param>
    public static void BlockCopy(this Array src, Int32 srcOffset, Array dst, Int32 dstOffset, Int32 count) => Buffer.BlockCopy(src, srcOffset, dst, dstOffset, count);

    /// <summary>
    /// 获取字节长度
    /// </summary>
    /// <param name="array">数组</param>
    public static Int32 ByteLength(this Array array) => Buffer.ByteLength(array);

    /// <summary>
    /// 获取指定索引的字节
    /// </summary>
    /// <param name="array">数组</param>
    /// <param name="index">索引</param>
    public static Byte GetByte(this Array array, Int32 index) => Buffer.GetByte(array, index);

    /// <summary>
    /// 设置字节
    /// </summary>
    /// <param name="array">数值</param>
    /// <param name="index">索引</param>
    /// <param name="value">字节</param>
    public static void SetByte(this Array array, Int32 index, Byte value) => Buffer.SetByte(array, index, value);
}
