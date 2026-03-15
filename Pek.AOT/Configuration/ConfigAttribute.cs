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

/// <summary>Http配置特性</summary>
/// <remarks>
/// 用于兼容 DH.NCore 的配置声明形态。
/// 当前 Pek.AOT 仍仅支持本地 XML/JSON 配置文件，相关属性仅用于保留 API 表面。
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class HttpConfigAttribute : ConfigAttribute
{
    /// <summary>服务器地址</summary>
    public String Server { get; set; }

    /// <summary>服务操作</summary>
    public String Action { get; set; }

    /// <summary>应用标识</summary>
    public String AppId { get; set; }

    /// <summary>应用密钥</summary>
    public String? Secret { get; set; }

    /// <summary>作用域</summary>
    public String? Scope { get; set; }

    /// <summary>本地缓存等级</summary>
    public ConfigCacheLevel CacheLevel { get; set; }

    /// <summary>指定Http配置</summary>
    /// <param name="name">配置名</param>
    /// <param name="server">服务器地址</param>
    /// <param name="action">服务操作</param>
    /// <param name="appId">应用标识</param>
    /// <param name="secret">应用密钥</param>
    /// <param name="scope">作用域</param>
    /// <param name="cacheLevel">本地缓存等级</param>
    public HttpConfigAttribute(String name, String server, String action, String appId, String? secret = null, String? scope = null, ConfigCacheLevel cacheLevel = ConfigCacheLevel.Encrypted)
        : base(name, "http")
    {
        Server = server;
        Action = action;
        AppId = appId;
        Secret = secret;
        Scope = scope;
        CacheLevel = cacheLevel;
    }
}