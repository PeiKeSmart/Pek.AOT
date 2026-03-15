using System.Text;

namespace Pek.Data;

/// <summary>经纬坐标的一维编码表示</summary>
public static class GeoHash
{
    private static readonly Int32[] BITS = [16, 8, 4, 2, 1];
    private const String Base32 = "0123456789bcdefghjkmnpqrstuvwxyz";
    private static readonly SByte[] DecodeMap = new SByte[128];
    private const Int32 MaxPrecision = 12;

    static GeoHash()
    {
        for (var i = 0; i < DecodeMap.Length; i++) DecodeMap[i] = -1;

        for (var i = 0; i < Base32.Length; i++)
        {
            var ch = Base32[i];
            if (ch < DecodeMap.Length) DecodeMap[ch] = (SByte)i;

            var upper = Char.ToUpperInvariant(ch);
            if (upper < DecodeMap.Length) DecodeMap[upper] = (SByte)i;
        }
    }

    /// <summary>编码坐标点为 GeoHash 字符串</summary>
    /// <param name="longitude">经度</param>
    /// <param name="latitude">纬度</param>
    /// <param name="charCount">字符个数，默认 9 位字符编码</param>
    /// <returns>GeoHash 字符串</returns>
    public static String Encode(Double longitude, Double latitude, Int32 charCount = 9)
    {
        if (charCount < 1) charCount = 1;
        if (charCount > MaxPrecision) charCount = MaxPrecision;

        longitude = ClampLongitude(longitude);
        latitude = ClampLatitude(latitude);

        Double[] longitudeRange = [-180, 180];
        Double[] latitudeRange = [-90, 90];

        var isEvenBit = true;
        UInt64 bits = 0;
        var length = charCount * 5;
        for (var i = 0; i < length; i++)
        {
            bits <<= 1;

            var value = isEvenBit ? longitude : latitude;
            var range = isEvenBit ? longitudeRange : latitudeRange;
            var middle = (range[0] + range[1]) / 2;
            if (value >= middle)
            {
                bits |= 0x1;
                range[0] = middle;
            }
            else
            {
                range[1] = middle;
            }

            isEvenBit = !isEvenBit;
        }

        bits <<= 64 - length;

        var builder = new StringBuilder(charCount);
        for (var i = 0; i < charCount; i++)
        {
            var index = (Int32)((bits & 0xf800000000000000L) >> 59);
            builder.Append(Base32[index]);
            bits <<= 5;
        }

        return builder.ToString();
    }

    /// <summary>解码 GeoHash 字符串为坐标点</summary>
    /// <param name="geohash">GeoHash 字符串</param>
    /// <returns>中心点经纬度</returns>
    public static (Double Longitude, Double Latitude) Decode(String geohash)
    {
        if (String.IsNullOrEmpty(geohash)) throw new ArgumentException("geohash 不能为空", nameof(geohash));

        DecodeCore(geohash, out var longitudeRange, out var latitudeRange);
        return ((longitudeRange[0] + longitudeRange[1]) / 2, (latitudeRange[0] + latitudeRange[1]) / 2);
    }

    /// <summary>尝试解码 GeoHash 字符串为坐标点</summary>
    /// <param name="geohash">GeoHash 字符串</param>
    /// <param name="longitude">输出经度</param>
    /// <param name="latitude">输出纬度</param>
    /// <returns>是否成功</returns>
    public static Boolean TryDecode(String geohash, out Double longitude, out Double latitude)
    {
        longitude = 0;
        latitude = 0;
        if (String.IsNullOrEmpty(geohash)) return false;

        if (!TryDecodeCore(geohash, out var longitudeRange, out var latitudeRange)) return false;

        longitude = (longitudeRange[0] + longitudeRange[1]) / 2;
        latitude = (latitudeRange[0] + latitudeRange[1]) / 2;
        return true;
    }

    /// <summary>判断 GeoHash 字符串是否有效</summary>
    /// <param name="geohash">GeoHash 字符串</param>
    /// <returns>是否有效</returns>
    public static Boolean IsValid(String geohash)
    {
        if (String.IsNullOrEmpty(geohash)) return false;

        for (var i = 0; i < geohash.Length; i++)
        {
            var ch = geohash[i];
            if (ch >= 128 || DecodeMap[ch] < 0) return false;
        }

        return true;
    }

    /// <summary>获取 GeoHash 对应的边界矩形</summary>
    /// <param name="geohash">GeoHash 字符串</param>
    /// <returns>最小经纬与最大经纬</returns>
    public static (Double MinLongitude, Double MinLatitude, Double MaxLongitude, Double MaxLatitude) GetBoundingBox(String geohash)
    {
        if (String.IsNullOrEmpty(geohash)) throw new ArgumentException("geohash 不能为空", nameof(geohash));

        DecodeCore(geohash, out var longitudeRange, out var latitudeRange);
        return (longitudeRange[0], latitudeRange[0], longitudeRange[1], latitudeRange[1]);
    }

    /// <summary>获取某个 GeoHash 的邻居</summary>
    /// <param name="geohash">中心 GeoHash</param>
    /// <param name="deltaLongitude">经度方向偏移</param>
    /// <param name="deltaLatitude">纬度方向偏移</param>
    /// <returns>邻居 GeoHash</returns>
    public static String Neighbor(String geohash, Int32 deltaLongitude, Int32 deltaLatitude)
    {
        if (String.IsNullOrEmpty(geohash)) throw new ArgumentException("geohash 不能为空", nameof(geohash));

        var (minLongitude, minLatitude, maxLongitude, maxLatitude) = GetBoundingBox(geohash);
        var longitudeSpan = maxLongitude - minLongitude;
        var latitudeSpan = maxLatitude - minLatitude;

        var centerLongitude = (minLongitude + maxLongitude) / 2;
        var centerLatitude = (minLatitude + maxLatitude) / 2;

        var nextLongitude = ClampLongitude(centerLongitude + deltaLongitude * longitudeSpan);
        var nextLatitude = ClampLatitude(centerLatitude + deltaLatitude * latitudeSpan);

        return Encode(nextLongitude, nextLatitude, geohash.Length);
    }

    /// <summary>获取 8 个方向的邻居</summary>
    /// <param name="geohash">中心 GeoHash</param>
    /// <returns>8 个邻居 GeoHash</returns>
    public static String[] Neighbors(String geohash)
    {
        return
        [
            Neighbor(geohash, -1, 1),
            Neighbor(geohash, 0, 1),
            Neighbor(geohash, 1, 1),
            Neighbor(geohash, -1, 0),
            Neighbor(geohash, 1, 0),
            Neighbor(geohash, -1, -1),
            Neighbor(geohash, 0, -1),
            Neighbor(geohash, 1, -1)
        ];
    }

    private static void DecodeCore(String geohash, out Double[] longitudeRange, out Double[] latitudeRange)
    {
        if (!TryDecodeCore(geohash, out longitudeRange, out latitudeRange))
            throw new ArgumentException($"geohash 包含非法字符: '{FindInvalidChar(geohash)}'", nameof(geohash));
    }

    private static Boolean TryDecodeCore(String geohash, out Double[] longitudeRange, out Double[] latitudeRange)
    {
        latitudeRange = [-90, 90];
        longitudeRange = [-180, 180];

        var isEvenBit = true;
        for (var i = 0; i < geohash.Length; i++)
        {
            var ch = geohash[i];
            var code = ch < 128 ? (Int32)DecodeMap[ch] : -1;
            if (code < 0) return false;

            for (var j = 0; j < 5; j++)
            {
                var range = isEvenBit ? longitudeRange : latitudeRange;
                var middle = (range[0] + range[1]) / 2;
                if ((code & BITS[j]) != 0)
                    range[0] = middle;
                else
                    range[1] = middle;

                isEvenBit = !isEvenBit;
            }
        }

        return true;
    }

    private static Char FindInvalidChar(String geohash)
    {
        for (var i = 0; i < geohash.Length; i++)
        {
            var ch = geohash[i];
            if (ch >= 128 || DecodeMap[ch] < 0) return ch;
        }

        return '\0';
    }

    private static Double ClampLongitude(Double value)
    {
        if (value < -180) return -180;
        if (value > 180) return 180;
        return value;
    }

    private static Double ClampLatitude(Double value)
    {
        if (value < -90) return -90;
        if (value > 90) return 90;
        return value;
    }
}