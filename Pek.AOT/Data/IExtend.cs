namespace Pek.Data;

/// <summary>具有可读写的扩展数据</summary>
public interface IExtend
{
    /// <summary>扩展数据项字典</summary>
    IDictionary<String, Object?> Items { get; }

    /// <summary>设置或获取扩展数据项</summary>
    /// <param name="key">扩展数据键</param>
    /// <returns>存在则返回对应值；未命中返回 null</returns>
    Object? this[String key] { get; set; }
}

/// <summary>具有扩展数据键集合</summary>
[Obsolete("逐步取消 IExtend2，建议直接使用 IExtend 或 IModel")]
public interface IExtend2 : IExtend
{
    /// <summary>扩展数据键集合</summary>
    IEnumerable<String> Keys { get; }
}

/// <summary>具有扩展数据字典</summary>
[Obsolete("逐步取消 IExtend3，建议直接使用 IExtend 或 IModel")]
public interface IExtend3 : IExtend
{
}