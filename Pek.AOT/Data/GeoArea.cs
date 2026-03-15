namespace Pek.Data;

/// <summary>地理区域</summary>
public class GeoArea
{
    /// <summary>编码</summary>
    public Int32 Code { get; set; }

    /// <summary>名称</summary>
    public String Name { get; set; } = String.Empty;

    /// <summary>父级</summary>
    public Int32 ParentCode { get; set; }

    /// <summary>中心</summary>
    public String Center { get; set; } = String.Empty;

    /// <summary>边界</summary>
    public String Polyline { get; set; } = String.Empty;

    /// <summary>级别</summary>
    public String Level { get; set; } = String.Empty;

    /// <summary>返回文本表示</summary>
    public override String ToString() => $"{Code} {Name}";
}