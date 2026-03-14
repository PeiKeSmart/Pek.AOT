using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

namespace Pek.Extension;

/// <summary>字符串辅助扩展</summary>
public static class StringHelper
{
    /// <summary>指示指定的字符串是 null 还是 String.Empty 字符串</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Boolean IsNullOrEmpty([NotNullWhen(false)] this String? value) => value == null || value.Length == 0;

    /// <summary>字符串转字节数组</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Byte[] GetBytes(this String? value, Encoding? encoding = null)
    {
        if (value.IsNullOrEmpty()) return [];
        encoding ??= Encoding.UTF8;
        return encoding.GetBytes(value);
    }
}