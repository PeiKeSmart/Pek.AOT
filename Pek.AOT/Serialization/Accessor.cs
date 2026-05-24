using Pek.Data;

namespace Pek.Serialization;

/// <summary>访问器基类</summary>
/// <remarks>
/// AOT 版本不再内置 Binary/IFormatterX/反射访问逻辑。
/// 具体类型自行实现 IAccessor 的流式读写，公共辅助能力复用现有 AccessorHelper。
/// </remarks>
public abstract class Accessor : IAccessor
{
    /// <summary>从数据流中读取消息</summary>
    /// <param name="stream">数据流</param>
    /// <param name="context">上下文</param>
    /// <returns>是否成功</returns>
    public abstract Boolean Read(Stream stream, Object? context);

    /// <summary>把消息写入到数据流中</summary>
    /// <param name="stream">数据流</param>
    /// <param name="context">上下文</param>
    /// <returns>是否成功</returns>
    public abstract Boolean Write(Stream stream, Object? context);

    /// <summary>消息转为数据包</summary>
    /// <returns>数据包</returns>
    public virtual IPacket ToPacket() => AccessorHelper.ToPacket(this);
}

/// <summary>访问器泛型基类</summary>
/// <typeparam name="T">访问器类型</typeparam>
public abstract class Accessor<T> : Accessor where T : Accessor<T>, new()
{
    /// <summary>从流中读取消息</summary>
    /// <param name="stream">数据流</param>
    /// <returns>访问器实例</returns>
    public static T? Read(Stream stream)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        var obj = new T();
        if (!obj.Read(stream, null)) return default;

        return obj;
    }

    /// <summary>从数据包中读取消息</summary>
    /// <param name="packet">数据包</param>
    /// <returns>访问器实例</returns>
    public static T? Read(IPacket packet)
    {
        if (packet == null) throw new ArgumentNullException(nameof(packet));

        return Read(packet.GetStream(false));
    }
}