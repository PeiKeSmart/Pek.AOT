using System;
using System.Text;

using Pek.Collections;

namespace Pek.IO;

/// <summary>十六进制转换工具</summary>
public class HexHelper
{
    #region Hex字符串与字节数组转换
    /// <summary>字节数组转为十六进制字符串</summary>
    /// <param name="data">字节数组</param>
    /// <returns>十六进制字符串</returns>
    public static String ByteToHexString(Byte[] data) => ByteToHexString(data, '\0');

    /// <summary>字节数组转为十六进制字符串（带分隔符）</summary>
    /// <param name="data">字节数组</param>
    /// <param name="segment">分隔符，0 表示无分隔</param>
    /// <returns>十六进制字符串</returns>
    public static String ByteToHexString(Byte[] data, Char segment)
    {
        var sb = Pool.StringBuilder.Get();
        try
        {
            foreach (var b in data)
            {
                if (segment == 0)
                    sb.Append($"{b:X2}");
                else
                    sb.Append($"{b:X2}{segment}");
            }

            if (segment != 0 && sb.Length > 1 && sb[sb.Length - 1] == segment)
                sb.Remove(sb.Length - 1, 1);

            return sb.ToString();
        }
        finally
        {
            Pool.StringBuilder.Return(sb);
        }
    }

    /// <summary>字符串转十六进制字符串（Unicode 编码）</summary>
    /// <param name="value">输入字符串</param>
    /// <returns>十六进制字符串</returns>
    public static String ByteToHexString(String value) => ByteToHexString(Encoding.Unicode.GetBytes(value));

    /// <summary>十六进制字符串转字节数组</summary>
    /// <param name="hexString">十六进制字符串</param>
    /// <returns>字节数组，输入为空时返回 null</returns>
    public static Byte[]? HexStringToBytes(String hexString)
    {
        if (String.IsNullOrEmpty(hexString)) return null;

        hexString = hexString.ToUpper();
        var length = hexString.Length / 2;
        var hexChars = hexString.ToCharArray();
        var result = new Byte[length];
        for (var i = 0; i < length; i++)
        {
            var pos = i * 2;
            result[i] = (Byte)(CharToByte(hexChars[pos]) << 4 | CharToByte(hexChars[pos + 1]));
        }
        return result;
    }

    private static Byte CharToByte(Char c) => (Byte)"0123456789ABCDEF".IndexOf(c);
    #endregion
}
