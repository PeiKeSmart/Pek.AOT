namespace NewLife.Reflection;

/// <summary>索引器访问接口</summary>
public interface IIndexAccessor
{
    /// <summary>获取或设置指定名称的属性或字段值</summary>
    /// <param name="name">成员名称</param>
    /// <returns>成员值</returns>
    Object this[String name] { get; set; }
}