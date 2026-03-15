namespace Pek.Data;

/// <summary>范围</summary>
public struct IndexRange
{
    /// <summary>开始，包含</summary>
    public Int32 Start;

    /// <summary>结束，不包含</summary>
    public Int32 End;

    /// <summary>返回文本表示</summary>
    public override readonly String ToString() => $"({Start}, {End})";
}