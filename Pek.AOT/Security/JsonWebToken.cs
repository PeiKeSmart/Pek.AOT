namespace Pek.Security;

/// <summary>JWT 令牌信息。AOT 安全版，使用 BCL 标准 API 替代 UnixTime/Conv/ToJsGetTime</summary>
[Serializable]
public class JsonWebToken
{
    /// <summary>用户Id</summary>
    public Int32 UId { get; set; }

    /// <summary>访问令牌。用于业务身份认证的令牌</summary>
    public String AccessToken { get; set; } = String.Empty;

    /// <summary>访问令牌有效期。UTC 标准，Unix 毫秒时间戳</summary>
    public Int64 AccessTokenUtcExpires { get; set; }

    /// <summary>刷新令牌。用于刷新 AccessToken 的令牌</summary>
    public String RefreshToken { get; set; } = String.Empty;

    /// <summary>刷新令牌有效期。UTC 标准，Unix 毫秒时间戳</summary>
    public Int64 RefreshUtcExpires { get; set; }

    /// <summary>访问令牌签发时间。UTC 标准，Unix 毫秒时间戳</summary>
    public Int64 StartTime { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>是否已过期</summary>
    public Boolean IsExpired() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > AccessTokenUtcExpires;

    /// <summary>是否已过期，可指定提前分钟数</summary>
    /// <param name="min">提前分钟数</param>
    public Boolean IsExpired(Int32 min) => DateTimeOffset.UtcNow.AddMinutes(min).ToUnixTimeMilliseconds() > AccessTokenUtcExpires;

    /// <summary>刷新令牌是否已过期</summary>
    public Boolean IsRefreshExpired() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > RefreshUtcExpires;

    /// <summary>刷新令牌是否已过期，可指定提前分钟数</summary>
    /// <param name="min">提前分钟数</param>
    public Boolean IsRefreshExpired(Int32 min) => DateTimeOffset.UtcNow.AddMinutes(min).ToUnixTimeMilliseconds() > RefreshUtcExpires;
}
