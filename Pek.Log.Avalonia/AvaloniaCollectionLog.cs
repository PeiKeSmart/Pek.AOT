using Avalonia.Threading;

namespace Pek.Log.Avalonia;

/// <summary>基于 Avalonia 集合的日志输出</summary>
public class AvaloniaCollectionLog : Logger
{
    private readonly AvaloniaLogBuffer _buffer;

    /// <summary>日志缓冲区</summary>
    public AvaloniaLogBuffer Buffer => _buffer;

    /// <summary>实例化</summary>
    /// <param name="buffer">日志缓冲区</param>
    public AvaloniaCollectionLog(AvaloniaLogBuffer buffer) => _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));

    /// <summary>实例化</summary>
    /// <param name="items">可绑定日志集合</param>
    /// <param name="options">日志选项</param>
    public AvaloniaCollectionLog(System.Collections.ObjectModel.ObservableCollection<String>? items = null, AvaloniaLogOptions? options = null)
        : this(new AvaloniaLogBuffer(items, options)) { }

    /// <summary>写入日志</summary>
    /// <param name="level">日志等级</param>
    /// <param name="format">格式化模板</param>
    /// <param name="args">格式化参数</param>
    protected override void OnWrite(LogLevel level, String format, params Object?[] args)
    {
        var item = WriteLogEventArgs.Current.Set(level);
        if (args.Length == 1 && args[0] is Exception ex && (String.IsNullOrEmpty(format) || format == "{0}"))
            item.Set(null, ex);
        else
            item.Set(Format(format, args), null);

        var message = item.GetAndReset();

        if (Dispatcher.UIThread.CheckAccess())
            _buffer.Add(message);
        else
            Dispatcher.UIThread.Post(() => _buffer.Add(message), DispatcherPriority.Background);
    }
}