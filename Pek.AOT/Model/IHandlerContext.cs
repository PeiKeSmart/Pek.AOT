using Pek.Collections;
using Pek.Data;

namespace Pek.Model;

/// <summary>处理器上下文</summary>
public interface IHandlerContext
{
    /// <summary>管道</summary>
    IPipeline? Pipeline { get; set; }

    /// <summary>上下文拥有者</summary>
    Object? Owner { get; set; }

    /// <summary>读取管道过滤后最终处理消息</summary>
    /// <param name="message">消息</param>
    void FireRead(Object message);

    /// <summary>写入管道过滤后最终处理消息</summary>
    /// <param name="message">消息</param>
    /// <returns>写出结果</returns>
    Int32 FireWrite(Object message);
}

/// <summary>处理器上下文</summary>
public class HandlerContext : IHandlerContext, IExtend
{
    /// <summary>管道</summary>
    public IPipeline? Pipeline { get; set; }

    /// <summary>上下文拥有者</summary>
    public Object? Owner { get; set; }

    /// <summary>数据项</summary>
    public IDictionary<String, Object?> Items { get; } = new NullableDictionary<String, Object?>();

    /// <summary>设置或获取数据项</summary>
    /// <param name="key">数据键</param>
    /// <returns>数据值</returns>
    public Object? this[String key]
    {
        get => Items.TryGetValue(key, out var obj) ? obj : null;
        set => Items[key] = value;
    }

    /// <summary>读取管道过滤后最终处理消息</summary>
    /// <param name="message">消息</param>
    public virtual void FireRead(Object message) { }

    /// <summary>写入管道过滤后最终处理消息</summary>
    /// <param name="message">消息</param>
    /// <returns>写出结果</returns>
    public virtual Int32 FireWrite(Object message) => 0;
}