namespace Pek.Data;

/// <summary>模型数据接口，支持通过键索引读写模型属性</summary>
public interface IModel
{
    /// <summary>设置或获取模型数据项</summary>
    /// <param name="key">属性名或逻辑键</param>
    /// <returns>存在则返回属性值；未命中返回 null</returns>
    Object? this[String key] { get; set; }
}