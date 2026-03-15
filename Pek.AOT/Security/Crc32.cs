namespace Pek.Security;

/// <summary>CRC32校验</summary>
/// <remarks>
/// Generate a table for a byte-wise 32-bit CRC calculation on the polynomial:
/// x^32+x^26+x^23+x^22+x^16+x^12+x^11+x^10+x^8+x^7+x^5+x^4+x^2+x+1.
///
/// Polynomials over GF(2) are represented in binary, one bit per coefficient,
/// with the lowest powers in the most significant bit. Then adding polynomials
/// is just exclusive-or, and multiplying a polynomial by x is a right shift by
/// one.
///
/// The table is simply the CRC of all possible eight bit values. This is all
/// the information needed to generate CRCs on data a byte at a time for all
/// combinations of CRC register values and incoming bytes.
/// </remarks>
public sealed class Crc32
{
    private const UInt32 CrcSeed = 0xFFFFFFFF;

    /// <summary>校验表</summary>
    public static UInt32[] Table { get; }

    static Crc32()
    {
        Table = new UInt32[256];
        const UInt32 kPoly = 0xEDB88320;
        for (UInt32 i = 0; i < 256; i++)
        {
            var value = i;
            for (var j = 0; j < 8; j++)
            {
                if ((value & 1) != 0)
                    value = (value >> 1) ^ kPoly;
                else
                    value >>= 1;
            }

            Table[i] = value;
        }
    }

    private UInt32 _crc = CrcSeed;

    /// <summary>校验值</summary>
    public UInt32 Value { get => _crc ^ CrcSeed; set => _crc = value ^ CrcSeed; }

    /// <summary>重置清零</summary>
    public Crc32 Reset()
    {
        _crc = CrcSeed;
        return this;
    }

    /// <summary>添加整数进行校验</summary>
    /// <param name="value">值</param>
    /// <returns>当前实例</returns>
    public Crc32 Update(Int32 value)
    {
        _crc = Table[(_crc ^ value) & 0xFF] ^ (_crc >> 8);
        return this;
    }

    /// <summary>添加字节数组进行校验</summary>
    /// <param name="buffer">缓冲区</param>
    /// <param name="offset">偏移量</param>
    /// <param name="count">数量</param>
    /// <returns>当前实例</returns>
    public Crc32 Update(Byte[] buffer, Int32 offset = 0, Int32 count = -1)
    {
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        if (count < 0) count = buffer.Length;
        if (offset < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));

        while (--count >= 0)
        {
            _crc = Table[(_crc ^ buffer[offset++]) & 0xFF] ^ (_crc >> 8);
        }

        return this;
    }

    /// <summary>添加数据区进行校验</summary>
    /// <param name="buffer">数据</param>
    /// <returns>当前实例</returns>
    public Crc32 Update(ReadOnlySpan<Byte> buffer)
    {
        for (var i = 0; i < buffer.Length; i++)
        {
            _crc = Table[(_crc ^ buffer[i]) & 0xFF] ^ (_crc >> 8);
        }

        return this;
    }

    /// <summary>添加数据流进行校验</summary>
    /// <param name="stream">流</param>
    /// <param name="count">数量</param>
    /// <returns>当前实例</returns>
    public Crc32 Update(Stream stream, Int64 count = -1)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        if (count <= 0) count = Int64.MaxValue;

        while (--count >= 0)
        {
            var value = stream.ReadByte();
            if (value == -1) break;

            _crc = Table[(_crc ^ value) & 0xFF] ^ (_crc >> 8);
        }

        return this;
    }

    /// <summary>计算校验码</summary>
    /// <param name="buffer">缓冲区</param>
    /// <param name="offset">偏移量</param>
    /// <param name="count">数量</param>
    /// <returns>校验值</returns>
    public static UInt32 Compute(Byte[] buffer, Int32 offset = 0, Int32 count = -1)
    {
        var crc = new Crc32();
        crc.Update(buffer, offset, count);
        return crc.Value;
    }

    /// <summary>计算校验码</summary>
    /// <param name="buffer">数据</param>
    /// <returns>校验值</returns>
    public static UInt32 Compute(ReadOnlySpan<Byte> buffer)
    {
        var crc = new Crc32();
        crc.Update(buffer);
        return crc.Value;
    }

    /// <summary>计算数据流校验码</summary>
    /// <param name="stream">流</param>
    /// <param name="count">数量</param>
    /// <returns>校验值</returns>
    public static UInt32 Compute(Stream stream, Int32 count = -1)
    {
        var crc = new Crc32();
        crc.Update(stream, count);
        return crc.Value;
    }

    /// <summary>计算数据流校验码，指定起始位置和字节数偏移量</summary>
    /// <remarks>
    /// 一般用于计算数据包校验码，需要回过头去开始校验，并且可能需要跳过最后的校验码长度。
    /// position 小于 0 时，数据流从当前位置开始计算校验；
    /// position 大于等于 0 时，数据流移到该位置开始计算，最后由 count 决定可能差几个字节不参与计算。
    /// </remarks>
    /// <param name="stream">数据流</param>
    /// <param name="position">如果大于等于 0，则表示从该位置开始计算</param>
    /// <param name="count">字节数偏移量，一般用负数表示</param>
    /// <returns>校验值</returns>
    public static UInt32 ComputeRange(Stream stream, Int64 position = -1, Int32 count = -1)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        if (position >= 0)
        {
            if (count > 0) count = -count;
            count += (Int32)(stream.Position - position);
            if (count == 0) return 0;

            stream.Position = position;
        }

        var crc = new Crc32();
        crc.Update(stream, count);
        return crc.Value;
    }
}