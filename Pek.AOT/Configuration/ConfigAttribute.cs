namespace Pek.Configuration;

/// <summary>配置特性</summary>
/// <remarks>
/// 声明配置模型使用哪一种配置提供者，以及所需要的文件名或分类名。
/// 当前 Pek.AOT 仅支持本地 XML/JSON 配置文件。
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class ConfigAttribute : Attribute
{
    /// <summary>提供者。当前支持 xml/json，不指定时使用全局默认</summary>
    public String? Provider { get; set; }

    /// <summary>配置名。可以是文件名或分类名</summary>
    public String Name { get; set; }

    /// <summary>指定配置名</summary>
    /// <param name="name">配置名。可以是文件名或分类名</param>
    /// <param name="provider">提供者。当前支持 xml/json</param>
    public ConfigAttribute(String name, String? provider = null)
    {
        Provider = provider;
        Name = name;
    }
}