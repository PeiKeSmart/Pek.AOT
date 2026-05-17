using Pek.Extension;

namespace Pek.Web.OAuth;

/// <summary>百度身份验证提供者</summary>
public class BaiduClient : OAuthClient
{
    /// <summary>实例化</summary>
    public BaiduClient()
    {
        Server = "https://openapi.baidu.com/oauth/2.0/";

        AuthUrl = "authorize?response_type={response_type}&client_id={key}&redirect_uri={redirect}&state={state}&scope={scope}";
        AccessUrl = "token?grant_type=authorization_code&client_id={key}&client_secret={secret}&code={code}&state={state}&redirect_uri={redirect}";
        UserUrl = "https://openapi.baidu.com/rest/2.0/passport/users/getInfo?access_token={token}";
    }

    /// <summary>从响应数据中获取信息</summary>
    /// <param name="dic"></param>
    protected override void OnGetInfo(IDictionary<String, String> dic)
    {
        base.OnGetInfo(dic);

        if (dic.TryGetValue("uid", out var str)) UserID = str.Trim().ToLong();
        if (dic.TryGetValue("uname", out str)) UserName = str.Trim();
        if (dic.TryGetValue("realname", out str)) NickName = str.Trim();
        if (dic.TryGetValue("userdetail", out str)) Detail = str.Trim();

        if (dic.TryGetValue("sex", out str) && str.ToInt() == 0) dic["sex"] = "2";
        if (dic.TryGetValue("portrait", out str)) Avatar = "http://tb.himg.baidu.com/sys/portrait/item/" + str.Trim();
        if (!UserName.IsNullOrEmpty() && UserName.Contains('*')) UserName = String.Empty;
    }
}
