using Pek.Extension;

namespace Pek.Web.OAuth;

/// <summary>淘宝身份验证提供者</summary>
public class TaobaoClient : OAuthClient
{
    /// <summary>实例化</summary>
    public TaobaoClient()
    {
        var url = "https://oauth.taobao.com/";

        AuthUrl = url + "authorize?response_type={response_type}&client_id={key}&redirect_uri={redirect}&state={state}&scope={scope}";
        AccessUrl = url + "token?grant_type=authorization_code&client_id={key}&client_secret={secret}&code={code}&state={state}&redirect_uri={redirect}";
        LogoutUrl = url + "logoff?client_id={key}&view=web";
    }

    /// <summary>从响应数据中获取信息</summary>
    /// <param name="dic"></param>
    protected override void OnGetInfo(IDictionary<String, String> dic)
    {
        base.OnGetInfo(dic);

        if (dic.TryGetValue("taobao_user_id", out var str)) UserID = str.Trim('"').ToLong();
        if (dic.TryGetValue("taobao_user_nick", out str)) UserName = str.Trim();
    }
}
