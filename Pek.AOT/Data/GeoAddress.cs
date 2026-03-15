namespace Pek.Data;

/// <summary>地理地址</summary>
public class GeoAddress
{
    /// <summary>名称</summary>
    public String Name { get; set; } = String.Empty;

    /// <summary>坐标</summary>
    public GeoPoint? Location { get; set; }

    /// <summary>地址</summary>
    public String Address { get; set; } = String.Empty;

    /// <summary>行政区域编码</summary>
    public Int32 Code { get; set; }

    /// <summary>国家</summary>
    public String Country { get; set; } = String.Empty;

    /// <summary>省份</summary>
    public String Province { get; set; } = String.Empty;

    /// <summary>城市</summary>
    public String City { get; set; } = String.Empty;

    /// <summary>区县</summary>
    public String District { get; set; } = String.Empty;

    /// <summary>乡镇</summary>
    public String Township { get; set; } = String.Empty;

    /// <summary>乡镇编码</summary>
    public String Towncode { get; set; } = String.Empty;

    /// <summary>街道</summary>
    public String Street { get; set; } = String.Empty;

    /// <summary>门牌号</summary>
    public String StreetNumber { get; set; } = String.Empty;

    /// <summary>级别</summary>
    public String Level { get; set; } = String.Empty;

    /// <summary>精确打点</summary>
    public Boolean Precise { get; set; }

    /// <summary>可信度，范围 0-100</summary>
    public Int32 Confidence { get; set; }

    /// <summary>返回文本表示</summary>
    public override String ToString() => Address;
}