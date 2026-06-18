namespace Pek.Security;

/// <summary>JWT 选项配置</summary>
public class JwtOptions
{
    /// <summary>密钥。密钥加密算法：HmacSha256</summary>
    public String Secret { get; set; }

    /// <summary>发行方</summary>
    public String Issuer { get; set; } = "bing_identity";

    /// <summary>订阅方</summary>
    public String Audience { get; set; } = "bing_client";

    /// <summary>访问令牌有效期分钟数</summary>
    public Double AccessExpireMinutes { get; set; }

    /// <summary>刷新令牌有效期分钟数</summary>
    public Double RefreshExpireMinutes { get; set; }

    /// <summary>启用抛异常方式</summary>
    public Boolean ThrowEnabled { get; set; }

    /// <summary>启用单设备登录</summary>
    public Boolean SingleDeviceEnabled { get; set; }
}
