using System.Security.Cryptography;
using System.Xml;

using Pek.Extension;
using Pek.IO;

namespace Pek.Security;

/// <summary>RSA算法</summary>
/// <remarks>RSA加密或签名小数据块时，密文长度固定且速度较快。</remarks>
public static class RSAHelper
{
    #region 加密解密
    /// <summary>产生非对称密钥对</summary>
    /// <param name="keySize">密钥长度，默认2048位强密钥</param>
    /// <returns>私钥和公钥</returns>
    public static String[] GenerateKey(Int32 keySize = 2048)
    {
        using var rsa = new RSACryptoServiceProvider(keySize);

        var ss = new String[2];
        ss[0] = rsa.ToXmlStringX(true);
        ss[1] = rsa.ToXmlStringX(false);

        return ss;
    }

    /// <summary>产生非对称参数密钥对</summary>
    /// <param name="keySize">密钥长度，默认2048位强密钥</param>
    /// <returns>私钥和公钥</returns>
    public static String[] GenerateParameters(Int32 keySize = 2048)
    {
        using var rsa = new RSACryptoServiceProvider(keySize);

        var ss = new String[2];
        ss[0] = WriteParameters(rsa.ExportParameters(true));
        ss[1] = WriteParameters(rsa.ExportParameters(false));

        return ss;
    }

    /// <summary>RSA参数转为Base64密钥</summary>
    /// <param name="p">RSA参数</param>
    /// <returns>UrlBase64格式的参数串</returns>
    public static String WriteParameters(RSAParameters p)
    {
        if (p.Modulus == null || p.Exponent == null) throw new ArgumentNullException(nameof(p));

        using var ms = new MemoryStream();
        ms.WriteArray(p.Modulus);
        ms.WriteArray(p.Exponent);

        if (p.D != null && p.D.Length > 0)
        {
            if (p.P == null || p.Q == null || p.DP == null || p.DQ == null || p.InverseQ == null)
                throw new ArgumentNullException(nameof(p));

            ms.WriteArray(p.D);
            ms.WriteArray(p.P);
            ms.WriteArray(p.Q);
            ms.WriteArray(p.DP);
            ms.WriteArray(p.DQ);
            ms.WriteArray(p.InverseQ);
        }

        return ToUrlBase64(ms.ToArray());
    }

    /// <summary>根据Base64密钥创建RSA参数</summary>
    /// <param name="key">Base64参数密钥</param>
    /// <returns>RSA参数</returns>
    public static RSAParameters ReadParameters(String key)
    {
        using var ms = new MemoryStream(FromBase64(key));

        var p = new RSAParameters
        {
            Modulus = ms.ReadArray(),
            Exponent = ms.ReadArray(),
        };

        if (ms.Position < ms.Length)
        {
            p.D = ms.ReadArray();
            p.P = ms.ReadArray();
            p.Q = ms.ReadArray();
            p.DP = ms.ReadArray();
            p.DQ = ms.ReadArray();
            p.InverseQ = ms.ReadArray();
        }

        return p;
    }

    /// <summary>创建RSA对象，支持Xml密钥和Pem密钥</summary>
    /// <param name="key">密钥内容</param>
    /// <returns>RSA对象</returns>
    public static RSACryptoServiceProvider Create(String key)
    {
        key = key.Trim();
        if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

        var rsa = new RSACryptoServiceProvider();
        if (key.StartsWith("<RSAKeyValue>", StringComparison.Ordinal) && key.EndsWith("</RSAKeyValue>", StringComparison.Ordinal))
            rsa.FromXmlStringX(key);
        else if (key.StartsWith("--", StringComparison.Ordinal) || key.Contains('\r') || key.Contains('\n'))
            rsa.ImportParameters(ReadPem(key));
        else
            rsa.ImportParameters(ReadParameters(key));

        return rsa;
    }

    /// <summary>RSA公钥加密。仅用于加密少量数据</summary>
    /// <param name="data">数据明文</param>
    /// <param name="pubKey">公钥</param>
    /// <param name="fOAEP">true 使用 OAEP 填充，否则使用 PKCS#1 v1.5</param>
    /// <returns>密文</returns>
    public static Byte[] Encrypt(Byte[] data, String pubKey, Boolean fOAEP = true)
    {
        using var rsa = Create(pubKey);
        return rsa.Encrypt(data, fOAEP);
    }

    /// <summary>RSA私钥解密。仅用于加密少量数据</summary>
    /// <param name="data">数据密文</param>
    /// <param name="priKey">私钥</param>
    /// <param name="fOAEP">true 使用 OAEP 填充，否则使用 PKCS#1 v1.5</param>
    /// <returns>明文</returns>
    public static Byte[] Decrypt(Byte[] data, String priKey, Boolean fOAEP = true)
    {
        using var rsa = Create(priKey);
        return rsa.Decrypt(data, fOAEP);
    }
    #endregion

    #region 数字签名
    /// <summary>签名，MD5散列</summary>
    /// <param name="data">原始数据</param>
    /// <param name="priKey">私钥</param>
    /// <returns>签名结果</returns>
    public static Byte[] Sign(Byte[] data, String priKey)
    {
        using var rsa = Create(priKey);
        using var md5 = MD5.Create();

        return rsa.SignData(data, md5);
    }

    /// <summary>验证，MD5散列</summary>
    /// <param name="data">原始数据</param>
    /// <param name="pukKey">公钥</param>
    /// <param name="rgbSignature">签名结果</param>
    /// <returns>是否通过</returns>
    public static Boolean Verify(Byte[] data, String pukKey, Byte[] rgbSignature)
    {
        using var rsa = Create(pukKey);
        using var md5 = MD5.Create();

        return rsa.VerifyData(data, md5, rgbSignature);
    }

    /// <summary>RS256</summary>
    /// <param name="data">原始数据</param>
    /// <param name="priKey">私钥</param>
    /// <returns>签名结果</returns>
    public static Byte[] SignSha256(this Byte[] data, String priKey)
    {
        using var rsa = Create(priKey);
        using var sha256 = SHA256.Create();

        return rsa.SignData(data, sha256);
    }

    /// <summary>RS256</summary>
    /// <param name="data">原始数据</param>
    /// <param name="pukKey">公钥</param>
    /// <param name="rgbSignature">签名结果</param>
    /// <returns>是否通过</returns>
    public static Boolean VerifySha256(this Byte[] data, String pukKey, Byte[] rgbSignature)
    {
        using var rsa = Create(pukKey);
        using var sha256 = SHA256.Create();

        return rsa.VerifyData(data, sha256, rgbSignature);
    }

    /// <summary>RS384</summary>
    /// <param name="data">原始数据</param>
    /// <param name="priKey">私钥</param>
    /// <returns>签名结果</returns>
    public static Byte[] SignSha384(this Byte[] data, String priKey)
    {
        using var rsa = Create(priKey);
        using var sha384 = SHA384.Create();

        return rsa.SignData(data, sha384);
    }

    /// <summary>RS384</summary>
    /// <param name="data">原始数据</param>
    /// <param name="pukKey">公钥</param>
    /// <param name="rgbSignature">签名结果</param>
    /// <returns>是否通过</returns>
    public static Boolean VerifySha384(this Byte[] data, String pukKey, Byte[] rgbSignature)
    {
        using var rsa = Create(pukKey);
        using var sha384 = SHA384.Create();

        return rsa.VerifyData(data, sha384, rgbSignature);
    }

    /// <summary>RS512</summary>
    /// <param name="data">原始数据</param>
    /// <param name="priKey">私钥</param>
    /// <returns>签名结果</returns>
    public static Byte[] SignSha512(this Byte[] data, String priKey)
    {
        using var rsa = Create(priKey);
        using var sha512 = SHA512.Create();

        return rsa.SignData(data, sha512);
    }

    /// <summary>RS512</summary>
    /// <param name="data">原始数据</param>
    /// <param name="pukKey">公钥</param>
    /// <param name="rgbSignature">签名结果</param>
    /// <returns>是否通过</returns>
    public static Boolean VerifySha512(this Byte[] data, String pukKey, Byte[] rgbSignature)
    {
        using var rsa = Create(pukKey);
        using var sha512 = SHA512.Create();

        return rsa.VerifyData(data, sha512, rgbSignature);
    }

    /// <summary>RS1，SHA1散列</summary>
    /// <param name="data">原始数据</param>
    /// <param name="priKey">私钥</param>
    /// <returns>签名结果</returns>
    public static Byte[] SignSha1(this Byte[] data, String priKey)
    {
        using var rsa = Create(priKey);
        using var sha1 = SHA1.Create();

        return rsa.SignData(data, sha1);
    }

    /// <summary>RS1，SHA1散列</summary>
    /// <param name="data">原始数据</param>
    /// <param name="pukKey">公钥</param>
    /// <param name="rgbSignature">签名结果</param>
    /// <returns>是否通过</returns>
    public static Boolean VerifySha1(this Byte[] data, String pukKey, Byte[] rgbSignature)
    {
        using var rsa = Create(pukKey);
        using var sha1 = SHA1.Create();

        return rsa.VerifyData(data, sha1, rgbSignature);
    }
    #endregion

    #region PEM
    /// <summary>读取PEM文件到RSA参数</summary>
    /// <param name="content">PEM内容</param>
    /// <returns>RSA参数</returns>
    public static RSAParameters ReadPem(String content)
    {
        if (String.IsNullOrEmpty(content)) throw new ArgumentNullException(nameof(content));

        content = content.Trim();
        if (content.StartsWithIgnoreCase("-----BEGIN RSA PRIVATE KEY-----", "-----BEGIN PRIVATE KEY-----"))
        {
            var content2 = NormalizePem(content,
                "-----BEGIN RSA PRIVATE KEY-----",
                "-----END RSA PRIVATE KEY-----",
                "-----BEGIN PRIVATE KEY-----",
                "-----END PRIVATE KEY-----");

            var data = Convert.FromBase64String(content2);

            var asn = Asn1.Read(data) ?? throw new InvalidDataException();
            var keys = asn.Value as Asn1[] ?? throw new InvalidDataException();

            var oids = asn.GetOids();
            if (oids.Any(e => e.FriendlyName == "RSA"))
            {
                var buf = keys[2].Value as Byte[];
                if (buf != null) keys = Asn1.Read(buf)?.Value as Asn1[];
            }

            if (keys == null) throw new InvalidDataException();

            return new RSAParameters
            {
                Modulus = keys[1].GetByteArray(true),
                Exponent = keys[2].GetByteArray(false),
                D = keys[3].GetByteArray(true),
                P = keys[4].GetByteArray(true),
                Q = keys[5].GetByteArray(true),
                DP = keys[6].GetByteArray(true),
                DQ = keys[7].GetByteArray(true),
                InverseQ = keys[8].GetByteArray(true)
            };
        }
        else
        {
            var content2 = NormalizePem(content, "-----BEGIN PUBLIC KEY-----", "-----END PUBLIC KEY-----");

            var data = Convert.FromBase64String(content2);

            var asn = Asn1.Read(data) ?? throw new InvalidDataException();
            var keys = asn.Value as Asn1[] ?? throw new InvalidDataException();

            var oids = asn.GetOids();
            if (oids.Any(e => e.FriendlyName == "RSA"))
            {
                var buf = keys.FirstOrDefault(e => e.Tag == Asn1Tags.BitString)?.Value as Byte[];
                if (buf != null) keys = Asn1.Read(buf)?.Value as Asn1[];
            }

            if (keys == null) throw new InvalidDataException();

            return new RSAParameters
            {
                Modulus = keys[0].GetByteArray(true),
                Exponent = keys[1].GetByteArray(false),
            };
        }
    }
    #endregion

    #region 辅助
    /// <summary>从Xml加载RSA密钥</summary>
    /// <param name="rsa">RSA对象</param>
    /// <param name="xmlString">XML密钥</param>
    public static void FromXmlStringX(this RSACryptoServiceProvider rsa, String xmlString)
    {
        var parameters = new RSAParameters();

        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xmlString);

        if (xmlDoc.DocumentElement == null || !xmlDoc.DocumentElement.Name.Equals("RSAKeyValue", StringComparison.Ordinal))
            throw new Exception("Invalid XML RSA key.");

        foreach (var item in xmlDoc.DocumentElement.ChildNodes)
        {
            if (item is not XmlNode node) continue;
            switch (node.Name)
            {
                case "Modulus": parameters.Modulus = ReadXmlParameter(node.InnerText); break;
                case "Exponent": parameters.Exponent = ReadXmlParameter(node.InnerText); break;
                case "P": parameters.P = ReadXmlParameter(node.InnerText); break;
                case "Q": parameters.Q = ReadXmlParameter(node.InnerText); break;
                case "DP": parameters.DP = ReadXmlParameter(node.InnerText); break;
                case "DQ": parameters.DQ = ReadXmlParameter(node.InnerText); break;
                case "InverseQ": parameters.InverseQ = ReadXmlParameter(node.InnerText); break;
                case "D": parameters.D = ReadXmlParameter(node.InnerText); break;
            }
        }

        rsa.ImportParameters(parameters);
    }

    /// <summary>保存RSA密钥到Xml</summary>
    /// <param name="rsa">RSA对象</param>
    /// <param name="includePrivateParameters">是否包含私钥部分</param>
    /// <returns>XML格式密钥</returns>
    public static String ToXmlStringX(this RSACryptoServiceProvider rsa, Boolean includePrivateParameters)
    {
        var parameters = rsa.ExportParameters(includePrivateParameters);

        if (!includePrivateParameters)
        {
            return String.Format("<RSAKeyValue><Modulus>{0}</Modulus><Exponent>{1}</Exponent></RSAKeyValue>",
                WriteXmlParameter(parameters.Modulus),
                WriteXmlParameter(parameters.Exponent));
        }

        return String.Format("<RSAKeyValue><Modulus>{0}</Modulus><Exponent>{1}</Exponent><P>{2}</P><Q>{3}</Q><DP>{4}</DP><DQ>{5}</DQ><InverseQ>{6}</InverseQ><D>{7}</D></RSAKeyValue>",
            WriteXmlParameter(parameters.Modulus),
            WriteXmlParameter(parameters.Exponent),
            WriteXmlParameter(parameters.P),
            WriteXmlParameter(parameters.Q),
            WriteXmlParameter(parameters.DP),
            WriteXmlParameter(parameters.DQ),
            WriteXmlParameter(parameters.InverseQ),
            WriteXmlParameter(parameters.D));
    }

    private static Byte[]? ReadXmlParameter(String value) => String.IsNullOrEmpty(value) ? null : Convert.FromBase64String(value);

    private static String? WriteXmlParameter(Byte[]? value) => value == null ? null : Convert.ToBase64String(value);

    private static String NormalizePem(String content, params String[] markers)
    {
        foreach (var item in markers)
        {
            content = content.Replace(item, null, StringComparison.Ordinal);
        }

        return content.Replace("\n", null, StringComparison.Ordinal)
            .Replace("\r", null, StringComparison.Ordinal)
            .Trim();
    }

    private static Byte[] FromBase64(String value)
    {
        if (String.IsNullOrWhiteSpace(value)) return [];

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