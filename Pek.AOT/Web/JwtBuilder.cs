using System.Diagnostics.CodeAnalysis;
using System.Text;

using Pek.Collections;
using Pek.Data;
using Pek.Extension;
using Pek.Security;
using Pek.Serialization;

namespace Pek.Web;

/// <summary>Jwt编码委托</summary>
/// <param name="data"></param>
/// <param name="secrect"></param>
/// <returns></returns>
public delegate Byte[] JwtEncodeDelegate(Byte[] data, String secrect);

/// <summary>Jwt解码委托</summary>
/// <param name="data"></param>
/// <param name="secrect"></param>
/// <param name="signature"></param>
/// <returns></returns>
public delegate Boolean JwtDecodeDelegate(Byte[] data, String secrect, Byte[] signature);

/// <summary>JSON Web Token</summary>
public class JwtBuilder : IExtend
{
    #region 属性
    /// <summary>颁发者</summary>
    public String? Issuer { get; set; }

    /// <summary>主体所有人。可以存放userid/roleid等，作为用户唯一标识</summary>
    public String? Subject { get; set; }

    /// <summary>受众</summary>
    public String? Audience { get; set; }

    /// <summary>有效期。默认2小时</summary>
    public DateTime Expire { get; set; } = Runtime.UtcNow.ToLocalTime().DateTime.AddHours(2);

    /// <summary>生效时间，在此之前是无效的</summary>
    public DateTime NotBefore { get; set; }

    /// <summary>颁发时间</summary>
    public DateTime IssuedAt { get; set; }

    /// <summary>标识</summary>
    public String? Id { get; set; }

    /// <summary>算法。默认HS256</summary>
    public String Algorithm { get; set; } = "HS256";

    /// <summary>令牌类型。默认JWT</summary>
    public String? Type { get; set; }

    /// <summary>密钥</summary>
    public String? Secret { get; set; }

    /// <summary>数据项</summary>
    public IDictionary<String, Object?> Items { get; private set; } = new Dictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>设置 或 获取 数据项</summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public Object? this[String key] { get => Items.TryGetValue(key, out var obj) ? obj : null; set => Items[key] = value; }
    #endregion

    #region 构造
    static JwtBuilder()
    {
        RegisterAlgorithm("HS256", static (d, s) => d.SHA256(s.GetBytes()), null);
        RegisterAlgorithm("HS384", static (d, s) => d.SHA384(s.GetBytes()), null);
        RegisterAlgorithm("HS512", static (d, s) => d.SHA512(s.GetBytes()), null);

        RegisterAlgorithm("RS256", RSAHelper.SignSha256, RSAHelper.VerifySha256);
        RegisterAlgorithm("RS384", RSAHelper.SignSha384, RSAHelper.VerifySha384);
        RegisterAlgorithm("RS512", RSAHelper.SignSha512, RSAHelper.VerifySha512);
    }
    #endregion

    #region JWT方法
    /// <summary>编码目标对象，生成令牌</summary>
    /// <param name="payload"></param>
    /// <returns></returns>
    public String Encode(Object payload)
    {
        if (payload == null) throw new ArgumentNullException(nameof(payload));
        if (Secret.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Secret));

        var now = Runtime.UtcNow.ToLocalTime().DateTime;

        var dic = GetPayloadDictionary(payload);
        if (!dic.ContainsKey("iss") && !Issuer.IsNullOrEmpty()) dic["iss"] = Issuer;
        if (!dic.ContainsKey("sub") && !Subject.IsNullOrEmpty()) dic["sub"] = Subject;
        if (!dic.ContainsKey("aud") && !Audience.IsNullOrEmpty()) dic["aud"] = Audience;
        if (!dic.ContainsKey("exp") && Expire.Year > 2000) dic["exp"] = Expire.ToUniversalTime().ToInt();
        if (!dic.ContainsKey("nbf") && NotBefore.Year > 2000) dic["nbf"] = NotBefore.ToUniversalTime().ToInt();
        if (!dic.ContainsKey("iat")) dic["iat"] = (IssuedAt.Year > 2000 ? IssuedAt : now).ToUniversalTime().ToInt();
        if (!dic.ContainsKey("jti") && !Id.IsNullOrEmpty()) dic["jti"] = Id;

        var alg = Algorithm ?? "HS256";
        Dictionary<String, Object?> hs = new(StringComparer.OrdinalIgnoreCase)
        {
            ["alg"] = alg,
        };
        if (!hs.ContainsKey("typ") && !Type.IsNullOrEmpty()) hs["typ"] = Type;
        var header = ToUrlBase64(hs.ToJson().GetBytes());

        var body = ToUrlBase64(dic.ToJson().GetBytes());

        var data = $"{header}.{body}".GetBytes();
        if (_encodes.TryGetValue(alg, out var enc) && enc != null)
        {
            var sign = enc(data, Secret);
            return $"{header}.{body}.{ToUrlBase64(sign)}";
        }

        throw new InvalidOperationException($"Unsupported algorithm [{alg}]");
    }

    /// <summary>分析令牌</summary>
    /// <param name="token"></param>
    /// <returns></returns>
    public String[]? Parse(String token)
    {
        var ts = token.Split('.');
        if (ts.Length != 3) return null;

        var headerJson = FromUrlBase64ToString(ts[0]);
        var header = headerJson.DecodeJson();
        if (header == null) return null;

        if (header.TryGetValue("alg", out var alg) && alg != null) Algorithm = alg + String.Empty;
        if (header.TryGetValue("typ", out var typ)) Type = typ + String.Empty;

        var bodyJson = FromUrlBase64ToString(ts[1]);
        var body = bodyJson.DecodeJson();
        if (body != null)
        {
            Items = new Dictionary<String, Object?>(body, StringComparer.OrdinalIgnoreCase);

            if (body.TryGetValue("iss", out var value)) Issuer = value + String.Empty;
            if (body.TryGetValue("sub", out value)) Subject = value + String.Empty;
            if (body.TryGetValue("aud", out value)) Audience = value + String.Empty;
            if (body.TryGetValue("exp", out value)) Expire = value.ToDateTime().ToLocalTime();
            if (body.TryGetValue("nbf", out value)) NotBefore = value.ToDateTime().ToLocalTime();
            if (body.TryGetValue("iat", out value)) IssuedAt = value.ToDateTime().ToLocalTime();
            if (body.TryGetValue("jti", out value)) Id = value + String.Empty;
        }

        return ts;
    }

    /// <summary>解码令牌</summary>
    /// <param name="token"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public Boolean TryDecode(String token, [NotNullWhen(false)] out String? message)
    {
        message = "JWT格式不正确";

        var ts = Parse(token);
        if (ts == null) return false;

        var now = Runtime.UtcNow.ToLocalTime().DateTime;
        if (Expire.Year > 2000 && Expire < now)
        {
            message = "令牌已过期";
            return false;
        }
        if (NotBefore.Year > 2000 && now < NotBefore)
        {
            message = "令牌未生效";
            return false;
        }
        if (Secret.IsNullOrEmpty())
        {
            message = "未设置密钥";
            return false;
        }

        message = null;

        var data = $"{ts[0]}.{ts[1]}".GetBytes();
        if (_decodes.TryGetValue(Algorithm, out var dec))
        {
            if (dec != null) return dec(data, Secret, FromUrlBase64(ts[2]));

            if (_encodes.TryGetValue(Algorithm, out var enc) && enc != null) return ToUrlBase64(enc(data, Secret)) == ts[2];
        }

        throw new InvalidOperationException($"Unsupported algorithm [{Algorithm}]");
    }
    #endregion

    #region 算法管理
    private static readonly Dictionary<String, JwtEncodeDelegate> _encodes = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<String, JwtDecodeDelegate?> _decodes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>注册算法的编解码实现</summary>
    /// <param name="algorithm"></param>
    /// <param name="encode"></param>
    /// <param name="decode"></param>
    public static void RegisterAlgorithm(String algorithm, JwtEncodeDelegate encode, JwtDecodeDelegate? decode)
    {
        _encodes[algorithm] = encode;
        _decodes[algorithm] = decode;
    }
    #endregion

    #region 辅助
    private static IDictionary<String, Object?> GetPayloadDictionary(Object payload)
    {
        if (payload is IDictionary<String, Object?> dictionary)
            return new Dictionary<String, Object?>(dictionary, StringComparer.OrdinalIgnoreCase);
        if (payload is IDictionarySource source)
            return new Dictionary<String, Object?>(source.ToDictionary(), StringComparer.OrdinalIgnoreCase);
        if (payload is IExtend extend)
            return new Dictionary<String, Object?>(extend.Items, StringComparer.OrdinalIgnoreCase);
        if (payload is String json)
            return json.DecodeJson() is { } data
                ? new Dictionary<String, Object?>(data, StringComparer.OrdinalIgnoreCase)
                : throw new NotSupportedException("JWT Json payload 解析失败。");

        throw new NotSupportedException($"JwtBuilder 仅支持 IDictionary<String, Object?>、IDictionarySource、IExtend 或 Json 字符串作为 payload。当前类型：{payload.GetType().FullName}");
    }

    private static String ToUrlBase64(Byte[] data)
    {
        var text = Convert.ToBase64String(data);
        return text.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static Byte[] FromUrlBase64(String data)
    {
        var text = data.Replace('-', '+').Replace('_', '/');
        var mod = text.Length % 4;
        if (mod > 0) text = text.PadRight(text.Length + 4 - mod, '=');
        return Convert.FromBase64String(text);
    }

    private static String FromUrlBase64ToString(String data) => Encoding.UTF8.GetString(FromUrlBase64(data));
    #endregion
}