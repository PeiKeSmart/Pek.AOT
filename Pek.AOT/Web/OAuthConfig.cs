using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

using Pek.Configuration;
using Pek.Extension;
using Pek.IO;
using Pek.Security;

namespace Pek.Web;

/// <summary>OAuth配置</summary>
[DisplayName("OAuth设置")]
[Config("OAuth")]
public class OAuthConfig : Config<OAuthConfig, OAuthConfigJsonContext>
{
    /// <summary>调试开关。默认true</summary>
    [Description("调试开关。默认true")]
    public Boolean Debug { get; set; } = true;

    /// <summary>应用地址。域名和端口，应用系统经过反向代理重定向时指定外部地址</summary>
    [Description("应用地址。域名和端口，应用系统经过反向代理重定向时指定外部地址")]
    public String AppUrl { get; set; } = String.Empty;

    /// <summary>配置项</summary>
    [Description("配置项")]
    public OAuthItem[] Items { get; set; } = [];

    /// <summary>已加载</summary>
    protected override void OnLoaded()
    {
        var ms = Items;
        if (ms == null || ms.Length == 0)
        {
            List<OAuthItem> list =
            [
                new OAuthItem { Name = "QQ" },
                new OAuthItem { Name = "Weixin" },
                new OAuthItem { Name = "Baidu" },
                new OAuthItem { Name = "Weibo" },
                new OAuthItem { Name = "Taobao" },
                new OAuthItem { Name = "Alipay" },
                new OAuthItem { Name = "Github" }
            ];

            var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var appName = assembly.GetName().Name ?? "Pek.AOT";
            var mi = new OAuthItem
            {
                Name = "NewLife",
                Server = "https://sso.newlifex.com/sso",
                AppID = appName,
                Secret = appName.GetBytes().RC4("NewLife".GetBytes()).ToBase64(),
            };
            list.Add(mi);
            Items = [.. list];
        }

        base.OnLoaded();
    }

    /// <summary>获取</summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public OAuthItem? Get(String name) => Items.FirstOrDefault(e => e.Name.EqualIgnoreCase(name));

    /// <summary>获取或添加</summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public OAuthItem? GetOrAdd(String name)
    {
        if (name.IsNullOrEmpty()) return null;

        var mi = Items.FirstOrDefault(e => e.Name.EqualIgnoreCase(name));
        if (mi != null) return mi;

        lock (this)
        {
            var list = new List<OAuthItem>(Items);
            mi = list.FirstOrDefault(e => e.Name.EqualIgnoreCase(name));
            if (mi != null) return mi;

            mi = new OAuthItem { Name = name };
            list.Add(mi);

            Items = [.. list];

            return mi;
        }
    }
}

/// <summary>开放验证服务器配置项</summary>
public class OAuthItem
{
    /// <summary>服务地址</summary>
    [XmlAttribute]
    public String Name { get; set; } = String.Empty;

    /// <summary>验证服务地址</summary>
    [XmlAttribute]
    public String Server { get; set; } = String.Empty;

    /// <summary>令牌服务地址。可以不同于验证地址的内网直达地址</summary>
    [XmlAttribute]
    public String AccessServer { get; set; } = String.Empty;

    /// <summary>应用标识</summary>
    [XmlAttribute]
    public String AppID { get; set; } = String.Empty;

    /// <summary>密钥</summary>
    [XmlAttribute]
    public String Secret { get; set; } = String.Empty;

    /// <summary>授权范围</summary>
    [XmlAttribute]
    public String Scope { get; set; } = String.Empty;
}

/// <summary>OAuth配置的AOT序列化上下文</summary>
[JsonSerializable(typeof(OAuthItem))]
[JsonSerializable(typeof(OAuthItem[]))]
[JsonSerializable(typeof(OAuthConfig))]
public partial class OAuthConfigJsonContext : JsonSerializerContext
{
}