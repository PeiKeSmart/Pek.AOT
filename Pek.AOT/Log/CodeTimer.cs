using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Pek.Log;

/// <summary>代码性能计时器</summary>
public class CodeTimer
{
    private UInt64 _cpuCycles;
    private Int64 _threadTime;
    private Thread? _thread;
    private CancellationTokenSource? _source;

    /// <summary>次数</summary>
    public Int32 Times { get; set; }

    /// <summary>迭代方法，如不指定，则使用 Time(Int32)</summary>
    public Action<Int32>? Action { get; set; }

    /// <summary>是否显示控制台进度</summary>
    public Boolean ShowProgress { get; set; }

    /// <summary>进度</summary>
    public Int32 Index { get; set; }

    /// <summary>CPU 周期</summary>
    public Int64 CpuCycles { get; set; }

    /// <summary>线程时间，单位是 ms</summary>
    public Int64 ThreadTime { get; set; }

    /// <summary>GC 代数</summary>
    public Int32[] Gen { get; set; } = [0, 0, 0];

    /// <summary>执行时间</summary>
    public TimeSpan Elapsed { get; set; }

    /// <summary>计时</summary>
    /// <param name="times">次数</param>
    /// <param name="action">动作</param>
    /// <param name="needTimeOne">是否预热</param>
    /// <returns>计时器</returns>
    public static CodeTimer Time(Int32 times, Action<Int32> action, Boolean needTimeOne = true)
    {
        var timer = new CodeTimer
        {
            Times = times,
            Action = action
        };

        if (needTimeOne) timer.TimeOne();
        timer.Time();

        return timer;
    }

    /// <summary>计时，并用控制台输出行</summary>
    /// <param name="title">标题</param>
    /// <param name="times">次数</param>
    /// <param name="action">动作</param>
    /// <param name="needTimeOne">是否预热</param>
    /// <returns>计时器</returns>
    public static CodeTimer TimeLine(String title, Int32 times, Action<Int32> action, Boolean needTimeOne = true)
    {
        var length = Encoding.UTF8.GetByteCount(title);
        Console.Write("{0}{1}：", length >= 16 ? String.Empty : new String(' ', 16 - length), title);

        var timer = new CodeTimer
        {
            Times = times,
            Action = action,
            ShowProgress = true
        };

        var color = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;
        var left = Console.CursorLeft;
        if (needTimeOne) timer.TimeOne();
        timer.Time();

        Thread.Sleep(10);
        Console.CursorLeft = left;
        Console.WriteLine(timer.ToString());
        Console.ForegroundColor = color;

        return timer;
    }

    /// <summary>显示头部</summary>
    /// <param name="title">标题</param>
    public static void ShowHeader(String title = "指标")
    {
        Write(title, 16);
        Console.Write("：");
        var color = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Write("执行时间", 9);
        Console.Write(' ');
        Write("CPU时间", 9);
        Console.Write(' ');
        Write("指令周期", 15);
        Write("GC(0/1/2)", 9);
        Console.WriteLine("   百分比");

        _msBase = 0;
        Console.ForegroundColor = color;
    }

    /// <summary>计时核心方法，处理进程和线程优先级</summary>
    public virtual void Time()
    {
        if (Times <= 0) throw new InvalidOperationException("非法迭代次数！");

        var process = Process.GetCurrentProcess();
        var processPriority = process.PriorityClass;
        var threadPriority = Thread.CurrentThread.Priority;
        try
        {
            process.PriorityClass = ProcessPriorityClass.High;
            Thread.CurrentThread.Priority = ThreadPriority.Highest;

            StartProgress();
            TimeTrue();
        }
        finally
        {
            StopProgress();
            Thread.CurrentThread.Priority = threadPriority;
            process.PriorityClass = processPriority;
        }
    }

    /// <summary>真正的计时</summary>
    protected virtual void TimeTrue()
    {
        if (Times <= 0) throw new InvalidOperationException("非法迭代次数！");

        GC.Collect(GC.MaxGeneration);
        var gen = new Int32[GC.MaxGeneration + 1];
        for (var i = 0; i <= GC.MaxGeneration; i++)
        {
            gen[i] = GC.CollectionCount(i);
        }

        var watch = Stopwatch.StartNew();
        _cpuCycles = GetCycleCount();
        _threadTime = GetCurrentThreadTimes();

        var action = Action;
        if (action == null)
        {
            action = Time;
            Init();
        }

        for (var i = 0; i < Times; i++)
        {
            Index = i;
            action(i);
        }

        if (Action == null) Finish();

        CpuCycles = (Int64)(GetCycleCount() - _cpuCycles);
        ThreadTime = (GetCurrentThreadTimes() - _threadTime) / 10_000;

        watch.Stop();
        Elapsed = watch.Elapsed;

        var list = new List<Int32>();
        for (var i = 0; i <= GC.MaxGeneration; i++)
        {
            list.Add(GC.CollectionCount(i) - gen[i]);
        }
        Gen = list.ToArray();
    }

    /// <summary>执行一次迭代，预热所有方法</summary>
    public void TimeOne()
    {
        var count = Times;
        try
        {
            Times = 1;
            Time();
        }
        finally
        {
            Times = count;
        }
    }

    /// <summary>迭代前执行</summary>
    public virtual void Init() { }

    /// <summary>每一次迭代</summary>
    /// <param name="index">索引</param>
    public virtual void Time(Int32 index) { }

    /// <summary>迭代后执行</summary>
    public virtual void Finish() { }

    /// <summary>已重载。输出依次分别是：执行时间、CPU线程时间、时钟周期、GC代数</summary>
    /// <returns>文本</returns>
    public override String ToString()
    {
        var ms = Elapsed.TotalMilliseconds;
        if (_msBase == 0) _msBase = ms;
        var percentage = _msBase == 0 ? 0 : ms / _msBase;
        return $"{ms,7:n0}ms {ThreadTime,7:n0}ms {CpuCycles,15:n0} {Gen[0],3}/{Gen[1]}/{Gen[2]}\t{percentage,8:p2}";
    }

    private void StartProgress()
    {
        if (!ShowProgress) return;

        _source = new CancellationTokenSource();
        _thread = new Thread(Progress)
        {
            IsBackground = true,
            Priority = ThreadPriority.BelowNormal
        };
        _thread.Start();
    }

    private void StopProgress()
    {
        if (_thread == null || !_thread.IsAlive) return;

        _source?.Cancel();
        _thread.Join(3000);
    }

    private void Progress()
    {
        var left = Console.CursorLeft;
        var cursorVisible = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Console.CursorVisible;
        Console.CursorVisible = false;
        var watch = Stopwatch.StartNew();
        while (_source != null && !_source.IsCancellationRequested)
        {
            try
            {
                var index = Index;
                if (index >= Times) break;

                if (index > 0 && watch.Elapsed.TotalMilliseconds > 10)
                {
                    var progress = (Double)index / Times;
                    var ms = watch.Elapsed.TotalMilliseconds;
                    var total = new TimeSpan(0, 0, 0, 0, (Int32)(ms * Times / index));
                    Console.Write($"{ms,7:n0}ms {progress:p2} Total=>{total}");
                    Console.CursorLeft = left;
                }
            }
            catch
            {
                break;
            }

            Thread.Sleep(500);
        }

        watch.Stop();
        Console.CursorLeft = left;
        Console.CursorVisible = cursorVisible;
    }

    private static void Write(String name, Int32 max)
    {
        var length = Encoding.UTF8.GetByteCount(name);
        if (length < max) Console.Write(new String(' ', max - length));
        Console.Write(name);
    }

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern Boolean QueryThreadCycleTime(IntPtr threadHandle, ref UInt64 cycleTime);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentThread();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern Boolean GetThreadTimes(IntPtr threadHandle, out Int64 creationTime, out Int64 exitTime, out Int64 kernelTime, out Int64 userTime);

    private static Boolean _supportCycle = true;
    private static Double _msBase;

    private static UInt64 GetCycleCount()
    {
        if (!_supportCycle) return 0;

        try
        {
            UInt64 cycleCount = 0;
            QueryThreadCycleTime(GetCurrentThread(), ref cycleCount);
            return cycleCount;
        }
        catch
        {
            _supportCycle = false;
            return 0;
        }
    }

    private static Int64 GetCurrentThreadTimes()
    {
        GetThreadTimes(GetCurrentThread(), out _, out _, out var kernelTime, out var userTime);
        return kernelTime + userTime;
    }
}