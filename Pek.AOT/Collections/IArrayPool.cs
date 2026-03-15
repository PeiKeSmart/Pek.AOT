namespace Pek.Collections;

/// <summary>数组缓冲池</summary>
/// <typeparam name="T">元素类型</typeparam>
public interface IArrayPool<T>
{
    /// <summary>借出数组</summary>
    /// <param name="minimumLength">最小长度</param>
    /// <returns>数组实例</returns>
    T[] Rent(Int32 minimumLength);

    /// <summary>归还数组</summary>
    /// <param name="array">数组实例</param>
    /// <param name="clearArray">是否清空数组</param>
    void Return(T[] array, Boolean clearArray = false);
}

/// <summary>数组池</summary>
public class ArrayPool
{
    /// <summary>空字节数组</summary>
    public static Byte[] Empty { get; } = [];
}