namespace Pek.Data;

/// <summary>内存字节流编码器</summary>
public interface IMemoryEncoder
{
    /// <summary>数值转内存字节流</summary>
    /// <param name="value">目标对象</param>
    /// <returns>编码后的内存数据</returns>
    Memory<Byte> Encode(Object value);

    /// <summary>内存字节流转对象</summary>
    /// <param name="data">源内存数据</param>
    /// <param name="type">目标类型</param>
    /// <returns>解码后的对象</returns>
    Object Decode(Memory<Byte> data, Type type);
}