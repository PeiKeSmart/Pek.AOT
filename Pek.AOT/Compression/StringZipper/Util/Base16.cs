using System.Text;

namespace Pek.Compression.StringZipper.Util;

/// <summary>Base16 编解码</summary>
public static class Base16
{
    /// <summary>编码为 Base16</summary>
    /// <param name="original">原始字节数组</param>
    /// <returns>Base16 编码字符串</returns>
    public static String ToBase16(Byte[] original)
    {
        var sb = new StringBuilder();
        foreach (var t in original)
        {
            sb.Append(t.ToString("X2"));
        }
        return sb.ToString();
    }

    /// <summary>从 Base16 解码</summary>
    /// <param name="base16">Base16 编码字符串</param>
    /// <returns>解码后的字节数组</returns>
    public static Byte[] FromBase16(String base16)
    {
        var bytes = new Byte[base16.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(base16.Substring(i * 2, 2), 16);
        }
        return bytes;
    }
}
