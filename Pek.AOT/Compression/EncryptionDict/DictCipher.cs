using System.IO.Compression;
using System.Text;

namespace Pek.Compression.EncryptionDict;

/// <summary>字典加密器</summary>
public class DictCipher
{
    private readonly Dictionary<Char, Char> _encryptionDict;
    private readonly Dictionary<Char, Char> _decryptionDict;

    /// <summary>使用指定字典构造加密器</summary>
    /// <param name="encryptionDict">加密字典</param>
    /// <param name="decryptionDict">解密字典</param>
    public DictCipher(Dictionary<Char, Char> encryptionDict, Dictionary<Char, Char> decryptionDict)
    {
        if (encryptionDict == null)
        {
            // 定义加密字典，包括字母、数字和符号
            encryptionDict = new Dictionary<Char, Char>
        {
            {'A', 'X'}, {'B', 'Y'}, {'C', 'Z'}, {'D', 'A'},
            {'E', 'B'}, {'F', 'C'}, {'G', 'D'}, {'H', 'E'},
            {'I', 'F'}, {'J', 'G'}, {'K', 'H'}, {'L', 'I'},
            {'M', 'J'}, {'N', 'K'}, {'O', 'L'}, {'P', 'M'},
            {'Q', 'N'}, {'R', 'O'}, {'S', 'P'}, {'T', 'Q'},
            {'U', 'R'}, {'V', 'S'}, {'W', 'T'}, {'X', 'U'},
            {'Y', 'V'}, {'Z', 'W'}, {' ', '_'},
            {'0', '9'}, {'1', '8'}, {'2', '7'}, {'3', '6'},
            {'4', '5'}, {'5', '4'}, {'6', '3'}, {'7', '2'},
            {'8', '1'}, {'9', '0'},
            {'!', '@'}, {'@', '#'}, {'#', '$'}, {'$', '%'},
            {'%', '^'}, {'^', '&'}, {'&', '*'}, {'*', '('},
            {'(', ')'}, {')', '-'}, {'-', '='}, {'=', '+'}
        };
            // 创建解密字典
            decryptionDict = encryptionDict.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
        }

        _encryptionDict = encryptionDict;
        _decryptionDict = decryptionDict;
    }

    /// <summary>加密</summary>
    /// <param name="plaintext">明文</param>
    /// <returns>密文</returns>
    public String Encrypt(String plaintext)
    {
        return new String(plaintext.Select(c => _encryptionDict.ContainsKey(c) ? _encryptionDict[c] : c).ToArray());
    }

    /// <summary>压缩</summary>
    /// <param name="data">数据</param>
    /// <returns>压缩后的字节数组</returns>
    public Byte[] Compress(String data)
    {
        var bytes = Encoding.UTF8.GetBytes(data);
        using var ms = new MemoryStream();
        using var gzip = new GZipStream(ms, CompressionMode.Compress);
        gzip.Write(bytes, 0, bytes.Length);
        gzip.Close();
        return ms.ToArray();
    }

    /// <summary>解密</summary>
    /// <param name="ciphertext">密文</param>
    /// <returns>明文</returns>
    public String Decrypt(String ciphertext)
    {
        return new String(ciphertext.Select(c => _decryptionDict.ContainsKey(c) ? _decryptionDict[c] : c).ToArray());
    }
}
