namespace Pek.Web;

/// <summary>访问令牌模型</summary>
public interface IToken
{
    /// <summary>访问令牌</summary>
    String? AccessToken { get; set; }

    /// <summary>刷新令牌</summary>
    String? RefreshToken { get; set; }

    /// <summary>过期时间。秒</summary>
    Int32 ExpireIn { get; set; }
}