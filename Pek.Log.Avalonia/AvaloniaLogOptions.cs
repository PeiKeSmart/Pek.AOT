namespace Pek.Log.Avalonia;

/// <summary>Avalonia 日志选项</summary>
public class AvaloniaLogOptions
{
    /// <summary>最大保留日志条数</summary>
    public Int32 MaxItems { get; set; } = 1000;

    /// <summary>批量刷新阈值</summary>
    public Int32 BatchSize { get; set; } = 1;
}