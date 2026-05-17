using Pek.Extension;

namespace Pek.Web.OAuth;

/// <summary>Github 身份验证提供者</summary>
public class GithubClient : OAuthClient
{
    /// <summary>实例化</summary>
    public GithubClient()
    {
        Server = "https://github.com/login/oauth/";

        AuthUrl = "authorize?response_type={response_type}&client_id={key}&redirect_uri={redirect}&state={state}&scope={scope}";
        AccessUrl = "access_token?grant_type=authorization_code&client_id={key}&client_secret={secret}&code={code}&state={state}&redirect_uri={redirect}";
        UserUrl = "https://api.github.com/user?access_token={token}";
    }

    /// <summary>从响应数据中获取信息</summary>
    /// <param name="dic"></param>
    protected override void OnGetInfo(IDictionary<String, String> dic)
    {
        base.OnGetInfo(dic);

        if (dic.TryGetValue("id", out var str)) UserID = str.Trim('"').ToLong();
        if (dic.TryGetValue("login", out str)) UserName = str.Trim();
        if (dic.TryGetValue("name", out str)) NickName = str.Trim();
        if (dic.TryGetValue("avatar_url", out str)) Avatar = str.Trim();
        if (dic.TryGetValue("bio", out str)) Detail = str.Trim();
    }
}
