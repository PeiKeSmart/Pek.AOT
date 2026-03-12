using System.Buffers;
using System.Text;

namespace Pek.Collections;

/// <summary>对象池接口</summary>
/// <typeparam name="T">池化类型</typeparam>
public interface IPool<T>
{
    /// <summary>对象池大小</summary>
    Int32 Max { get; set; }

    /// <summary>获取实例</summary>
    /// <returns>对象实例</returns>
    T Get();

    /// <summary>归还实例</summary>
    /// <param name="value">对象实例</param>
    /// <returns>是否归还成功</returns>
    [Obsolete("Please use Return from 2024-02-01")]
    Boolean Put(T value);

    /// <summary>归还实例</summary>
    /// <param name="value">对象实例</param>
    /// <returns>是否归还成功</returns>
    Boolean Return(T value);

    /// <summary>清空对象池</summary>
    /// <returns>清理的对象数量</returns>
    Int32 Clear();
}

/// <summary>对象池扩展</summary>
public static class Pool
{
    /// <summary>字符串构建器池</summary>
    public static IPool<StringBuilder> StringBuilder { get; set; } = new StringBuilderPool();

    /// <summary>内存流池</summary>
    public static IPool<MemoryStream> MemoryStream { get; set; } = new MemoryStreamPool();

    /// <summary>字节数组共享池</summary>
    public static ArrayPool<Byte> Shared { get; set; } = ArrayPool<Byte>.Shared;

    /// <summary>空字节数组</summary>
    public static Byte[] Empty { get; } = [];

    /// <summary>归还字符串构建器并返回结果</summary>
    /// <param name="sb">字符串构建器</param>
    /// <param name="requireResult">是否返回结果</param>
    /// <returns>字符串结果</returns>
    [Obsolete("Please use Return from 2024-02-01")]
    public static String Put(this StringBuilder sb, Boolean requireResult = false)
    {
        var str = requireResult ? sb.ToString() : String.Empty;
        StringBuilder.Return(sb);
        return str;
    }

    /// <summary>归还字符串构建器并返回结果</summary>
    /// <param name="sb">字符串构建器</param>
    /// <param name="returnResult">是否返回结果</param>
    /// <returns>字符串结果</returns>
    public static String Return(this StringBuilder sb, Boolean returnResult = true)
    {
        var str = returnResult ? sb.ToString() : String.Empty;
        StringBuilder.Return(sb);
        return str;
    }

    /// <summary>归还内存流并返回结果</summary>
    /// <param name="ms">内存流</param>
    /// <param name="requireResult">是否返回结果</param>
    /// <returns>字节数组</returns>
    [Obsolete("Please use Return from 2024-02-01")]
    public static Byte[] Put(this MemoryStream ms, Boolean requireResult = false) => Return(ms, requireResult);

    /// <summary>归还内存流并返回结果</summary>
    /// <param name="ms">内存流</param>
    /// <param name="returnResult">是否返回结果</param>
    /// <returns>字节数组</returns>
    public static Byte[] Return(this MemoryStream ms, Boolean returnResult = true)
    {
        var buf = returnResult ? ms.ToArray() : Empty;
        MemoryStream.Return(ms);
        return buf;
    }

    /// <summary>字符串构建器池</summary>
    public class StringBuilderPool : Pool<StringBuilder>
    {
        /// <summary>初始容量</summary>
        public Int32 InitialCapacity { get; set; } = 100;

        /// <summary>最大容量</summary>
        public Int32 MaximumCapacity { get; set; } = 4 * 1024;

        /// <summary>实例化字符串构建器池</summary>
        public StringBuilderPool() : base(0, true) { }

        /// <summary>创建实例</summary>
        /// <returns>字符串构建器</returns>
        protected override StringBuilder OnCreate() => new(InitialCapacity);

        /// <summary>归还实例</summary>
        /// <param name="value">字符串构建器</param>
        /// <returns>是否归还成功</returns>
        public override Boolean Return(StringBuilder value)
        {
            if (value.Capacity > MaximumCapacity) return false;

            value.Clear();
            return base.Return(value);
        }
    }

    /// <summary>内存流池</summary>
    public class MemoryStreamPool : Pool<MemoryStream>
    {
        /// <summary>初始容量</summary>
        public Int32 InitialCapacity { get; set; } = 1024;

        /// <summary>最大容量</summary>
        public Int32 MaximumCapacity { get; set; } = 64 * 1024;

        /// <summary>实例化内存流池</summary>
        public MemoryStreamPool() : base(0, true) { }

        /// <summary>创建实例</summary>
        /// <returns>内存流</returns>
        protected override MemoryStream OnCreate() => new(InitialCapacity);

        /// <summary>归还实例</summary>
        /// <param name="value">内存流</param>
        /// <returns>是否归还成功</returns>
        public override Boolean Return(MemoryStream value)
        {
            if (value.Capacity > MaximumCapacity) return false;

            value.Position = 0;
            value.SetLength(0);
            return base.Return(value);
        }
    }
}