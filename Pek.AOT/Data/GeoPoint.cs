using Pek;
using Pek.Extension;

namespace Pek.Data;

/// <summary>经纬度坐标</summary>
public class GeoPoint
{
    /// <summary>经度</summary>
    public Double Longitude { get; set; }

    /// <summary>纬度</summary>
    public Double Latitude { get; set; }

    /// <summary>实例化经纬度坐标</summary>
    public GeoPoint() { }

    /// <summary>实例化经纬度坐标</summary>
    /// <param name="longitude">经度</param>
    /// <param name="latitude">纬度</param>
    public GeoPoint(Double longitude, Double latitude)
    {
        Longitude = longitude;
        Latitude = latitude;
    }

    /// <summary>根据坐标字符串实例化经纬度坐标</summary>
    /// <param name="location">坐标字符串，格式为 longitude,latitude</param>
    public GeoPoint(String location)
    {
        if (location.IsNullOrEmpty()) return;

        var parts = location.Split(',');
        if (parts.Length < 2) return;

        Longitude = parts[0].ToDouble();
        Latitude = parts[1].ToDouble();
    }

    /// <summary>返回文本表示</summary>
    public override String ToString() => $"{Longitude},{Latitude}";
}