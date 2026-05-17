using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Pek.Web;

/// <summary>访问令牌模型</summary>
public class TokenModel : IToken
{
    /// <summary>访问令牌</summary>
    [DataMember(Name = "access_token")]
    [JsonPropertyName("access_token")]
    public String? AccessToken { get; set; }

    /// <summary>令牌类型</summary>
    [DataMember(Name = "token_type")]
    [JsonPropertyName("token_type")]
    public String? TokenType { get; set; }

    /// <summary>过期时间。秒</summary>
    [DataMember(Name = "expire_in")]
    [JsonPropertyName("expire_in")]
    public Int32 ExpireIn { get; set; }

    /// <summary>刷新令牌</summary>
    [DataMember(Name = "refresh_token")]
    [JsonPropertyName("refresh_token")]
    public String? RefreshToken { get; set; }

    /// <summary>作用域</summary>
    [DataMember(Name = "scope")]
    [JsonPropertyName("scope")]
    public String? Scope { get; set; }
}

/// <summary>访问令牌模型的AOT序列化上下文</summary>
[JsonSerializable(typeof(TokenModel))]
public partial class TokenModelJsonContext : JsonSerializerContext
{
}