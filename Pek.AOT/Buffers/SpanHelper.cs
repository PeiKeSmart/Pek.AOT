using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

using Pek.Collections;

namespace Pek.Buffers;

/// <summary>Span帮助类</summary>
public static class SpanHelper
{
    private static readonly String HexChars = "0123456789ABCDEF";

    /// <summary>转字符串</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static String ToStr(this ReadOnlySpan<Byte> span, Encoding? encoding = null)
    {
        if (span.Length == 0) return String.Empty;

        return (encoding ?? Encoding.UTF8).GetString(span);
    }

    /// <summary>转字符串</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static String ToStr(this Span<Byte> span, Encoding? encoding = null)
    {
        if (span.Length == 0) return String.Empty;

        return (encoding ?? Encoding.UTF8).GetString(span);
    }

    /// <summary>获取字符串的字节数组写入数量</summary>
    public static unsafe Int32 GetBytes(this Encoding encoding, ReadOnlySpan<Char> chars, Span<Byte> bytes)
    {
        fixed (Char* charsPtr = &MemoryMarshal.GetReference(chars))
        fixed (Byte* bytesPtr = &MemoryMarshal.GetReference(bytes))
        {
            return encoding.GetBytes(charsPtr, chars.Length, bytesPtr, bytes.Length);
        }
    }

    /// <summary>获取字节数组的字符串</summary>
    public static unsafe String GetString(this Encoding encoding, ReadOnlySpan<Byte> bytes)
    {
        if (bytes.IsEmpty) return String.Empty;

        fixed (Byte* bytesPtr = &MemoryMarshal.GetReference(bytes))
        {
            return encoding.GetString(bytesPtr, bytes.Length);
        }
    }

    /// <summary>把字节数组编码为十六进制字符串</summary>
    public static String ToHex(this ReadOnlySpan<Byte> data)
    {
        if (data.Length == 0) return String.Empty;

        Span<Char> chars = stackalloc Char[data.Length * 2];
        for (Int32 i = 0, j = 0; i < data.Length; i++, j += 2)
        {
            var value = data[i];
            chars[j] = HexChars[value >> 4];
            chars[j + 1] = HexChars[value & 0x0F];
        }

        return chars.ToString();
    }

    /// <summary>把字节数组编码为十六进制字符串</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static String ToHex(this Span<Byte> data) => ((ReadOnlySpan<Byte>)data).ToHex();

    /// <summary>把字节数组编码为十六进制字符串</summary>
    public static String ToHex(this ReadOnlySpan<Byte> data, String? separate, Int32 groupSize = 0, Int32 maxLength = -1)
    {
        if (data.Length == 0 || maxLength == 0) return String.Empty;

        if (maxLength > 0 && data.Length > maxLength) data = data[..maxLength];
        if (String.IsNullOrEmpty(separate)) return data.ToHex();
        if (groupSize < 0) groupSize = 0;

        var builder = Pool.StringBuilder.Get();
        for (var i = 0; i < data.Length; i++)
        {
            if (i > 0 && (groupSize <= 0 || i % groupSize == 0)) builder.Append(separate);

            var value = data[i];
            builder.Append(HexChars[value >> 4]);
            builder.Append(HexChars[value & 0x0F]);
        }

        return builder.Return(true);
    }

    /// <summary>把字节数组编码为十六进制字符串</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static String ToHex(this Span<Byte> data, String? separate, Int32 groupSize = 0, Int32 maxLength = -1) => ((ReadOnlySpan<Byte>)data).ToHex(separate, groupSize, maxLength);
}