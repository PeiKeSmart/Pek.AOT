using System.ComponentModel;
using Pek.Configuration;

namespace Pek.Configs;

/// <summary>限流配置</summary>
[DisplayName("限流配置")]
[Config("RateLimter")]
public class RateLimterSetting : Config<RateLimterSetting>
{
    /// <summary>是否允许限流</summary>
    [Description("是否允许限流")]
    public Boolean AllowRateLimter { get; set; }
}
