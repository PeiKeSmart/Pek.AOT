namespace Pek.Security;

/// <summary>RC4对称加密算法</summary>
class RC4
{
    /// <summary>加密</summary>
    /// <param name="data">数据</param>
    /// <param name="pass">密码</param>
    /// <returns>加密结果</returns>
    public static Byte[] Encrypt(Byte[] data, Byte[] pass)
    {
        if (data == null || data.Length == 0) return [];
        if (pass == null || pass.Length == 0) return data;

        var output = new Byte[data.Length];
        var i = 0;
        var j = 0;
        var box = GetKey(pass, 256);
        for (var k = 0; k < data.Length; k++)
        {
            i = (i + 1) % box.Length;
            j = (j + box[i]) % box.Length;

            var temp = box[i];
            box[i] = box[j];
            box[j] = temp;

            var a = data[k];
            var b = box[(box[i] + box[j]) % box.Length];
            output[k] = (Byte)(a ^ b);
        }

        return output;
    }

    /// <summary>打乱密码</summary>
    /// <param name="pass">密码</param>
    /// <param name="length">密码箱长度</param>
    /// <returns>打乱后的密码箱</returns>
    private static Byte[] GetKey(Byte[] pass, Int32 length)
    {
        var box = new Byte[length];
        for (var i = 0; i < length; i++)
        {
            box[i] = (Byte)i;
        }

        var j = 0;
        for (var i = 0; i < length; i++)
        {
            j = (j + box[i] + pass[i % pass.Length]) % length;

            var temp = box[i];
            box[i] = box[j];
            box[j] = temp;
        }

        return box;
    }
}