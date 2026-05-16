using System.Security.Cryptography;

using Pek.Configuration;
using Pek.Extension;
using Pek.IO;

namespace Pek.Security;

/// <summary>数据保护者。保护连接字符串中的密码</summary>
public class ProtectedKey
{
    #region 属性
    /// <summary>保护数据的密钥</summary>
    public Byte[]? Secret { get; set; }

    /// <summary>算法。默认AES</summary>
    public String Algorithm { get; set; } = "AES";

    /// <summary>隐藏字符串</summary>
    public String HideString { get; set; } = "{***}";

    /// <summary>密码名字</summary>
    public String[] Names { get; set; } = ["password", "pass", "pwd"];
    #endregion

    #region 静态实例
    /// <summary>全局实例。从环境变量和配置文件读取ProtectedKey密钥</summary>
    public static ProtectedKey Instance { get; set; }

    static ProtectedKey()
    {
        var pd = new ProtectedKey();

        var key = Pek.Runtime.GetEnvironmentVariable("ProtectedKey");
        if (key.IsNullOrEmpty())
        {
            var config = JsonConfigProvider.LoadAppSettings();
            key = config["ProtectedKey"];
        }

        if (!key.IsNullOrEmpty())
        {
            if (key.StartsWithIgnoreCase("$Base64$"))
                pd.Secret = FromBase64(key.Substring("$Base64$".Length));
            else if (key.StartsWithIgnoreCase("$Hex$"))
                pd.Secret = FromBase64(key.Substring("$Hex$".Length));
            else
                pd.Secret = key.GetBytes();
        }

        Instance = pd;
    }
    #endregion

    #region 方法
    /// <summary>保护连接字符串中的密码</summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public String Protect(String value)
    {
        using var alg = Create(Algorithm);

        var p = value.IndexOf('=');
        if (p < 0)
        {
            var pass = ToUrlBase64(alg.Encrypt(value.GetBytes(), Secret));
            return $"${Algorithm}${pass}";
        }

        var dic = value.SplitAsDictionary("=", ";", true);
        foreach (var item in Names)
        {
            if (dic.TryGetValue(item, out var pass))
            {
                if (pass.IsNullOrEmpty()) break;

                pass = ToUrlBase64(alg.Encrypt(pass.GetBytes(), Secret));
                dic[item] = $"${Algorithm}${pass}";

                return dic.Join(";", e => $"{e.Key}={e.Value}");
            }
        }

        return value;
    }

    /// <summary>解保护连接字符串中的密码</summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public String Unprotect(String value)
    {
        var p = value.IndexOf('=');
        if (p < 0)
        {
            var ss = value.Split('$');
            if (ss == null || ss.Length < 3) return value;

            using var alg = Create(ss[1]);
            return alg.Decrypt(FromBase64(ss[2]), Secret).ToStr();
        }

        var dic = value.SplitAsDictionary("=", ";");
        foreach (var item in Names)
        {
            if (dic.TryGetValue(item, out var pass))
            {
                if (pass.IsNullOrEmpty()) break;

                var ss = pass.Split('$');
                if (ss == null || ss.Length < 3) continue;

                using var alg = Create(ss[1]);
                dic[item] = alg.Decrypt(FromBase64(ss[2]), Secret).ToStr();

                return dic.Join(";", e => $"{e.Key}={e.Value}");
            }
        }

        return value;
    }

    /// <summary>隐藏连接字符串中的密码</summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public String Hide(String value)
    {
        var dic = value.SplitAsDictionary("=", ";");
        foreach (var item in Names)
        {
            if (dic.TryGetValue(item, out _))
            {
                dic[item] = HideString;
                return dic.Join(";", e => $"{e.Key}={e.Value}");
            }
        }

        return value;
    }

    static SymmetricAlgorithm Create(String name)
    {
        return name.ToLowerInvariant() switch
        {
            "aes" => Aes.Create(),
            "des" => DES.Create(),
            "rc2" => RC2.Create(),
            "tripledes" => TripleDES.Create(),
            _ => throw new NotSupportedException($"Not Supported [{name}]"),
        };
    }

    private static Byte[] FromBase64(String value)
    {
        if (value.IsNullOrWhiteSpace()) return [];

        value = value.Trim();
        if (value[^1] != '=')
        {
            var n = value.Length % 4;
            if (n > 0) value += new String('=', 4 - n);
        }

        value = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(value);
    }

    private static String ToUrlBase64(Byte[] data)
    {
        var value = Convert.ToBase64String(data);
        value = value.TrimEnd('=');
        value = value.Replace('+', '-');
        value = value.Replace('/', '_');
        return value;
    }
    #endregion
}