using System.Collections.Concurrent;

using Pek.Data;

namespace Pek.Serialization;

/// <summary>数据流序列化访问器。接口实现者可以在这里完全自定义序列化行为</summary>
public interface IAccessor
{
    /// <summary>从数据流中读取消息</summary>
    /// <param name="stream">数据流</param>
    /// <param name="context">上下文</param>
    /// <returns>是否成功</returns>
    Boolean Read(Stream stream, Object? context);

    /// <summary>把消息写入到数据流中</summary>
    /// <param name="stream">数据流</param>
    /// <param name="context">上下文</param>
    /// <returns>是否成功</returns>
    Boolean Write(Stream stream, Object? context);
}

/// <summary>自定义数据序列化访问器</summary>
/// <typeparam name="T">数据类型</typeparam>
public interface IAccessor<T>
{
    /// <summary>从数据中读取消息</summary>
    /// <param name="data">数据</param>
    /// <param name="context">上下文</param>
    /// <returns>是否成功</returns>
    Boolean Read(T data, Object? context);

    /// <summary>把消息写入到数据中</summary>
    /// <param name="data">数据</param>
    /// <param name="context">上下文</param>
    /// <returns>是否成功</returns>
    Boolean Write(T data, Object? context);
}

/// <summary>访问器助手</summary>
public static class AccessorHelper
{
    private static readonly ConcurrentDictionary<Type, Func<Object>> _factories = new();

    /// <summary>注册访问器工厂</summary>
    /// <typeparam name="T">访问器类型</typeparam>
    public static void RegisterFactory<T>() where T : class, IAccessor, new() => RegisterFactory<T>(() => new T());

    /// <summary>注册访问器工厂</summary>
    /// <typeparam name="T">访问器类型</typeparam>
    /// <param name="factory">访问器工厂</param>
    public static void RegisterFactory<T>(Func<T> factory) where T : class, IAccessor
    {
        if (factory == null) throw new ArgumentNullException(nameof(factory));

        _factories[typeof(T)] = () => factory() ?? throw new InvalidOperationException($"Accessor factory returned null for {typeof(T).FullName}");
    }

    /// <summary>支持访问器的对象转数据包</summary>
    /// <param name="accessor">访问器</param>
    /// <param name="context">上下文</param>
    /// <returns>数据包</returns>
    public static IPacket ToPacket(this IAccessor accessor, Object? context = null)
    {
        if (accessor == null) throw new ArgumentNullException(nameof(accessor));

        var stream = new MemoryStream { Position = 8 };
        accessor.Write(stream, context);
        stream.Position = 8;

        return new ArrayPacket(stream);
    }

    /// <summary>通过访问器读取</summary>
    /// <param name="type">访问器类型</param>
    /// <param name="packet">数据包</param>
    /// <param name="context">上下文</param>
    /// <returns>实体对象</returns>
    public static Object? AccessorRead(this Type type, IPacket packet, Object? context = null)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        if (packet == null) throw new ArgumentNullException(nameof(packet));

        if (!_factories.TryGetValue(type, out var factory))
            throw new InvalidOperationException($"Accessor type {type.FullName} is not registered. Call RegisterFactory before using AccessorRead.");

        var obj = factory();
        if (obj is not IAccessor accessor)
            throw new InvalidOperationException($"Registered factory for {type.FullName} did not produce an IAccessor instance.");

        accessor.Read(packet.GetStream(false), context);
        return obj;
    }

    /// <summary>通过访问器转换数据包为实体对象</summary>
    /// <typeparam name="T">访问器类型</typeparam>
    /// <param name="packet">数据包</param>
    /// <param name="context">上下文</param>
    /// <returns>实体对象</returns>
    public static T ToEntity<T>(this IPacket packet, Object? context = null) where T : IAccessor, new()
    {
        if (packet == null) throw new ArgumentNullException(nameof(packet));

        var obj = new T();
        obj.Read(packet.GetStream(false), context);

        return obj;
    }
}