using System.Net;

using Pek.Extension;
using Pek.Log;
using Pek.Serialization;

namespace Pek.Web;

/// <summary>OAuth 2.0 客户端</summary>
public class OAuthClient
{
    #region 属性
    /// <summary>名称</summary>
    public String Name { get; set; }

    /// <summary>验证服务器地址</summary>
    public String Server { get; set; }

    /// <summary>令牌服务地址。可以不同于验证地址的内网直达地址</summary>
    public String AccessServer { get; set; }

    /// <summary>应用Key</summary>
    public String Key { get; set; }

    /// <summary>安全码</summary>
    public String Secret { get; set; }

    /// <summary>验证地址</summary>
    public String AuthUrl { get; set; }

    /// <summary>访问令牌地址</summary>
    public String AccessUrl { get; set; }

    /// <summary>响应类型</summary>
    /// <remarks>
    /// 验证服务器跳转回来子系统时的类型，默认code，此时还需要子系统服务端请求验证服务器换取AccessToken；
    /// 可选token，此时验证服务器直接返回AccessToken，子系统不需要再次请求。
    /// </remarks>
    public String ResponseType { get; set; } = "code";

    /// <summary>作用域</summary>
    public String Scope { get; set; }
    #endregion

    #region 返回参数
    /// <summary>授权码</summary>
    public String Code { get; set; }

    /// <summary>访问令牌</summary>
    public String AccessToken { get; set; }

    /// <summary>刷新令牌</summary>
    public String RefreshToken { get; set; }

    /// <summary>统一标识</summary>
    public String OpenID { get; set; }

    /// <summary>企业级标识</summary>
    public String UnionID { get; set; }

    /// <summary>过期时间</summary>
    public DateTime Expire { get; set; }

    /// <summary>访问项</summary>
    public IDictionary<String, String>? Items { get; set; }
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public OAuthClient()
    {
        Name = GetType().Name.TrimEnd("Client");
        Server = String.Empty;
        AccessServer = String.Empty;
        Key = String.Empty;
        Secret = String.Empty;
        AuthUrl = "authorize?response_type={response_type}&client_id={key}&redirect_uri={redirect}&state={state}&scope={scope}";
        AccessUrl = "access_token?grant_type=authorization_code&client_id={key}&client_secret={secret}&code={code}&state={state}&redirect_uri={redirect}";
        Scope = String.Empty;
        Code = String.Empty;
        AccessToken = String.Empty;
        RefreshToken = String.Empty;
        OpenID = String.Empty;
        UnionID = String.Empty;
        UserUrl = String.Empty;
        UserName = String.Empty;
        NickName = String.Empty;
        UserCode = String.Empty;
        Mobile = String.Empty;
        Mail = String.Empty;
        Avatar = String.Empty;
        Detail = String.Empty;
        OpenIDUrl = String.Empty;
        LogoutUrl = String.Empty;
    }
    #endregion

    #region 静态创建
    /// <summary>根据名称创建客户端</summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static OAuthClient Create(String name)
    {
        if (name.IsNullOrEmpty())
        {
            var set = OAuthConfig.Current;
            var mi = set.Items.FirstOrDefault(e => !e.AppID.IsNullOrEmpty());
            if (mi != null) name = mi.Name;
        }
        if (name.IsNullOrEmpty()) throw new ArgumentNullException(nameof(name), "未正确配置OAuth");

        OAuthClient client = name.ToLowerInvariant() switch
        {
            "qq" => new OAuth.QQClient(),
            "weixin" => new OAuth.WeixinClient(),
            "baidu" => new OAuth.BaiduClient(),
            "taobao" => new OAuth.TaobaoClient(),
            "github" => new OAuth.GithubClient(),
            _ => new OAuthClient(),
        };
        client.Apply(name);

        if (name.EqualIgnoreCase("NewLife") && client.LogoutUrl.IsNullOrEmpty()) client.LogoutUrl = "logout?client_id={key}&redirect_uri={redirect}&state={state}";

        return client;
    }
    #endregion

    #region 方法
    /// <summary>应用参数设置</summary>
    /// <param name="name"></param>
    public void Apply(String name)
    {
        var set = OAuthConfig.Current;
        var ms = set.Items;
        if (ms == null || ms.Length == 0) throw new InvalidOperationException("未设置OAuth服务端");

        var mi = set.GetOrAdd(name);
        if (name.IsNullOrEmpty()) mi = ms.FirstOrDefault(e => !e.AppID.IsNullOrEmpty());
        if (mi == null) throw new InvalidOperationException($"未找到有效的OAuth服务端设置[{name}]");

        Name = mi.Name;

        if (set.Debug) Log = XTrace.Log;

        Apply(mi);
    }

    /// <summary>应用参数设置</summary>
    /// <param name="mi"></param>
    public virtual void Apply(OAuthItem mi)
    {
        Name = mi.Name;
        if (!mi.Server.IsNullOrEmpty()) Server = mi.Server;
        if (!mi.AccessServer.IsNullOrEmpty()) AccessServer = mi.AccessServer;
        if (!mi.AppID.IsNullOrEmpty()) Key = mi.AppID;
        if (!mi.Secret.IsNullOrEmpty()) Secret = mi.Secret;
        if (!mi.Scope.IsNullOrEmpty()) Scope = mi.Scope;
    }
    #endregion

    #region 1-跳转验证
    private String _redirect = String.Empty;
    private String _state = String.Empty;

    /// <summary>构建跳转验证地址</summary>
    /// <param name="redirect">验证完成后调整的目标地址</param>
    /// <param name="state">用户状态数据</param>
    /// <param name="baseUri">相对地址的基地址</param>
    /// <returns></returns>
    public virtual String Authorize(String redirect, String state = null!, Uri baseUri = null!)
    {
        if (redirect.IsNullOrEmpty()) throw new ArgumentNullException(nameof(redirect));

        if (Key.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Key), "未设置应用标识");
        if (Secret.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Secret), "未设置应用密钥");

        _redirect = redirect;
        _state = state ?? String.Empty;

        var url = GetUrl(AuthUrl);
        if (!_state.IsNullOrEmpty()) WriteLog("Authorize {0}", url);

        return url;
    }
    #endregion

    #region 2-获取访问令牌
    /// <summary>根据授权码获取访问令牌</summary>
    /// <param name="code"></param>
    /// <returns></returns>
    public virtual String? GetAccessToken(String code)
    {
        if (code.IsNullOrEmpty()) throw new ArgumentNullException(nameof(code), "未设置授权码");

        Code = code;

        var url = GetUrl(AccessUrl);
        WriteLog("GetAccessToken {0}", url);

        var html = Request(url);
        if (html.IsNullOrEmpty()) return null;

        html = html.Trim();
        if (Log != null && Log.Enable) WriteLog("{0}", html);

        var dic = GetNameValues(html);
        if (dic != null)
        {
            if (dic.TryGetValue("access_token", out var str)) AccessToken = str.Trim();
            if (dic.TryGetValue("expires_in", out str)) Expire = DateTime.Now.AddSeconds(str.Trim().ToInt());
            if (dic.TryGetValue("refresh_token", out str)) RefreshToken = str.Trim();

            if (UserUrl.IsNullOrEmpty() && dic.TryGetValue("scope", out str))
            {
                var ss = str.Trim().Split(',');
                if (ss.Any(e => e.EqualIgnoreCase("UserInfo")))
                {
                    UserUrl = "userinfo?access_token={token}";
                    LogoutUrl = "logout?client_id={key}&redirect_uri={redirect}&state={state}";
                }
            }

            OnGetInfo(dic);
        }
        Items = dic;

        return html;
    }
    #endregion

    #region 3-获取OpenID
    /// <summary>OpenID地址</summary>
    public String OpenIDUrl { get; set; }

    /// <summary>根据授权码获取访问令牌</summary>
    /// <returns></returns>
    public virtual String? GetOpenID()
    {
        if (AccessToken.IsNullOrEmpty()) throw new ArgumentNullException(nameof(AccessToken), "未设置授权码");

        var url = GetUrl(OpenIDUrl);
        WriteLog("GetOpenID {0}", url);

        var html = Request(url);
        if (html.IsNullOrEmpty()) return null;

        html = html.Trim();
        if (Log != null && Log.Enable) WriteLog("{0}", html);

        var dic = GetNameValues(html);
        if (dic != null)
        {
            if (dic.TryGetValue("expires_in", out var str)) Expire = DateTime.Now.AddSeconds(str.Trim().ToInt());
            if (dic.TryGetValue("openid", out str)) OpenID = str.Trim();

            OnGetInfo(dic);
        }
        Items = dic;

        return html;
    }
    #endregion

    #region 4-用户信息
    /// <summary>用户信息地址</summary>
    public String UserUrl { get; set; }

    /// <summary>用户ID</summary>
    public Int64 UserID { get; set; }

    /// <summary>用户名</summary>
    public String UserName { get; set; }

    /// <summary>昵称</summary>
    public String NickName { get; set; }

    /// <summary>用户代码</summary>
    public String UserCode { get; set; }

    /// <summary>手机</summary>
    public String Mobile { get; set; }

    /// <summary>邮箱</summary>
    public String Mail { get; set; }

    /// <summary>头像</summary>
    public String Avatar { get; set; }

    /// <summary>明细</summary>
    public String Detail { get; set; }

    /// <summary>获取用户信息</summary>
    /// <returns></returns>
    public virtual String? GetUserInfo()
    {
        var url = UserUrl;
        if (url.IsNullOrEmpty()) throw new ArgumentNullException(nameof(UserUrl), "未设置用户信息地址");

        url = GetUrl(url);
        WriteLog("GetUserInfo {0}", url);

        var html = Request(url);
        if (html.IsNullOrEmpty()) return null;

        html = html.Trim();
        if (Log != null && Log.Enable) WriteLog("{0}", html);

        var dic = GetNameValues(html);
        if (dic != null)
        {
            OnGetInfo(dic);

            if (Items == null)
                Items = dic;
            else
            {
                foreach (var item in dic)
                {
                    Items[item.Key] = item.Value;
                }
            }
        }

        return html;
    }
    #endregion

    #region 5-注销
    /// <summary>注销地址</summary>
    public String LogoutUrl { get; set; }

    /// <summary>注销</summary>
    /// <param name="redirect">完成后调整的目标地址</param>
    /// <param name="state">用户状态数据</param>
    /// <param name="baseUri">相对地址的基地址</param>
    /// <returns></returns>
    public virtual String Logout(String redirect = null!, String state = null!, Uri baseUri = null!)
    {
        var url = LogoutUrl;
        if (url.IsNullOrEmpty()) throw new ArgumentNullException(nameof(LogoutUrl), "未设置注销地址");

        _redirect = redirect ?? String.Empty;
        _state = state ?? String.Empty;

        url = GetUrl(url);
        WriteLog("Logout {0}", url);

        return url;
    }
    #endregion

    #region 辅助
    /// <summary>替换地址模版参数</summary>
    /// <param name="url"></param>
    /// <returns></returns>
    protected virtual String GetUrl(String url)
    {
        if (!url.StartsWithIgnoreCase("http"))
        {
            if (!AccessServer.IsNullOrEmpty() && !url.StartsWithIgnoreCase("auth"))
                url = AccessServer.EnsureEnd("/") + url.TrimStart('/');
            else
                url = Server.EnsureEnd("/") + url.TrimStart('/');
        }

        url = url
            .Replace("{key}", Key ?? String.Empty)
            .Replace("{secret}", Secret ?? String.Empty)
            .Replace("{response_type}", ResponseType ?? String.Empty)
            .Replace("{token}", AccessToken ?? String.Empty)
            .Replace("{code}", Code ?? String.Empty)
            .Replace("{openid}", OpenID ?? String.Empty)
            .Replace("{redirect}", WebUtility.UrlEncode(_redirect + String.Empty))
            .Replace("{scope}", Scope ?? String.Empty)
            .Replace("{state}", _state ?? String.Empty);

        return url;
    }

    /// <summary>获取名值字典</summary>
    /// <param name="html"></param>
    /// <returns></returns>
    protected virtual IDictionary<String, String>? GetNameValues(String html)
    {
        var p1 = html.IndexOf('{');
        var p2 = html.LastIndexOf('}');
        if (p1 > 0 && p2 > p1) html = html[p1..(p2 + 1)];

        if (p1 >= 0 && p2 > p1)
        {
            var source = html.DecodeJson();
            if (source == null || source.Count == 0) return null;

            var dic = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in source)
            {
                dic[item.Key] = item.Value + String.Empty;
            }

            return dic;
        }

        if (html.Contains('=') && html.Contains('&'))
        {
            var source = html.SplitAsDictionary("=", "&");
            return new Dictionary<String, String>(source, StringComparer.OrdinalIgnoreCase);
        }

        return null;
    }

    /// <summary>最后一次请求的响应内容</summary>
    public String LastHtml { get; set; } = String.Empty;

    private WebClientX? _client;

    /// <summary>创建客户端</summary>
    /// <param name="url">路径</param>
    /// <returns></returns>
    protected virtual String? Request(String url)
    {
        _client ??= new WebClientX();

        return LastHtml = _client.GetHtml(url);
    }

    /// <summary>从响应数据中获取信息</summary>
    /// <param name="dic"></param>
    protected virtual void OnGetInfo(IDictionary<String, String> dic)
    {
        if (dic.TryGetValue("openid", out var str)) OpenID = str.Trim();
        if (dic.TryGetValue("unionid", out str)) UnionID = str.Trim();

        if (dic.TryGetValue("uid", out str)) UserID = str.ToLong();
        if (dic.TryGetValue("userid", out str)) UserID = str.ToLong();
        if (dic.TryGetValue("user_id", out str)) UserID = str.ToLong();

        if (dic.TryGetValue("name", out str)) UserName = str.Trim();
        if (dic.TryGetValue("username", out str)) UserName = str.Trim();
        if (dic.TryGetValue("user_name", out str)) UserName = str.Trim();

        if (dic.TryGetValue("nickname", out str)) NickName = str.Trim();
        if (dic.TryGetValue("nick_name", out str)) NickName = str.Trim();

        if (dic.TryGetValue("code", out str) && Code.IsNullOrEmpty()) Code = str.Trim();
        if (dic.TryGetValue("mobile", out str)) Mobile = str.Trim();
        if (dic.TryGetValue("mail", out str)) Mail = str.Trim();
        if (dic.TryGetValue("email", out str) && Mail.IsNullOrEmpty()) Mail = str.Trim();
        if (dic.TryGetValue("avatar", out str)) Avatar = str.Trim();

        Detail = dic.ToJson();

        if (dic.TryGetValue("error", out str)) throw new InvalidOperationException(str);
    }
    #endregion

    #region 日志
    /// <summary>日志</summary>
    public ILog? Log { get; set; }

    /// <summary>写日志</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public void WriteLog(String format, params Object?[] args) => Log?.Info(format, args);
    #endregion
}