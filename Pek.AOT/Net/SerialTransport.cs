using System.ComponentModel;
using System.IO.Ports;
using System.Runtime.Versioning;
using System.Text;

using Microsoft.Win32;

using Pek.Data;
using Pek.Log;
using Pek.Threading;

namespace Pek.Net;

/// <summary>串口传输</summary>
public class SerialTransport : DisposeBase, ITransport
{
    private const String LogScope = "Pek.Net";

    private readonly TimerX _timer;
    private TaskCompletionSource<IPacket?>? _source;
    private SerialPort? _serial;
    private String? _description;
    private Boolean _inDisconnectEvent;

    /// <summary>串口对象</summary>
    public SerialPort? Serial
    {
        get => _serial;
        set
        {
            _serial = value;
            if (_serial == null) return;

            PortName = _serial.PortName;
            BaudRate = _serial.BaudRate;
            Parity = _serial.Parity;
            DataBits = _serial.DataBits;
            StopBits = _serial.StopBits;
            DtrEnable = _serial.DtrEnable;
            RtsEnable = _serial.RtsEnable;
            BreakState = _serial.BreakState;
        }
    }

    /// <summary>端口名称。默认 COM1</summary>
    public String PortName { get; set; } = "COM1";

    /// <summary>波特率。默认 115200</summary>
    public Int32 BaudRate { get; set; } = 115200;

    /// <summary>奇偶校验位。默认 None</summary>
    public Parity Parity { get; set; } = Parity.None;

    /// <summary>数据位。默认 8</summary>
    public Int32 DataBits { get; set; } = 8;

    /// <summary>停止位。默认 One</summary>
    public StopBits StopBits { get; set; } = StopBits.One;

    /// <summary>DtrEnable</summary>
    public Boolean DtrEnable { get; set; }

    /// <summary>RtsEnable</summary>
    public Boolean RtsEnable { get; set; }

    /// <summary>BreakState</summary>
    public Boolean BreakState { get; set; }

    /// <summary>超时时间。默认 1000ms</summary>
    public Int32 Timeout { get; set; } = 1000;

    /// <summary>字节超时。数据包间隔，默认 20ms</summary>
    public Int32 ByteTimeout { get; set; } = 20;

    /// <summary>日志对象</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>描述信息</summary>
    public String Description
    {
        get
        {
            if (_description != null) return _description;

            var names = GetNames();
            _description = names.TryGetValue(PortName, out var description) ? description : String.Empty;
            return _description;
        }
    }

    /// <summary>数据到达事件</summary>
    public event EventHandler<ReceivedEventArgs>? Received;

    /// <summary>断开时触发</summary>
    public event EventHandler? Disconnected;

    /// <summary>串口传输</summary>
    public SerialTransport() => _timer = new TimerX(CheckDisconnect, null, 3000, 3000, "SerialTransport");

    /// <summary>应用配置</summary>
    /// <param name="config">串口配置</param>
    public void Apply(SerialPortConfig config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));

        PortName = config.PortName;
        BaudRate = config.BaudRate;
        DataBits = config.DataBits;
        StopBits = config.StopBits;
        Parity = config.Parity;
        DtrEnable = config.DtrEnable;
        RtsEnable = config.RtsEnable;
        BreakState = config.BreakState;
    }

    /// <summary>确保创建</summary>
    public virtual void EnsureCreate()
    {
        ThrowIfDisposed();

        if (Serial != null) return;

        Serial = new SerialPort(PortName, BaudRate, Parity, DataBits, StopBits)
        {
            DtrEnable = DtrEnable,
            RtsEnable = RtsEnable,
            ReadTimeout = Timeout > 0 ? Timeout : SerialPort.InfiniteTimeout,
            WriteTimeout = Timeout > 0 ? Timeout : SerialPort.InfiniteTimeout,
        };

        try
        {
            Serial.BreakState = BreakState;
        }
        catch
        {
        }

        _description = null;
    }

    /// <summary>打开</summary>
    /// <returns>是否成功</returns>
    public virtual Boolean Open()
    {
        EnsureCreate();

        if (Serial == null) return false;
        if (Serial.IsOpen) return true;

        Serial.DataReceived -= DataReceived;
        Serial.DataReceived += DataReceived;
        Serial.Open();
        return true;
    }

    /// <summary>关闭</summary>
    /// <returns>是否成功</returns>
    public virtual Boolean Close()
    {
        var serial = Serial;
        if (serial == null) return true;

        Serial = null;
        try
        {
            serial.DataReceived -= DataReceived;
            if (serial.IsOpen) serial.Close();
            serial.Dispose();
        }
        finally
        {
            OnDisconnect();
        }

        return true;
    }

    /// <summary>发送数据</summary>
    /// <param name="data">数据包</param>
    /// <returns>是否成功</returns>
    public virtual Boolean Send(IPacket data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (!Open() || Serial == null) return false;

        WriteLog("Send Port={0} Data={1}", PortName, data.ToHex());

        var buffer = data.ToArray();
        lock (Serial)
        {
            Serial.Write(buffer, 0, buffer.Length);
        }

        return true;
    }

    /// <summary>异步发送数据并等待响应</summary>
    /// <param name="data">待发送数据</param>
    /// <returns>响应数据包</returns>
    public virtual Task<IPacket?> SendAsync(IPacket? data)
    {
        if (!Open() || Serial == null) return Task.FromResult<IPacket?>(null);
        if (_source != null) throw new InvalidOperationException("SerialTransport 目前不支持并发等待多个响应。");

        _source = new TaskCompletionSource<IPacket?>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (data != null)
        {
            WriteLog("SendAsync Port={0} Data={1}", PortName, data.ToHex());
            var buffer = data.ToArray();
            lock (Serial)
            {
                Serial.Write(buffer, 0, buffer.Length);
            }
        }

        return WaitResponseAsync(_source);
    }

    /// <summary>同步接收数据</summary>
    /// <returns>响应数据包</returns>
    public virtual IPacket? Receive()
    {
        var task = SendAsync(null);
        if (Timeout > 0 && !task.Wait(Timeout)) return null;

        return task.GetAwaiter().GetResult();
    }

    /// <summary>获取带有描述的串口名，没有时返回空数组</summary>
    /// <returns>串口名数组</returns>
    public static String[] GetPortNames() => GetNames().Select(e => $"{e.Key}({e.Value})").ToArray();

    /// <summary>获取串口列表，名称和描述</summary>
    /// <returns>串口列表</returns>
    public static Dictionary<String, String> GetNames()
    {
        var names = SerialPort.GetPortNames().OrderBy(e => e, StringComparer.OrdinalIgnoreCase).ToArray();
        var result = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in names)
        {
            result[item] = item;
        }

        if (!OperatingSystem.IsWindows()) return result;

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DEVICEMAP\SERIALCOMM", false);
            using var usb = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\USB", false);
            if (key == null) return result;

            foreach (var item in key.GetValueNames())
            {
                var name = key.GetValue(item)?.ToString() ?? String.Empty;
                if (String.IsNullOrWhiteSpace(name)) continue;

                var description = ResolveDescription(usb, name, item);
                result[name] = description;
            }
        }
        catch
        {
        }

        return result;
    }

    /// <summary>从串口列表选择串口，支持自动选择关键字</summary>
    /// <param name="keyword">串口名称或者描述关键字</param>
    /// <returns>串口传输实例</returns>
    public static SerialTransport? Choose(String? keyword = null)
    {
        var ports = GetNames();
        if (ports.Count == 0)
        {
            Console.WriteLine("没有可用串口！");
            return null;
        }

        String? selectedName = null;
        String? selectedDescription = null;

        Console.WriteLine("可用串口：");
        Console.ForegroundColor = ConsoleColor.Green;
        foreach (var item in ports)
        {
            if (String.Equals(item.Value, "Serial0", StringComparison.OrdinalIgnoreCase)) continue;

            if (!String.IsNullOrWhiteSpace(keyword) &&
                (String.Equals(item.Key, keyword, StringComparison.OrdinalIgnoreCase) ||
                 item.Value.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                selectedName = item.Key;
                selectedDescription = item.Value;
            }

            Console.WriteLine("{0,5}({1})", item.Key, item.Value);
        }

        if (String.IsNullOrWhiteSpace(selectedName))
        {
            var last = ports.Last();
            selectedName = last.Key;
            selectedDescription = last.Value;
        }

        while (true)
        {
            Console.ResetColor();
            Console.Write("请输入串口名称（默认 ");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write(selectedName);
            Console.ResetColor();
            Console.Write("）：");

            var input = Console.ReadLine();
            if (String.IsNullOrWhiteSpace(input)) break;

            if (ports.TryGetValue(input, out var description))
            {
                selectedName = input;
                selectedDescription = description;
                break;
            }
        }

        Console.WriteLine();
        Console.Write("正在打开串口 ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("{0}({1})", selectedName, selectedDescription);
        Console.ResetColor();

        return new SerialTransport { PortName = selectedName! };
    }

    /// <summary>输出日志</summary>
    /// <param name="format">格式模板</param>
    /// <param name="args">参数</param>
    public void WriteLog(String format, params Object?[] args)
    {
        if (Log == null || !Log.Enable || LogLevel.Info < Log.Level) return;
        Log.Info(XXTrace.FormatScope(LogScope, "SerialTransport", PortName + " ", format), args);
    }

    /// <summary>返回文本表示</summary>
    /// <returns>端口名称</returns>
    public override String ToString() => !String.IsNullOrWhiteSpace(PortName) ? PortName : "(SerialPort)";

    /// <summary>释放资源</summary>
    /// <param name="disposing">是否显式释放</param>
    protected override void Dispose(Boolean disposing)
    {
        if (Disposed) return;

        try
        {
            if (disposing)
            {
                Close();
                _timer.Dispose();
            }
        }
        catch
        {
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    private async Task<IPacket?> WaitResponseAsync(TaskCompletionSource<IPacket?> source)
    {
        if (Timeout <= 0) return await source.Task.ConfigureAwait(false);

        using var cts = new CancellationTokenSource(Timeout);
        using var registration = cts.Token.Register(() => source.TrySetResult(null));
        return await source.Task.ConfigureAwait(false);
    }

    private void DataReceived(Object? sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            var serial = sender as SerialPort ?? Serial;
            if (serial == null || !serial.IsOpen) return;

            WaitMore(serial);
            var bytesToRead = serial.BytesToRead;
            if (bytesToRead <= 0) return;

            var buffer = new Byte[bytesToRead];
            var count = serial.Read(buffer, 0, buffer.Length);
            if (count <= 0) return;

            ProcessReceive(new ArrayPacket(buffer, 0, count));
        }
        catch (Exception ex)
        {
            if (Log != null && Log.Enable) Log.Error("{0}", ex);
        }
    }

    private void WaitMore(SerialPort serial)
    {
        var interval = ByteTimeout > 0 ? ByteTimeout : 1;
        var deadline = DateTime.Now.AddMilliseconds(interval);
        var count = serial.BytesToRead;
        while (serial.IsOpen && deadline > DateTime.Now)
        {
            Thread.Sleep(interval);
            if (count == serial.BytesToRead) continue;

            deadline = DateTime.Now.AddMilliseconds(interval);
            count = serial.BytesToRead;
        }
    }

    private void ProcessReceive(IPacket packet)
    {
        try
        {
            OnReceive(packet);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            if (Log != null && Log.Enable) Log.Error("{0}", ex);
        }
    }

    private void OnReceive(IPacket packet)
    {
        if (_source != null)
        {
            var source = _source;
            _source = null;
            source.TrySetResult(packet);
            return;
        }

        var eventArgs = ReceivedEventArgs.Rent();
        try
        {
            eventArgs.Packet = packet;
            Received?.Invoke(this, eventArgs);
        }
        finally
        {
            ReceivedEventArgs.Return(eventArgs);
        }
    }

    private void OnDisconnect()
    {
        if (Disconnected == null || _inDisconnectEvent) return;

        try
        {
            _inDisconnectEvent = true;
            Disconnected(this, EventArgs.Empty);
        }
        finally
        {
            _inDisconnectEvent = false;
        }
    }

    private void CheckDisconnect(Object? state)
    {
        if (String.IsNullOrWhiteSpace(PortName) || Serial == null || !Serial.IsOpen) return;

        var exists = SerialPort.GetPortNames().Any(e => String.Equals(e, PortName, StringComparison.OrdinalIgnoreCase));
        if (exists) return;

        WriteLog("端口已不存在，准备关闭 Port={0}", PortName);
        Close();
    }

    [SupportedOSPlatform("windows")]
    private static String ResolveDescription(RegistryKey? usb, String name, String fallback)
    {
        if (usb != null)
        {
            foreach (var vid in usb.GetSubKeyNames())
            {
                using var usbVid = usb.OpenSubKey(vid);
                if (usbVid == null) continue;

                foreach (var child in usbVid.GetSubKeyNames())
                {
                    using var sub = usbVid.OpenSubKey(child);
                    var friendlyName = sub?.GetValue("FriendlyName")?.ToString();
                    if (String.IsNullOrWhiteSpace(friendlyName)) continue;
                    if (!friendlyName.Contains($"({name})", StringComparison.OrdinalIgnoreCase)) continue;

                    return friendlyName.Replace($"({name})", String.Empty, StringComparison.OrdinalIgnoreCase).Trim();
                }
            }
        }

        var index = fallback.LastIndexOf('\\');
        return index >= 0 ? fallback[(index + 1)..] : fallback;
    }
}