using System.Collections.ObjectModel;

namespace Pek.Log.Avalonia;

/// <summary>Avalonia 日志缓冲区</summary>
public class AvaloniaLogBuffer
{
    private readonly ObservableCollection<String> _items;
    private readonly AvaloniaLogOptions _options;

    /// <summary>日志集合</summary>
    public ObservableCollection<String> Items => _items;

    /// <summary>实例化</summary>
    /// <param name="items">外部日志集合</param>
    /// <param name="options">日志选项</param>
    public AvaloniaLogBuffer(ObservableCollection<String>? items = null, AvaloniaLogOptions? options = null)
    {
        _items = items ?? [];
        _options = options ?? new AvaloniaLogOptions();
    }

    /// <summary>添加日志</summary>
    /// <param name="message">日志文本</param>
    public void Add(String message)
    {
        _items.Add(message);

        var overflow = _items.Count - _options.MaxItems;
        while (overflow > 0)
        {
            _items.RemoveAt(0);
            overflow--;
        }
    }

    /// <summary>清空日志</summary>
    public void Clear() => _items.Clear();
}