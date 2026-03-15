namespace Pek.Data;

/// <summary>时序点，用于时序数据计算</summary>
public struct TimePoint
{
    /// <summary>时间</summary>
    public Int64 Time;

    /// <summary>数值</summary>
    public Double Value;

    /// <summary>返回文本表示</summary>
    public override readonly String ToString() => $"({Time}, {Value})";
}