namespace Pek.Configuration;

/// <summary>配置映射接口</summary>
public interface IConfigMapping
{
    /// <summary>映射配置树到当前对象</summary>
    /// <param name="provider">配置提供者</param>
    /// <param name="section">配置数据段</param>
    void MapConfig(IConfigProvider provider, IConfigSection section);
}