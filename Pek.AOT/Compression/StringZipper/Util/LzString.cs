using System.Text;

namespace Pek.Compression.StringZipper.Util;

/// <summary>LZString 压缩算法</summary>
public class LzString
{
    private static Int32 GetBaseValue(String alphabet, Char character)
    {
        if (!LzString.baseReverseDic.ContainsKey(alphabet))
        {
            LzString.baseReverseDic[alphabet] = new Dictionary<Char, Int32>();
            for (var i = 0; i < alphabet.Length; i++)
            {
                LzString.baseReverseDic[alphabet][alphabet[i]] = i;
            }
        }
        return LzString.baseReverseDic[alphabet][character];
    }

    /// <summary>压缩为 Base64</summary>
    /// <param name="input">输入字符串</param>
    /// <returns>Base64 编码的压缩字符串</returns>
    public static String CompressToBase64(String input)
    {
        if (input == null)
        {
            return String.Empty;
        }
        var res = LzString.Compress(input, 6, (Int32 a) => LzString.keyStrBase64[a]);
        switch (res.Length % 4)
        {
            case 0:
                return res;
            case 1:
                return res + "===";
            case 2:
                return res + "==";
            case 3:
                return res + "=";
            default:
                return null;
        }
    }

    /// <summary>从 Base64 解压</summary>
    /// <param name="input">Base64 编码字符串</param>
    /// <returns>解压后的字符串</returns>
    public static String DecompressFromBase64(String input)
    {
        if (String.IsNullOrEmpty(input))
        {
            return String.Empty;
        }
        return LzString.Decompress(input.Length, 32, (Int32 index) => LzString.GetBaseValue(LzString.keyStrBase64, input[index]));
    }

    /// <summary>压缩为 UTF16</summary>
    /// <param name="input">输入字符串</param>
    /// <returns>UTF16 编码的压缩字符串</returns>
    public static String CompressToUTF16(String input)
    {
        if (input == null)
        {
            return String.Empty;
        }
        return LzString.Compress(input, 15, (Int32 a) => LzString.f(a + 32)) + " ";
    }

    /// <summary>从 UTF16 解压</summary>
    /// <param name="compressed">压缩字符串</param>
    /// <returns>解压后的字符串</returns>
    public static String DecompressFromUTF16(String compressed)
    {
        if (String.IsNullOrWhiteSpace(compressed))
        {
            return String.Empty;
        }
        return LzString.Decompress(compressed.Length, 16384, (Int32 index) => Convert.ToInt32(compressed[index]) - 32);
    }

    /// <summary>压缩为字节数组</summary>
    /// <param name="uncompressed">未压缩字符串</param>
    /// <returns>压缩后的字节数组</returns>
    public static Byte[] CompressToUint8Array(String uncompressed)
    {
        var compressed = LzString.Compress(uncompressed);
        var buf = new Byte[compressed.Length * 2];
        var i = 0;
        var TotalLen = compressed.Length;
        while (i < TotalLen)
        {
            var current_value = Convert.ToInt32(compressed[i]);
            buf[i * 2] = (Byte)((UInt32)current_value >> 8);
            buf[i * 2 + 1] = (Byte)(current_value % 256);
            i++;
        }
        return buf;
    }

    /// <summary>从字节数组解压</summary>
    /// <param name="compressed">压缩字节数组</param>
    /// <returns>解压后的字符串</returns>
    public static String DecompressFromUint8Array(Byte[] compressed)
    {
        if (compressed == null)
        {
            return String.Empty;
        }
        var buf = new Int32[compressed.Length / 2];
        var i = 0;
        var TotalLen = buf.Length;
        while (i < TotalLen)
        {
            buf[i] = (Int32)compressed[i * 2] * 256 + (Int32)compressed[i * 2 + 1];
            i++;
        }
        var result = new Char[buf.Length];
        for (var j = 0; j < buf.Length; j++)
        {
            result[j] = LzString.f(buf[j]);
        }
        return LzString.Decompress(new String(result));
    }

    /// <summary>压缩为 URI 安全字符串</summary>
    /// <param name="input">输入字符串</param>
    /// <returns>URI 安全的压缩字符串</returns>
    public static String CompressToEncodedURIComponent(String input)
    {
        if (String.IsNullOrWhiteSpace(input))
        {
            return String.Empty;
        }
        return LzString.Compress(input, 6, (Int32 a) => LzString.keyStrUriSafe[a]);
    }

    /// <summary>从 URI 安全字符串解压</summary>
    /// <param name="input">URI 安全的压缩字符串</param>
    /// <returns>解压后的字符串</returns>
    public static String DecompressFromEncodedURIComponent(String input)
    {
        if (String.IsNullOrWhiteSpace(input))
        {
            return String.Empty;
        }
        input = input.Replace(' ', '+');
        return LzString.Decompress(input.Length, 32, (Int32 index) => LzString.GetBaseValue(LzString.keyStrUriSafe, input[index]));
    }

    /// <summary>压缩</summary>
    /// <param name="uncompressed">未压缩字符串</param>
    /// <returns>压缩后的字符串</returns>
    public static String Compress(String uncompressed)
    {
        return LzString.Compress(uncompressed, 16, LzString.f);
    }

    private static String Compress(String uncompressed, Int32 bitsPerChar, LzString.GetCharFromInt getCharFromInt)
    {
        if (uncompressed == null)
        {
            return String.Empty;
        }
        var context_enlargeIn = 2;
        var context_dictSize = 3;
        var context_numBits = 2;
        var context_data_val = 0;
        var context_data_position = 0;
        var context_dictionaryToCreate = new Dictionary<String, Boolean>();
        var context_dictionary = new Dictionary<String, Int32>();
        var context_data = new StringBuilder();
        var context_c = String.Empty;
        var context_wc = String.Empty;
        var context_w = String.Empty;
        Int32 value;
        for (var ii = 0; ii < uncompressed.Length; ii++)
        {
            context_c = uncompressed[ii].ToString();
            if (!context_dictionary.ContainsKey(context_c))
            {
                context_dictionary[context_c] = context_dictSize++;
                context_dictionaryToCreate[context_c] = true;
            }
            context_wc = context_w + context_c;
            if (context_dictionary.ContainsKey(context_wc))
            {
                context_w = context_wc;
            }
            else
            {
                if (context_dictionaryToCreate.ContainsKey(context_w))
                {
                    if (Convert.ToInt32(context_w[0]) < 256)
                    {
                        for (var i = 0; i < context_numBits; i++)
                        {
                            context_data_val <<= 1;
                            if (context_data_position == bitsPerChar - 1)
                            {
                                context_data_position = 0;
                                context_data.Append(getCharFromInt(context_data_val));
                                context_data_val = 0;
                            }
                            else
                            {
                                context_data_position++;
                            }
                        }
                        value = Convert.ToInt32(context_w[0]);
                        for (var i = 0; i < 8; i++)
                        {
                            context_data_val = (context_data_val << 1 | (value & 1));
                            if (context_data_position == bitsPerChar - 1)
                            {
                                context_data_position = 0;
                                context_data.Append(getCharFromInt(context_data_val));
                                context_data_val = 0;
                            }
                            else
                            {
                                context_data_position++;
                            }
                            value >>= 1;
                        }
                    }
                    else
                    {
                        value = 1;
                        for (var i = 0; i < context_numBits; i++)
                        {
                            context_data_val = (context_data_val << 1 | value);
                            if (context_data_position == bitsPerChar - 1)
                            {
                                context_data_position = 0;
                                context_data.Append(getCharFromInt(context_data_val));
                                context_data_val = 0;
                            }
                            else
                            {
                                context_data_position++;
                            }
                            value = 0;
                        }
                        value = Convert.ToInt32(context_w[0]);
                        for (var i = 0; i < 16; i++)
                        {
                            context_data_val = (context_data_val << 1 | (value & 1));
                            if (context_data_position == bitsPerChar - 1)
                            {
                                context_data_position = 0;
                                context_data.Append(getCharFromInt(context_data_val));
                                context_data_val = 0;
                            }
                            else
                            {
                                context_data_position++;
                            }
                            value >>= 1;
                        }
                    }
                    context_enlargeIn--;
                    if (context_enlargeIn == 0)
                    {
                        context_enlargeIn = (Int32)Math.Pow(2.0, (Double)context_numBits);
                        context_numBits++;
                    }
                    context_dictionaryToCreate.Remove(context_w);
                }
                else
                {
                    value = context_dictionary[context_w];
                    for (var i = 0; i < context_numBits; i++)
                    {
                        context_data_val = (context_data_val << 1 | (value & 1));
                        if (context_data_position == bitsPerChar - 1)
                        {
                            context_data_position = 0;
                            context_data.Append(getCharFromInt(context_data_val));
                            context_data_val = 0;
                        }
                        else
                        {
                            context_data_position++;
                        }
                        value >>= 1;
                    }
                }
                context_enlargeIn--;
                if (context_enlargeIn == 0)
                {
                    context_enlargeIn = (Int32)Math.Pow(2.0, (Double)context_numBits);
                    context_numBits++;
                }
                context_dictionary[context_wc] = context_dictSize++;
                context_w = context_c;
            }
        }
        if (context_w != String.Empty)
        {
            if (context_dictionaryToCreate.ContainsKey(context_w))
            {
                if (Convert.ToInt32(context_w[0]) < 256)
                {
                    for (var i = 0; i < context_numBits; i++)
                    {
                        context_data_val <<= 1;
                        if (context_data_position == bitsPerChar - 1)
                        {
                            context_data_position = 0;
                            context_data.Append(getCharFromInt(context_data_val));
                            context_data_val = 0;
                        }
                        else
                        {
                            context_data_position++;
                        }
                    }
                    value = Convert.ToInt32(context_w[0]);
                    for (var i = 0; i < 8; i++)
                    {
                        context_data_val = (context_data_val << 1 | (value & 1));
                        if (context_data_position == bitsPerChar - 1)
                        {
                            context_data_position = 0;
                            context_data.Append(getCharFromInt(context_data_val));
                            context_data_val = 0;
                        }
                        else
                        {
                            context_data_position++;
                        }
                        value >>= 1;
                    }
                }
                else
                {
                    value = 1;
                    for (var i = 0; i < context_numBits; i++)
                    {
                        context_data_val = (context_data_val << 1 | value);
                        if (context_data_position == bitsPerChar - 1)
                        {
                            context_data_position = 0;
                            context_data.Append(getCharFromInt(context_data_val));
                            context_data_val = 0;
                        }
                        else
                        {
                            context_data_position++;
                        }
                        value = 0;
                    }
                    value = Convert.ToInt32(context_w[0]);
                    for (var i = 0; i < 16; i++)
                    {
                        context_data_val = (context_data_val << 1 | (value & 1));
                        if (context_data_position == bitsPerChar - 1)
                        {
                            context_data_position = 0;
                            context_data.Append(getCharFromInt(context_data_val));
                            context_data_val = 0;
                        }
                        else
                        {
                            context_data_position++;
                        }
                        value >>= 1;
                    }
                }
                context_enlargeIn--;
                if (context_enlargeIn == 0)
                {
                    context_enlargeIn = (Int32)Math.Pow(2.0, (Double)context_numBits);
                    context_numBits++;
                }
                context_dictionaryToCreate.Remove(context_w);
            }
            else
            {
                value = context_dictionary[context_w];
                for (var i = 0; i < context_numBits; i++)
                {
                    context_data_val = (context_data_val << 1 | (value & 1));
                    if (context_data_position == bitsPerChar - 1)
                    {
                        context_data_position = 0;
                        context_data.Append(getCharFromInt(context_data_val));
                        context_data_val = 0;
                    }
                    else
                    {
                        context_data_position++;
                    }
                    value >>= 1;
                }
            }
            if (context_enlargeIn - 1 == 0)
            {
                context_enlargeIn = (Int32)Math.Pow(2.0, (Double)context_numBits);
                context_numBits++;
            }
        }
        value = 2;
        for (var i = 0; i < context_numBits; i++)
        {
            context_data_val = (context_data_val << 1 | (value & 1));
            if (context_data_position == bitsPerChar - 1)
            {
                context_data_position = 0;
                context_data.Append(getCharFromInt(context_data_val));
                context_data_val = 0;
            }
            else
            {
                context_data_position++;
            }
            value >>= 1;
        }
        for (; ; )
        {
            context_data_val <<= 1;
            if (context_data_position == bitsPerChar - 1)
            {
                break;
            }
            context_data_position++;
        }
        context_data.Append(getCharFromInt(context_data_val));
        return context_data.ToString();
    }

    /// <summary>解压</summary>
    /// <param name="compressed">压缩字符串</param>
    /// <returns>解压后的字符串</returns>
    public static String Decompress(String compressed)
    {
        if (String.IsNullOrWhiteSpace(compressed))
        {
            return String.Empty;
        }
        return LzString.Decompress(compressed.Length, 32768, (Int32 index) => Convert.ToInt32(compressed[index]));
    }

    private static String Decompress(Int32 length, Int32 resetValue, LzString.GetNextValue getNextValue)
    {
        var dictionary = new Dictionary<Int32, String>();
        var enlargeIn = 4;
        var dictSize = 4;
        var numBits = 3;
        var c = 0;
        var entry = String.Empty;
        var result = new StringBuilder();
        var data = new LzString.DataStruct
        {
            val = getNextValue(0),
            position = resetValue,
            index = 1
        };
        for (var i = 0; i < 3; i++)
        {
            dictionary[i] = Convert.ToChar(i).ToString();
        }
        var bits = 0;
        var maxpower = (Int32)Math.Pow(2.0, 2.0);
        for (var power = 1; power != maxpower; power <<= 1)
        {
            var resb = data.val & data.position;
            data.position >>= 1;
            if (data.position == 0)
            {
                data.position = resetValue;
                var index = data.index;
                data.index = index + 1;
                data.val = getNextValue(index);
            }
            bits |= ((resb > 0) ? 1 : 0) * power;
        }
        switch (bits)
        {
            case 0:
                bits = 0;
                maxpower = (Int32)Math.Pow(2.0, 8.0);
                for (var power = 1; power != maxpower; power <<= 1)
                {
                    var resb = data.val & data.position;
                    data.position >>= 1;
                    if (data.position == 0)
                    {
                        data.position = resetValue;
                        var index2 = data.index;
                        data.index = index2 + 1;
                        data.val = getNextValue(index2);
                    }
                    bits |= ((resb > 0) ? 1 : 0) * power;
                }
                c = Convert.ToInt32(LzString.f(bits));
                break;
            case 1:
                bits = 0;
                maxpower = (Int32)Math.Pow(2.0, 16.0);
                for (var power = 1; power != maxpower; power <<= 1)
                {
                    var resb = data.val & data.position;
                    data.position >>= 1;
                    if (data.position == 0)
                    {
                        data.position = resetValue;
                        var index2 = data.index;
                        data.index = index2 + 1;
                        data.val = getNextValue(index2);
                    }
                    bits |= ((resb > 0) ? 1 : 0) * power;
                }
                c = Convert.ToInt32(LzString.f(bits));
                break;
            case 2:
                return String.Empty;
        }
        dictionary[3] = Convert.ToChar(c).ToString();
        var w = Convert.ToChar(c).ToString();
        result.Append(Convert.ToChar(c));
        while (data.index <= length)
        {
            bits = 0;
            maxpower = (Int32)Math.Pow(2.0, (Double)numBits);
            for (var power = 1; power != maxpower; power <<= 1)
            {
                var resb = data.val & data.position;
                data.position >>= 1;
                if (data.position == 0)
                {
                    data.position = resetValue;
                    var index = data.index;
                    data.index = index + 1;
                    data.val = getNextValue(index);
                }
                bits |= ((resb > 0) ? 1 : 0) * power;
            }
            switch (c = bits)
            {
                case 0:
                    bits = 0;
                    maxpower = (Int32)Math.Pow(2.0, 8.0);
                    for (var power = 1; power != maxpower; power <<= 1)
                    {
                        var resb = data.val & data.position;
                        data.position >>= 1;
                        if (data.position == 0)
                        {
                            data.position = resetValue;
                            var index2 = data.index;
                            data.index = index2 + 1;
                            data.val = getNextValue(index2);
                        }
                        bits |= ((resb > 0) ? 1 : 0) * power;
                    }
                    dictionary[dictSize++] = LzString.f(bits).ToString();
                    c = dictSize - 1;
                    enlargeIn--;
                    break;
                case 1:
                    bits = 0;
                    maxpower = (Int32)Math.Pow(2.0, 16.0);
                    for (var power = 1; power != maxpower; power <<= 1)
                    {
                        var resb = data.val & data.position;
                        data.position >>= 1;
                        if (data.position == 0)
                        {
                            data.position = resetValue;
                            var index2 = data.index;
                            data.index = index2 + 1;
                            data.val = getNextValue(index2);
                        }
                        bits |= ((resb > 0) ? 1 : 0) * power;
                    }
                    dictionary[dictSize++] = LzString.f(bits).ToString();
                    c = dictSize - 1;
                    enlargeIn--;
                    break;
                case 2:
                    return result.ToString();
            }
            if (enlargeIn == 0)
            {
                enlargeIn = (Int32)Math.Pow(2.0, (Double)numBits);
                numBits++;
            }
            if (dictionary.ContainsKey(c))
            {
                entry = dictionary[c];
            }
            else
            {
                if (c != dictSize)
                {
                    return null;
                }
                entry = w + w[0].ToString();
            }
            result.Append(entry);
            dictionary[dictSize++] = w + entry[0].ToString();
            enlargeIn--;
            w = entry;
            if (enlargeIn == 0)
            {
                enlargeIn = (Int32)Math.Pow(2.0, (Double)numBits);
                numBits++;
            }
        }
        return String.Empty;
    }

    private static readonly String keyStrBase64 = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=";

    private static readonly String keyStrUriSafe = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+-$";

    private static readonly Dictionary<String, Dictionary<Char, Int32>> baseReverseDic = new Dictionary<String, Dictionary<Char, Int32>>();

    private static readonly LzString.GetCharFromInt f = (Int32 a) => Convert.ToChar(a);

    private delegate Char GetCharFromInt(Int32 a);

    private delegate Int32 GetNextValue(Int32 index);

    private struct DataStruct
    {
        public Int32 val;

        public Int32 position;

        public Int32 index;
    }
}
