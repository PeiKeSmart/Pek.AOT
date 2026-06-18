using System;
using System.Security.Cryptography;

namespace Pek.Security;

/// <summary>加密随机数生成器，基于 System.Security.Cryptography.RandomNumberGenerator</summary>
public class SecureRandomNumberGenerator : RandomNumberGenerator
{
    #region 字段
    private Boolean _disposed;
    private readonly RandomNumberGenerator _rng;
    #endregion

    #region 构造
    /// <summary>实例化安全随机数生成器</summary>
    public SecureRandomNumberGenerator()
    {
        _rng = Create();
    }
    #endregion

    #region 方法
    /// <summary>生成随机整数</summary>
    /// <returns>非负随机整数</returns>
    public Int32 Next()
    {
        var data = new Byte[sizeof(Int32)];
        _rng.GetBytes(data);
        return BitConverter.ToInt32(data, 0) & (Int32.MaxValue - 1);
    }

    /// <summary>生成指定范围随机整数</summary>
    /// <param name="maxValue">上限（不含）</param>
    /// <returns>随机整数</returns>
    public Int32 Next(Int32 maxValue) => Next(0, maxValue);

    /// <summary>生成指定范围随机整数</summary>
    /// <param name="minValue">下限（含）</param>
    /// <param name="maxValue">上限（不含）</param>
    /// <returns>随机整数</returns>
    public Int32 Next(Int32 minValue, Int32 maxValue)
    {
        if (minValue > maxValue) throw new ArgumentOutOfRangeException(nameof(minValue));

        return (Int32)Math.Floor(minValue + (maxValue - minValue) * NextDouble());
    }

    /// <summary>生成随机双精度浮点数 [0.0, 1.0)</summary>
    /// <returns>随机双精度数</returns>
    public Double NextDouble()
    {
        var data = new Byte[sizeof(UInt32)];
        _rng.GetBytes(data);
        var randUint = BitConverter.ToUInt32(data, 0);
        return randUint / (UInt32.MaxValue + 1.0);
    }

    /// <summary>填充随机字节</summary>
    /// <param name="data">目标字节数组</param>
    public override void GetBytes(Byte[] data)
    {
        _rng.GetBytes(data);
    }

    /// <summary>填充非零随机字节</summary>
    /// <param name="data">目标字节数组</param>
    public override void GetNonZeroBytes(Byte[] data)
    {
        _rng.GetNonZeroBytes(data);
    }

    /// <summary>释放资源</summary>
    public new void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>释放资源</summary>
    /// <param name="disposing">是否释放托管资源</param>
    protected override void Dispose(Boolean disposing)
    {
        if (_disposed) return;

        if (disposing) _rng?.Dispose();

        _disposed = true;
        base.Dispose(disposing);
    }
    #endregion
}
