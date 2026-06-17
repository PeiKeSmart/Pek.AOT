using System.Text;

namespace Pek.Helpers;

/// <summary>随机数操作管理类。AOT 安全版</summary>
public static class Randoms
{
    /// <summary>生成大小写字母+数字的随机字符串</summary>
    /// <param name="lens">长度</param>
    public static String RandomString(Int32 lens)
    {
        var chArray = new Char[]
        {
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q',
            'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z', 'A', 'B', 'C', 'D', 'E', 'F', 'G',
            'H', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X',
            'Y', 'Z', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9'
        };
        var length = chArray.Length;
        var str = "";
        var rnd = new Random();
        for (var i = 0; i < lens; i++)
        {
            str += chArray[rnd.Next(length)];
        }
        return str;
    }

    /// <summary>生成大写字母+数字的随机字符串</summary>
    /// <param name="lens">长度</param>
    public static String RandomStr(Int32 lens)
    {
        var chArray = new Char[]
        {
            'A', 'B', 'C', 'D', 'E', 'F', 'G',
            'H', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X',
            'Y', 'Z', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9'
        };
        var length = chArray.Length;
        var str = "";
        var rnd = new Random();
        for (var i = 0; i < lens; i++)
        {
            str += chArray[rnd.Next(length)];
        }
        return str;
    }

    /// <summary>从指定字符集中生成指定长度的随机字符串</summary>
    /// <param name="pwdchars">字符集</param>
    /// <param name="pwdlen">长度</param>
    public static String MakeRandomString(this String pwdchars, Int32 pwdlen)
    {
        var builder = new StringBuilder();
        var rnd = new Random();
        for (var i = 0; i < pwdlen; i++)
        {
            var num = rnd.Next(pwdchars.Length);
            builder.Append(pwdchars[num]);
        }
        return builder.ToString();
    }

    /// <summary>生成 0-9 随机数字串</summary>
    /// <param name="VcodeNum">生成长度</param>
    public static String RndNum(Int32 VcodeNum)
    {
        var sb = new StringBuilder(VcodeNum);
        var rnd = new Random();
        for (var i = 1; i < VcodeNum + 1; i++)
        {
            var t = rnd.Next(9);
            sb.AppendFormat("{0}", t);
        }
        return sb.ToString();
    }

    /// <summary>根据当前时间生成随机文件名</summary>
    public static String MakeFileRndName()
    {
        return DateTime.Now.ToString("yyyyMMddHHmmss") + MakeRandomString("0123456789", 4);
    }

    /// <summary>获取当前时间编号</summary>
    public static String GetNO()
    {
        return DateTime.Now.ToString("yyyyMMddhhmmss");
    }

    /// <summary>以日期为标准获得一个绝对的名称</summary>
    public static String MakeName()
    {
        return DateTime.Now.ToString("yyMMddHHmmss");
    }

    /// <summary>得到年月的文件夹名</summary>
    public static String MakeFolderName()
    {
        return DateTime.Now.ToString("yyyyMM");
    }

    /// <summary>获取一个由 26 个小写字母组成的指定长度随机字符串</summary>
    /// <param name="intLong">指定长度</param>
    public static String RandomSTR(Int32 intLong)
    {
        var str = "";
        var strArray = new String[]
        {
            "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z"
        };
        var random = new Random();
        for (var i = 0; i < intLong; i++)
        {
            str += strArray[random.Next(26)];
        }
        return str;
    }
}
