using System.Net;
using System.Net.Http.Headers;

using Pek.Extension;
using Pek.IO;
using Pek.Log;
using Pek.Security;
using Pek.Serialization;
using Pek.Web;

namespace Pek.Http;

/// <summary>Http令牌过滤器，请求前加上令牌，请求后拦截401/403</summary>
public class TokenHttpFilter : IHttpFilter
{
    #region 属性
    /// <summary>用户</summary>
    public String? UserName { get; set; }

    /// <summary>密钥</summary>
    public String? Password { get; set; }

    /// <summary>安全密钥。keyName$keyValue</summary>
    /// <remarks>
    /// 公钥，用于RSA加密用户密码，在通信链路上保护用户密码安全，可以写死在代码里面。
    /// 密钥前面可以增加keyName，形成keyName$keyValue，用于向服务端指示所使用的密钥标识，方便未来更换密钥。
    /// </remarks>
    public String? SecurityKey { get; set; }

    /// <summary>申请令牌动作名，默认 OAuth/Token</summary>
    public String Action { get; set; } = "OAuth/Token";

    /// <summary>令牌信息</summary>
    public IToken? Token { get; set; }

    /// <summary>令牌有效期</summary>
    public DateTime Expire { get; set; }

    private DateTime _refresh;

    /// <summary>清空令牌的错误码。默认401和403</summary>
    public IList<Int32> ErrorCodes { get; set; } = [(Int32)HttpStatusCode.Unauthorized, (Int32)HttpStatusCode.Forbidden];
    #endregion

    static TokenHttpFilter() => JsonHelper.Register(TokenModelJsonContext.Default.TokenModel);

    /// <summary>请求前</summary>
    /// <param name="client">客户端</param>
    /// <param name="request">请求消息</param>
    /// <param name="state">状态数据</param>
    /// <param name="cancellationToken">取消通知</param>
    /// <returns>任务</returns>
    public virtual async Task OnRequest(HttpClient client, HttpRequestMessage request, Object? state, CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization != null) return;

        var uri = request.RequestUri;
        var path = client.BaseAddress == null ? uri?.AbsoluteUri : uri?.OriginalString;
        if (!path.IsNullOrEmpty() && path.StartsWithIgnoreCase(Action.EnsureStart("/"))) return;

        var now = DateTime.Now;
        var token = Token;
        if (token == null || Expire < now)
        {
            token = await SendAuth(client, cancellationToken).ConfigureAwait(false);
            if (token != null)
            {
                Token = token;

                Expire = now.AddSeconds(token.ExpireIn);
                _refresh = now.AddSeconds(token.ExpireIn / 2);
            }
        }

        if (token != null && Expire > now && _refresh < now)
        {
            try
            {
                token = await SendRefresh(client, cancellationToken).ConfigureAwait(false);
                if (token != null)
                {
                    Token = token;

                    Expire = now.AddSeconds(token.ExpireIn);
                    _refresh = now.AddSeconds(token.ExpireIn / 2);
                }
            }
            catch (Exception ex)
            {
                XTrace.WriteLine("刷新令牌异常");
                XTrace.WriteException(ex);
            }
        }

        if (token != null && Expire > now)
        {
            var type = (token as TokenModel)?.TokenType;
            if (type.IsNullOrEmpty() || type.EqualIgnoreCase("Token", "JWT")) type = "Bearer";
            request.Headers.Authorization = new AuthenticationHeaderValue(type, token.AccessToken);
        }
    }

    /// <summary>发起密码认证请求</summary>
    /// <param name="client"></param>
    /// <param name="cancellationToken">取消通知</param>
    /// <returns></returns>
    protected virtual async Task<IToken?> SendAuth(HttpClient client, CancellationToken cancellationToken)
    {
        if (UserName.IsNullOrEmpty()) throw new ArgumentNullException(nameof(UserName));

        var pass = EncodePassword(UserName, Password);
        Dictionary<String, String?> data = new()
        {
            ["grant_type"] = "password",
            ["username"] = UserName,
            ["password"] = pass,
            ["clientId"] = Runtime.ClientId,
        };

        var rs = await client.PostFormAsync(Action, data, null, cancellationToken).ConfigureAwait(false);
        return rs.IsNullOrWhiteSpace() ? null : JsonHelper.Default.Read<TokenModel>(rs);
    }

    /// <summary>发起刷新令牌请求</summary>
    /// <param name="client"></param>
    /// <param name="cancellationToken">取消通知</param>
    /// <returns></returns>
    protected virtual async Task<IToken?> SendRefresh(HttpClient client, CancellationToken cancellationToken)
    {
        if (Token == null) throw new ArgumentNullException(nameof(Token));

        Dictionary<String, String?> data = new()
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = Token.RefreshToken,
            ["clientId"] = Runtime.ClientId,
        };

        var rs = await client.PostFormAsync(Action, data, null, cancellationToken).ConfigureAwait(false);
        return rs.IsNullOrWhiteSpace() ? null : JsonHelper.Default.Read<TokenModel>(rs);
    }

    /// <summary>编码密码，在传输中保护安全，一般使用RSA加密</summary>
    /// <param name="username"></param>
    /// <param name="password"></param>
    /// <returns></returns>
    protected virtual String? EncodePassword(String username, String? password)
    {
        if (password.IsNullOrEmpty()) return password;

        var key = SecurityKey;
        if (!key.IsNullOrEmpty())
        {
            var name = String.Empty;
            var p = key.IndexOf('$');
            if (p >= 0)
            {
                name = key[..p];
                key = key[(p + 1)..];
            }

            var pass = RSAHelper.Encrypt(password.GetBytes(), key).ToBase64();
            password = name.IsNullOrEmpty() ? $"$rsa$${pass}" : $"$rsa${name}${pass}";
        }

        return password;
    }

    /// <summary>获取响应后</summary>
    /// <param name="client">客户端</param>
    /// <param name="response">响应消息</param>
    /// <param name="state">状态数据</param>
    /// <param name="cancellationToken">取消通知</param>
    /// <returns>任务</returns>
    public virtual Task OnResponse(HttpClient client, HttpResponseMessage response, Object? state, CancellationToken cancellationToken)
    {
        var code = (Int32)response.StatusCode;
        if (ErrorCodes.Contains(code)) Expire = DateTime.MinValue;

        return Task.CompletedTask;
    }

    /// <summary>发生错误时</summary>
    /// <param name="client">客户端</param>
    /// <param name="exception">异常</param>
    /// <param name="state">状态数据</param>
    /// <param name="cancellationToken">取消通知</param>
    /// <returns>任务</returns>
    public virtual Task OnError(HttpClient client, Exception exception, Object? state, CancellationToken cancellationToken) => Task.CompletedTask;
}