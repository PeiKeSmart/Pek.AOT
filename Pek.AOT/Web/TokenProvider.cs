using Pek;
using Pek.Extension;
using Pek.IO;
using Pek.Security;

namespace Pek.Web;

/// <summary>令牌提供者</summary>
/// <remarks>
/// 文档 https://newlifex.com/core/token_provider
/// </remarks>
public class TokenProvider
{
    #region 属性
    /// <summary>密钥。签发方用私钥，验证方用公钥</summary>
    public String? Key { get; set; }
    #endregion

    #region 方法
    /// <summary>读取密钥</summary>
    /// <param name="file">文件</param>
    /// <param name="generate">是否生成</param>
    /// <returns></returns>
    public Boolean ReadKey(String file, Boolean generate = false)
    {
        if (file.IsNullOrEmpty()) return false;

        file = file.GetBasePath();
        if (File.Exists(file))
        {
            Key = File.ReadAllText(file);

            if (!Key.IsNullOrEmpty()) return true;
        }

        if (!generate || !file.EndsWithIgnoreCase(".prvkey")) return false;

        var ss = DSAHelper.GenerateKey();
        File.WriteAllText(file.EnsureDirectory(true), ss[0]);
        file = Path.ChangeExtension(file, ".pubkey");
        File.WriteAllText(file, ss[1]);

        Key = ss[0];

        return true;
    }

    /// <summary>编码用户和有效期得到令牌</summary>
    /// <param name="user">用户</param>
    /// <param name="expire">有效期</param>
    /// <returns></returns>
    public String Encode(String user, DateTime expire)
    {
        if (user.IsNullOrEmpty()) throw new ArgumentNullException(nameof(user));
        if (expire.Year < 2000) throw new ArgumentOutOfRangeException(nameof(expire));
        if (Key.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Key));

        var secs = expire.ToUniversalTime().ToInt();

        var data = (user + "," + secs).GetBytes();
        var sig = DSAHelper.Sign(data, Key);

        return ToUrlBase64(data) + "." + ToUrlBase64(sig);
    }

    /// <summary>令牌解码得到用户和有效期</summary>
    /// <param name="token">令牌</param>
    /// <param name="expire">有效期</param>
    /// <returns></returns>
    [Obsolete("=>TryDecode")]
    public String? Decode(String token, out DateTime expire)
    {
        if (token.IsNullOrEmpty()) throw new ArgumentNullException(nameof(token));
        if (Key.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Key));

        expire = DateTime.MinValue;

        var p = token.IndexOf('.');
        var data = FromUrlBase64(token[..p]);
        var sig = FromUrlBase64(token[(p + 1)..]);

        if (!DSAHelper.Verify(data, Key, sig)) return null;

        var str = data.ToStr();
        p = str.LastIndexOf(',');

        var user = str[..p];
        var secs = str[(p + 1)..].ToInt();
        expire = secs.ToDateTime().ToLocalTime();

        return user;
    }

    /// <summary>尝试解码令牌，即使失败，也会返回用户信息和有效时间</summary>
    /// <param name="token">令牌</param>
    /// <param name="user">用户信息</param>
    /// <param name="expire">有效时间</param>
    /// <returns>解码结果，成功或失败</returns>
    public Boolean TryDecode(String token, out String user, out DateTime expire)
    {
        if (token.IsNullOrEmpty()) throw new ArgumentNullException(nameof(token));

        var p = token.IndexOf('.');
        var data = FromUrlBase64(token[..p]);
        var sig = FromUrlBase64(token[(p + 1)..]);

        var str = data.ToStr();
        p = str.LastIndexOf(',');

        user = str[..p];
        var secs = str[(p + 1)..].ToInt();
        expire = secs.ToDateTime().ToLocalTime();

        if (Key.IsNullOrEmpty()) return false;
        if (!DSAHelper.Verify(data, Key, sig)) return false;

        return true;
    }

    private static String ToUrlBase64(Byte[] data)
    {
        var value = Convert.ToBase64String(data);
        value = value.TrimEnd('=');
        value = value.Replace('+', '-');
        value = value.Replace('/', '_');
        return value;
    }

    private static Byte[] FromUrlBase64(String value)
    {
        value = value.Replace('-', '+');
        value = value.Replace('_', '/');

        var padding = value.Length % 4;
        if (padding > 0) value = value.PadRight(value.Length + 4 - padding, '=');

        return Convert.FromBase64String(value);
    }
    #endregion
}