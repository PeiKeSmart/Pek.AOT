using System.Collections.Concurrent;

using Pek.Collections;

namespace Pek.Log;

/// <summary>控制台日志</summary>
public class ConsoleLog : Logger
{
    private readonly ConcurrentQueue<WriteLogEventArgs> _logs = new();
    private readonly Object _lock = new();
    private readonly Pool<WriteLogEventArgs> _pool = new(64, () => new WriteLogEventArgs());
    private volatile Int32 _logCount;
    private Int32 _writing;

    private static readonly ConcurrentDictionary<Int32, ConsoleColor> _threadColors = [];
    private static readonly ConsoleColor[] _colors = [
        ConsoleColor.Green, ConsoleColor.Cyan, ConsoleColor.Magenta, ConsoleColor.White, ConsoleColor.Yellow,
        ConsoleColor.DarkGreen, ConsoleColor.DarkCyan, ConsoleColor.DarkMagenta, ConsoleColor.DarkRed, ConsoleColor.DarkYellow
    ];

    /// <summary>是否使用颜色</summary>
    public Boolean UseColor { get; set; } = true;

    /// <summary>写入日志</summary>
    /// <param name="level">日志等级</param>
    /// <param name="format">格式化模板</param>
    /// <param name="args">格式化参数</param>
    protected override void OnWrite(LogLevel level, String format, params Object?[] args)
    {
        if (_logCount > 1024) return;

        var item = _pool.Get().Set(level);
        if (args.Length == 1 && args[0] is Exception ex && (String.IsNullOrEmpty(format) || format == "{0}"))
            item = item.Set(null, ex);
        else
            item = item.Set(Format(format, args), null);

        _logs.Enqueue(item);
        Interlocked.Increment(ref _logCount);

        if (Interlocked.CompareExchange(ref _writing, 1, 0) == 0)
        {
            ThreadPool.UnsafeQueueUserWorkItem(_ =>
            {
                try
                {
                    WriteConsole();
                }
                catch
                {
                }
                finally
                {
                    _writing = 0;
                }
            }, null);
        }
    }

    private void WriteConsole()
    {
        while (_logs.TryDequeue(out var item))
        {
            Interlocked.Decrement(ref _logCount);

            if (!UseColor)
            {
                Console.WriteLine(item.GetAndReset());
                _pool.Return(item);
                continue;
            }

            var hasLock = false;
            try
            {
                if (Monitor.TryEnter(_lock, 5_000))
                {
                    hasLock = true;
                    Console.ForegroundColor = item.Level switch
                    {
                        LogLevel.Warn => ConsoleColor.Yellow,
                        LogLevel.Error or LogLevel.Fatal => ConsoleColor.Red,
                        _ => GetThreadColor(item.ThreadId),
                    };
                    Console.WriteLine(item.GetAndReset());
                    Console.ResetColor();
                }
            }
            finally
            {
                if (hasLock) Monitor.Exit(_lock);
                _pool.Return(item);
            }
        }
    }

    private static ConsoleColor GetThreadColor(Int32 threadId)
    {
        if (threadId == 1) return ConsoleColor.Gray;

        return _threadColors.GetOrAdd(threadId, static value => _colors[value % _colors.Length]);
    }

    /// <summary>已重载。</summary>
    /// <returns>日志器信息</returns>
    public override String ToString() => $"{GetType().Name} UseColor={UseColor}";
}
