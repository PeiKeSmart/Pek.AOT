using System.Text;

namespace Pek.Helpers;

/// <summary>Base64帮助类</summary>
public class Base64Helper
{
    /// <summary>将Base64编码解析成字符串</summary>
    /// <param name="strbase">要解码的string字符</param>
    /// <param name="encoding">字符编码方案</param>
    /// <returns></returns>
    public static String Base64ToString(String strbase, Encoding encoding)
    {
        var buff = Convert.FromBase64String(strbase);
        return encoding.GetString(buff);
    }

    /// <summary>将Base64编码解析成字符串</summary>
    /// <param name="strbase">要解码的string字符</param>
    /// <returns></returns>
    public static String Base64ToString(String strbase) => Base64ToString(strbase, Encoding.UTF8);

    /// <summary>将Base64编码解析成字节数组</summary>
    /// <param name="strbase">要解码的string字符</param>
    /// <returns></returns>
    public static Byte[] Base64ToBytes(String strbase) => Convert.FromBase64String(strbase);

    /// <summary>将字节数组为Base64编码</summary>
    /// <param name="bytebase">要编码的byte[]</param>
    /// <returns></returns>
    public static String StringToBase64(Byte[] bytebase) => Convert.ToBase64String(bytebase);

    /// <summary>将字符串转为Base64编码</summary>
    /// <param name="str">要编码的string字符</param>
    /// <param name="encoding">字符编码方案</param>
    /// <returns></returns>
    public static String StringToBase64(String str, Encoding encoding)
    {
        var buff = encoding.GetBytes(str);
        return Convert.ToBase64String(buff);
    }

    /// <summary>将字符串转为Base64编码</summary>
    /// <param name="str">要编码的string字符</param>
    /// <returns></returns>
    public static String StringToBase64(String str)
    {
        var buff = Encoding.UTF8.GetBytes(str);
        return Convert.ToBase64String(buff);
    }
}
