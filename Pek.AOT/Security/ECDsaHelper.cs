using System.Security.Cryptography;

using Pek.Extension;
using Pek.IO;

namespace Pek.Security;

/// <summary>椭圆曲线数字签名算法</summary>
public static class ECDsaHelper
{
    #region 生成密钥
    /// <summary>产生非对称密钥对</summary>
    /// <param name="keySize">密钥长度，默认521位强密钥</param>
    /// <returns></returns>
    public static String[] GenerateKey(Int32 keySize = 521)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("ECDsaHelper requires Windows CNG support.");

        using var dsa = new ECDsaCng(keySize);

        var ss = new String[2];
        ss[0] = dsa.Key.Export(CngKeyBlobFormat.EccPrivateBlob).ToBase64();
        ss[1] = dsa.Key.Export(CngKeyBlobFormat.EccPublicBlob).ToBase64();

        return ss;
    }

    /// <summary>创建ECDsa对象，支持Base64密钥和Pem密钥</summary>
    /// <param name="key"></param>
    /// <param name="privateKey"></param>
    /// <returns></returns>
    public static ECDsaCng? Create(String key, Boolean? privateKey = null)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("ECDsaHelper requires Windows CNG support.");

        if (key.IsNullOrWhiteSpace()) return null;
        key = key.Trim();

        if (key.StartsWith("-----", StringComparison.Ordinal) && key.EndsWith("-----", StringComparison.Ordinal))
        {
            var ek = ReadPem(key);

            var ec = new ECDsaCng();
            ec.ImportParameters(ek.ExportParameters());

            return ec;
        }
        else
        {
            var buf = FromBase64(key);
            var ckey =
                privateKey != null
                    ? CngKey.Import(buf, !privateKey.Value ? CngKeyBlobFormat.EccPublicBlob : CngKeyBlobFormat.EccPrivateBlob)
                    : CngKey.Import(buf, buf.Length < 100 ? CngKeyBlobFormat.EccPublicBlob : CngKeyBlobFormat.EccPrivateBlob);

            return new ECDsaCng(ckey);
        }
    }
    #endregion

    #region 数字签名
    /// <summary>签名，MD5散列</summary>
    /// <param name="data"></param>
    /// <param name="priKey"></param>
    /// <returns></returns>
    public static Byte[] Sign(Byte[] data, String priKey)
    {
        using var ecc = Create(priKey, true) ?? throw new ArgumentNullException(nameof(priKey));
        ecc.HashAlgorithm = CngAlgorithm.MD5;

        return ecc.SignData(data);
    }

    /// <summary>验证，MD5散列</summary>
    /// <param name="data"></param>
    /// <param name="pukKey"></param>
    /// <param name="rgbSignature"></param>
    /// <returns></returns>
    public static Boolean Verify(Byte[] data, String pukKey, Byte[] rgbSignature)
    {
        using var ecc = Create(pukKey, false) ?? throw new ArgumentNullException(nameof(pukKey));
        ecc.HashAlgorithm = CngAlgorithm.MD5;

        return ecc.VerifyData(data, rgbSignature);
    }

    /// <summary>Sha256</summary>
    /// <param name="data"></param>
    /// <param name="priKey"></param>
    /// <returns></returns>
    public static Byte[] SignSha256(this Byte[] data, String priKey)
    {
        using var ecc = Create(priKey, true) ?? throw new ArgumentNullException(nameof(priKey));
        ecc.HashAlgorithm = CngAlgorithm.Sha256;

        return ecc.SignData(data);
    }

    /// <summary>Sha256</summary>
    /// <param name="data"></param>
    /// <param name="pukKey"></param>
    /// <param name="rgbSignature"></param>
    /// <returns></returns>
    public static Boolean VerifySha256(this Byte[] data, String pukKey, Byte[] rgbSignature)
    {
        using var ecc = Create(pukKey, false) ?? throw new ArgumentNullException(nameof(pukKey));
        ecc.HashAlgorithm = CngAlgorithm.Sha256;

        return ecc.VerifyData(data, rgbSignature);
    }

    /// <summary>Sha384</summary>
    /// <param name="data"></param>
    /// <param name="priKey"></param>
    /// <returns></returns>
    public static Byte[] SignSha384(this Byte[] data, String priKey)
    {
        using var ecc = Create(priKey, true) ?? throw new ArgumentNullException(nameof(priKey));
        ecc.HashAlgorithm = CngAlgorithm.Sha384;

        return ecc.SignData(data);
    }

    /// <summary>Sha384</summary>
    /// <param name="data"></param>
    /// <param name="pukKey"></param>
    /// <param name="rgbSignature"></param>
    /// <returns></returns>
    public static Boolean VerifySha384(this Byte[] data, String pukKey, Byte[] rgbSignature)
    {
        using var ecc = Create(pukKey, false) ?? throw new ArgumentNullException(nameof(pukKey));
        ecc.HashAlgorithm = CngAlgorithm.Sha384;

        return ecc.VerifyData(data, rgbSignature);
    }

    /// <summary>Sha512</summary>
    /// <param name="data"></param>
    /// <param name="priKey"></param>
    /// <returns></returns>
    public static Byte[] SignSha512(this Byte[] data, String priKey)
    {
        using var ecc = Create(priKey, true) ?? throw new ArgumentNullException(nameof(priKey));
        ecc.HashAlgorithm = CngAlgorithm.Sha512;

        return ecc.SignData(data);
    }

    /// <summary>Sha512</summary>
    /// <param name="data"></param>
    /// <param name="pukKey"></param>
    /// <param name="rgbSignature"></param>
    /// <returns></returns>
    public static Boolean VerifySha512(this Byte[] data, String pukKey, Byte[] rgbSignature)
    {
        using var ecc = Create(pukKey, false) ?? throw new ArgumentNullException(nameof(pukKey));
        ecc.HashAlgorithm = CngAlgorithm.Sha512;

        return ecc.VerifyData(data, rgbSignature);
    }
    #endregion

    #region PEM
    /// <summary>读取PEM文件到RSA参数</summary>
    /// <param name="content"></param>
    /// <returns></returns>
    public static ECKey ReadPem(String content)
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

            var asn = Asn1.Read(data) ?? throw new InvalidDataException("Invalid ECC private key.");
            var keys = asn.Value as Asn1[] ?? throw new InvalidDataException("Invalid ECC private key structure.");

            var oids = asn.GetOids();
            if (oids.Length < 2) throw new InvalidDataException("Invalid ECC private key algorithm.");

            var algorithm = oids[0];
            var parameters = oids[1];

            if (algorithm.FriendlyName != "ECC") throw new InvalidDataException($"Invalid key {algorithm}");

            keys = Asn1.Read(keys[2].Value as Byte[] ?? throw new InvalidDataException("Invalid ECC private key body."))?.Value as Asn1[]
                ?? throw new InvalidDataException("Invalid ECC private key body.");

            var k2 = Asn1.Read(keys[2].Value as Byte[] ?? throw new InvalidDataException("Invalid ECC private key point."))?.Value as Byte[]
                ?? throw new InvalidDataException("Invalid ECC private key point.");
            var len = (k2.Length - 1) / 2;

            var ek = new ECKey
            {
                D = keys[1].Value as Byte[],
                X = k2.ReadBytes(1, len),
                Y = k2.ReadBytes(1 + len, len),
            };
            ek.SetAlgorithm(parameters, true);

            return ek;
        }
        else
        {
            var content2 = NormalizePem(content, "-----BEGIN PUBLIC KEY-----", "-----END PUBLIC KEY-----");

            var data = Convert.FromBase64String(content2);

            var asn = Asn1.Read(data) ?? throw new InvalidDataException("Invalid ECC public key.");
            var keys = asn.Value as Asn1[] ?? throw new InvalidDataException("Invalid ECC public key structure.");

            var oids = asn.GetOids();
            if (oids.Length < 2) throw new InvalidDataException("Invalid ECC public key algorithm.");

            var algorithm = oids[0];
            var parameters = oids[1];

            if (algorithm.FriendlyName != "ECC") throw new InvalidDataException($"Invalid key {algorithm}");

            var k2 = keys[1].Value as Byte[] ?? throw new InvalidDataException("Invalid ECC public key point.");
            var len = (k2.Length - 1) / 2;

            var ek = new ECKey
            {
                X = k2.ReadBytes(1, len),
                Y = k2.ReadBytes(1 + len, len),
            };
            ek.SetAlgorithm(parameters, false);

            return ek;
        }
    }

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

        return Convert.FromBase64String(value);
    }
    #endregion
}