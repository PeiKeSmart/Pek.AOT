using System.Security.Cryptography;

using Pek.Buffers;
using Pek.Serialization;

namespace Pek.Security;

/// <summary>椭圆曲线密钥</summary>
public class ECKey : IAccessor, ISpanSerializable
{
    #region 属性
    private AlgorithmKeyBlob _algorithm;

    /// <summary>算法</summary>
    public String Algorithm
    {
        get => _algorithm.ToString();
        set => _algorithm = (AlgorithmKeyBlob)Enum.Parse(typeof(AlgorithmKeyBlob), value);
    }

    /// <summary>坐标X</summary>
    public Byte[] X { get; set; } = [];

    /// <summary>坐标Y</summary>
    public Byte[] Y { get; set; } = [];

    /// <summary>私钥才有</summary>
    public Byte[]? D { get; set; }
    #endregion

    #region 方法
    /// <summary>设置算法参数</summary>
    /// <param name="oid"></param>
    /// <param name="privateKey"></param>
    public void SetAlgorithm(Oid oid, Boolean privateKey)
    {
        if (oid == null) throw new ArgumentNullException(nameof(oid));
        var friendlyName = oid.FriendlyName;
        if (String.IsNullOrEmpty(friendlyName)) throw new InvalidOperationException("Invalid ECC curve friendly name.");

        if (privateKey)
            Algorithm = friendlyName.Replace("_", "_PRIVATE_");
        else
            Algorithm = friendlyName.Replace("_", "_PUBLIC_");
    }
    #endregion

    #region 导入导出
    /// <summary>读取</summary>
    /// <param name="data"></param>
    public void Read(Byte[] data) => Read(new MemoryStream(data), null);

    /// <summary>转字节数组</summary>
    /// <returns></returns>
    public Byte[] ToArray()
    {
        using var ms = new MemoryStream();
        Write(ms, null);
        return ms.ToArray();
    }

    /// <summary>读取</summary>
    /// <param name="stream"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public Boolean Read(Stream stream, Object? context)
    {
        var reader = context as BinaryReader ?? new BinaryReader(stream);

        _algorithm = (AlgorithmKeyBlob)reader.ReadInt32();

        var len = reader.ReadInt32();
        X = reader.ReadBytes(len);
        Y = reader.ReadBytes(len);
        if (reader.BaseStream.Position < reader.BaseStream.Length) D = reader.ReadBytes(len);

        return true;
    }

    /// <summary>写入</summary>
    /// <param name="stream"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public Boolean Write(Stream stream, Object? context)
    {
        var writer = context as BinaryWriter ?? new BinaryWriter(stream);

        writer.Write((Int32)_algorithm);
        writer.Write(X.Length);
        writer.Write(X);
        writer.Write(Y);
        if (D != null && D.Length > 0) writer.Write(D);

        return true;
    }

    /// <summary>导出参数</summary>
    /// <returns></returns>
    public ECParameters ExportParameters()
    {
        return new ECParameters
        {
            D = D,
            Q = new ECPoint
            {
                X = X,
                Y = Y,
            },
            Curve = ECCurve.CreateFromFriendlyName(Algorithm.Replace("_PRIVATE_", "_").Replace("_PUBLIC_", "_")),
        };
    }

    /// <summary>写入到Span写入器</summary>
    /// <param name="writer">Span写入器</param>
    public void Write(ref SpanWriter writer)
    {
        writer.Write((Int32)_algorithm);
        writer.Write(X.Length);
        writer.Write(X);
        writer.Write(Y);
        if (D != null && D.Length > 0) writer.Write(D);
    }

    /// <summary>从Span读取器读取</summary>
    /// <param name="reader">Span读取器</param>
    public void Read(ref SpanReader reader)
    {
        _algorithm = (AlgorithmKeyBlob)reader.ReadInt32();

        var len = reader.ReadInt32();
        X = reader.ReadBytes(len).ToArray();
        Y = reader.ReadBytes(len).ToArray();
        if (reader.Available > 0) D = reader.ReadBytes(len).ToArray();
    }
    #endregion
}