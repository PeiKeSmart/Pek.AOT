using Pek.Net;
using Pek.Security;
using Pek.Extension;

namespace Pek.Model;

/// <summary>璁よ瘉鐢ㄦ埛鎺ュ彛锛屽叿鏈夌櫥褰曢獙璇併€佹敞鍐屻€佸湪绾跨瓑鍩烘湰淇℃伅</summary>
public interface IAuthUser : IManageUser
{
    #region 灞炴€?
    /// <summary>瀵嗙爜</summary>
    String? Password { get; set; }

    ///// <summary>鍦ㄧ嚎</summary>
    //Boolean Online { get; set; }

    /// <summary>鐧诲綍娆℃暟</summary>
    Int32 Logins { get; set; }

    /// <summary>鏈€鍚庣櫥褰?/summary>
    DateTime LastLogin { get; set; }

    /// <summary>鏈€鍚庣櫥褰旾P</summary>
    String? LastLoginIP { get; set; }

    /// <summary>娉ㄥ唽鏃堕棿</summary>
    DateTime RegisterTime { get; set; }

    /// <summary>娉ㄥ唽IP</summary>
    String? RegisterIP { get; set; }
    #endregion

    /// <summary>淇濆瓨</summary>
    /// <returns></returns>
    Int32 Save();
}

/// <summary>鐢ㄦ埛鎺ュ彛宸ュ叿绫?/summary>
public static class ManageUserHelper
{
    /// <summary>姣旇緝瀵嗙爜鐩哥瓑</summary>
    /// <param name="user"></param>
    /// <param name="pass"></param>
    /// <returns></returns>
    public static Boolean CheckEqual(this IAuthUser user, String pass)
    {
        // 楠岃瘉瀵嗙爜
        if (user.Password != pass) throw new Exception($"Password error for user [{user}]");

        return true;
    }

    /// <summary>姣旇緝瀵嗙爜MD5</summary>
    /// <param name="user"></param>
    /// <param name="pass"></param>
    /// <returns></returns>
    public static Boolean CheckMD5(this IAuthUser user, String pass)
    {
        // 楠岃瘉瀵嗙爜
        if (user.Password != pass.MD5()) throw new Exception($"Password error for user [{user}]");

        return true;
    }

    /// <summary>姣旇緝瀵嗙爜RC4</summary>
    /// <param name="user"></param>
    /// <param name="pass"></param>
    /// <returns></returns>
    public static Boolean CheckRC4(this IAuthUser user, String pass)
    {
        // 瀵嗙爜鏈夌洂鍊煎拰瀵嗘枃涓ら儴鍒嗙粍鎴?
        var p = pass.Length / 2;
        var salt = pass[..p].ToHex();
        pass = pass[p..];

        // 楠岃瘉瀵嗙爜
        var tpass = user.Password.GetBytes();
        if (salt.RC4(tpass).ToHexString() != pass) throw new Exception($"Password error for user [{user}]");

        return true;
    }

    /// <summary>淇濆瓨鐧诲綍淇℃伅</summary>
    /// <param name="user"></param>
    /// <param name="session"></param>
    public static void SaveLogin(this IAuthUser user, INetSession session)
    {
        user.Logins++;
        user.LastLogin = Pek.Runtime.UtcNow.ToLocalTime().DateTime;

        if (session != null)
        {
            user.LastLoginIP = session.Remote?.Address + "";
            //// 閿€姣佹椂
            //session.OnDisposed += (s, e) =>
            //{
            //    user.Online = false;
            //    user.Save();
            //};
        }
        //else
        //    user.LastLoginIP = WebHelper.UserHost;

        //user.Online = true;
        user.Save();
    }

    /// <summary>淇濆瓨娉ㄥ唽淇℃伅</summary>
    /// <param name="user"></param>
    /// <param name="session"></param>
    public static void SaveRegister(this IAuthUser user, INetSession session)
    {
        //user.Registers++;
        user.RegisterTime = Pek.Runtime.UtcNow.ToLocalTime().DateTime;
        //user.RegisterIP = ns.Remote.EndPoint.Address + "";

        if (session != null)
        {
            user.RegisterIP = session.Remote?.Address + "";
            //// 閿€姣佹椂
            //session.OnDisposed += (s, e) =>
            //{
            //    user.Online = false;
            //    user.Save();
            //};
        }

        //user.Online = true;
        user.Save();
    }
}
