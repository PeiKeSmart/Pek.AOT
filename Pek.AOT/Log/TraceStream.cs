using System.Text;

namespace Pek.Log;

/// <summary>跟踪流。包装基础流，用于记录 Read/Write 等行为</summary>
public class TraceStream : Stream
{
    private static readonly String[] DefaultTraceMembers = ["Write", "WriteByte", "Read", "ReadByte", "BeginRead", "BeginWrite", "EndRead", "EndWrite", "Seek", "Close", "Flush", "SetLength", "SetPosition"];
    private Int64 _lastPosition = -1;
    private Boolean _useConsole;

    /// <summary>基础流</summary>
    public Stream BaseStream { get; set; }

    /// <summary>跟踪的成员</summary>
    public ICollection<String> TraceMembers { get; set; }

    /// <summary>是否小端字节序</summary>
    public Boolean IsLittleEndian { get; set; }

    /// <summary>显示位置的步长，默认 16，0 表示不输出位置</summary>
    public Int32 ShowPositionStep { get; set; }

    /// <summary>编码</summary>
    public Encoding Encoding { get; set; }

    /// <summary>操作事件</summary>
    public event EventHandler<TraceStreamEventArgs>? OnAction;

    /// <summary>是否使用控制台输出</summary>
    public Boolean UseConsole
    {
        get => _useConsole;
        set
        {
            if (value && !Runtime.IsConsole) return;
            if (value == _useConsole) return;

            if (value)
                OnAction += HandleConsoleTrace;
            else
                OnAction -= HandleConsoleTrace;

            _useConsole = value;
        }
    }

    /// <summary>实例化</summary>
    public TraceStream() : this(null) { }

    /// <summary>实例化</summary>
    /// <param name="stream">基础流</param>
    public TraceStream(Stream? stream)
    {
        BaseStream = stream ?? new MemoryStream();
        TraceMembers = new HashSet<String>(DefaultTraceMembers, StringComparer.OrdinalIgnoreCase);
        IsLittleEndian = true;
        ShowPositionStep = 16;
        Encoding = Encoding.UTF8;
        UseConsole = true;
    }

    /// <summary>写入</summary>
    public override void Write(Byte[] buffer, Int32 offset, Int32 count)
    {
        RaiseAction("Write", buffer, offset, count);
        BaseStream.Write(buffer, offset, count);
    }

    /// <summary>写入一个字节</summary>
    public override void WriteByte(Byte value)
    {
        RaiseAction("WriteByte", value);
        BaseStream.WriteByte(value);
    }

    /// <summary>读取</summary>
    public override Int32 Read(Byte[] buffer, Int32 offset, Int32 count)
    {
        var value = BaseStream.Read(buffer, offset, count);
        RaiseAction("Read", buffer, offset, count, value);
        return value;
    }

    /// <summary>读取一个字节</summary>
    public override Int32 ReadByte()
    {
        var value = BaseStream.ReadByte();
        RaiseAction("ReadByte", value);
        return value;
    }

    /// <summary>异步开始读</summary>
    public override IAsyncResult BeginRead(Byte[] buffer, Int32 offset, Int32 count, AsyncCallback? callback, Object? state)
    {
        RaiseAction("BeginRead", offset, count);
        return BaseStream.BeginRead(buffer, offset, count, callback, state);
    }

    /// <summary>异步开始写</summary>
    public override IAsyncResult BeginWrite(Byte[] buffer, Int32 offset, Int32 count, AsyncCallback? callback, Object? state)
    {
        RaiseAction("BeginWrite", offset, count);
        return BaseStream.BeginWrite(buffer, offset, count, callback, state);
    }

    /// <summary>异步读结束</summary>
    public override Int32 EndRead(IAsyncResult asyncResult)
    {
        RaiseAction("EndRead");
        return BaseStream.EndRead(asyncResult);
    }

    /// <summary>异步写结束</summary>
    public override void EndWrite(IAsyncResult asyncResult)
    {
        RaiseAction("EndWrite");
        BaseStream.EndWrite(asyncResult);
    }

    /// <summary>设置流位置</summary>
    public override Int64 Seek(Int64 offset, SeekOrigin origin)
    {
        RaiseAction("Seek", offset, origin);
        return BaseStream.Seek(offset, origin);
    }

    /// <summary>关闭数据流</summary>
    public override void Close()
    {
        RaiseAction("Close");
        BaseStream.Close();
    }

    /// <summary>刷新缓冲区</summary>
    public override void Flush()
    {
        RaiseAction("Flush");
        BaseStream.Flush();
    }

    /// <summary>设置长度</summary>
    public override void SetLength(Int64 value)
    {
        RaiseAction("SetLength", value);
        BaseStream.SetLength(value);
    }

    /// <summary>是否可读</summary>
    public override Boolean CanRead => BaseStream.CanRead;

    /// <summary>是否可搜索</summary>
    public override Boolean CanSeek => BaseStream.CanSeek;

    /// <summary>是否可超时</summary>
    public override Boolean CanTimeout => BaseStream.CanTimeout;

    /// <summary>是否可写</summary>
    public override Boolean CanWrite => BaseStream.CanWrite;

    /// <summary>读取超时</summary>
    public override Int32 ReadTimeout { get => BaseStream.ReadTimeout; set => BaseStream.ReadTimeout = value; }

    /// <summary>写入超时</summary>
    public override Int32 WriteTimeout { get => BaseStream.WriteTimeout; set => BaseStream.WriteTimeout = value; }

    /// <summary>长度</summary>
    public override Int64 Length => BaseStream.Length;

    /// <summary>位置</summary>
    public override Int64 Position
    {
        get => BaseStream.Position;
        set
        {
            RaiseAction("SetPosition", value);
            BaseStream.Position = value;
        }
    }

    private void RaiseAction(String action, params Object?[] args)
    {
        if (OnAction == null || !TraceMembers.Contains(action)) return;

        if (ShowPositionStep > 0)
        {
            var current = Position;
            if (_lastPosition < 0)
            {
                _lastPosition = current;
                OnAction(this, new TraceStreamEventArgs("BeginPosition", [_lastPosition]));
            }
            else if (current > _lastPosition + ShowPositionStep)
            {
                _lastPosition = current;
                OnAction(this, new TraceStreamEventArgs("Position", [_lastPosition]));
            }
        }

        OnAction(this, new TraceStreamEventArgs(action, args));
    }

    private void HandleConsoleTrace(Object? sender, TraceStreamEventArgs e)
    {
        var color = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = ConsoleColor.Red;
            var action = e.Action.Length < 8 ? e.Action + "\t" : e.Action;
            Console.Write(action);
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("\t");

            if (TryWriteBuffer(e.Arguments))
            {
                Console.WriteLine();
                return;
            }

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(String.Join(", ", e.Arguments.Select(static arg => arg?.ToString() ?? String.Empty)));
        }
        finally
        {
            Console.ForegroundColor = color;
        }
    }

    private Boolean TryWriteBuffer(Object?[] args)
    {
        if (args.Length == 1 && args[0] is Byte byteValue)
        {
            Console.Write(byteValue.ToString("X2"));
            return true;
        }

        if (args.Length < 3 || args[0] is not Byte[] buffer || args[1] is not Int32 offset || args[^1] is not Int32 count) return false;
        if (count <= 0) return true;

        var actualCount = Math.Min(count, buffer.Length - offset);
        if (actualCount <= 0) return true;

        var span = new ReadOnlySpan<Byte>(buffer, offset, actualCount);
        Console.Write(BitConverter.ToString(span.ToArray()).Replace('-', ' '));
        return true;
    }
}

/// <summary>跟踪流事件参数</summary>
public sealed class TraceStreamEventArgs(String action, Object?[] arguments) : EventArgs
{
    /// <summary>操作名</summary>
    public String Action { get; } = action;

    /// <summary>参数</summary>
    public Object?[] Arguments { get; } = arguments;
}