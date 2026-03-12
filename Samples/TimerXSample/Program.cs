using NewLife;
using NewLife.Log;
using NewLife.Threading;

ThreadPoolX.Init();
XTrace.Log = new ConsoleLog { Level = LogLevel.Debug };
TimerScheduler.Default.Log = XTrace.Log;

Console.WriteLine("TimerX Sample");
Console.WriteLine($"TickCount64: {Runtime.TickCount64}");
Console.WriteLine();

await RunPeriodicTimerTestAsync();
await RunAsyncTimerTestAsync();
await RunAbsoluteTimerTestAsync();
await RunCronTimerTestAsync();

Console.WriteLine();
Console.WriteLine("TimerX sample passed.");

static async Task RunPeriodicTimerTestAsync()
{
    Int32 count = 0;
    Int32 currentParallel = 0;
    Int32 maxParallel = 0;
    var completed = new TaskCompletionSource<Boolean>(TaskCreationOptions.RunContinuationsAsynchronously);

    using var timer = new TimerX(_ =>
    {
        var parallel = Interlocked.Increment(ref currentParallel);
        UpdateMax(ref maxParallel, parallel);

        var currentCount = Interlocked.Increment(ref count);
        Thread.Sleep(140);

        Interlocked.Decrement(ref currentParallel);
        if (currentCount >= 3) completed.TrySetResult(true);
    }, "Sync", 50, 100, "SyncScheduler");

    var task = await Task.WhenAny(completed.Task, Task.Delay(4000));
    Ensure(task == completed.Task, "周期定时器在限定时间内未完成");
    Ensure(count >= 3, $"周期定时器执行次数异常: {count}");
    Ensure(maxParallel == 1, $"周期定时器出现重入: {maxParallel}");

    Console.WriteLine($"Periodic Test: count={count}, maxParallel={maxParallel}, scheduler={timer.Scheduler.Name}");
}

static async Task RunAsyncTimerTestAsync()
{
    Int32 count = 0;
    var completed = new TaskCompletionSource<Boolean>(TaskCreationOptions.RunContinuationsAsynchronously);

    using var timer = new TimerX(async _ =>
    {
        var currentCount = Interlocked.Increment(ref count);
        await Task.Delay(120).ConfigureAwait(false);
        if (currentCount >= 2) completed.TrySetResult(true);
    }, "Async", 50, 150, "AsyncScheduler");

    var task = await Task.WhenAny(completed.Task, Task.Delay(4000));
    Ensure(task == completed.Task, "异步定时器在限定时间内未完成");
    Ensure(count >= 2, $"异步定时器执行次数异常: {count}");
    Ensure(timer.Async, "异步定时器未标记为异步");

    Console.WriteLine($"Async Test: count={count}, scheduler={timer.Scheduler.Name}");
}

static async Task RunAbsoluteTimerTestAsync()
{
    DateTime? firedAt = null;
    var target = DateTime.Now.AddMilliseconds(300);
    var completed = new TaskCompletionSource<Boolean>(TaskCreationOptions.RunContinuationsAsynchronously);

    using var timer = new TimerX(_ =>
    {
        firedAt = DateTime.Now;
        completed.TrySetResult(true);
    }, null, target, 500, "AbsoluteScheduler");

    var task = await Task.WhenAny(completed.Task, Task.Delay(4000));
    Ensure(task == completed.Task, "绝对定时器在限定时间内未触发");
    Ensure(firedAt.HasValue, "绝对定时器未记录触发时间");

    var delta = Math.Abs((firedAt.Value - target).TotalMilliseconds);
    Ensure(delta < 400, $"绝对定时器偏差过大: {delta:n0}ms");

    Console.WriteLine($"Absolute Test: target={target:HH:mm:ss.fff}, fired={firedAt.Value:HH:mm:ss.fff}, delta={delta:n0}ms");
}

static async Task RunCronTimerTestAsync()
{
    DateTime? firedAt = null;
    var start = DateTime.Now;
    var completed = new TaskCompletionSource<Boolean>(TaskCreationOptions.RunContinuationsAsynchronously);

    using var timer = new TimerX(_ =>
    {
        firedAt = DateTime.Now;
        completed.TrySetResult(true);
    }, "Cron", "*/1 * * * * *", "CronScheduler");

    var task = await Task.WhenAny(completed.Task, Task.Delay(5000));
    Ensure(task == completed.Task, "Cron 定时器在限定时间内未触发");
    Ensure(timer.Crons != null && timer.Crons.Length == 1, "Cron 定时器表达式解析失败");
    Ensure(firedAt.HasValue && firedAt.Value > start, "Cron 定时器触发时间无效");

    Console.WriteLine($"Cron Test: next={timer.NextTime:HH:mm:ss.fff}, fired={firedAt.Value:HH:mm:ss.fff}, cronCount={timer.Crons.Length}");
}

static void Ensure(Boolean condition, String message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void UpdateMax(ref Int32 target, Int32 value)
{
    while (true)
    {
        var current = Volatile.Read(ref target);
        if (value <= current) return;
        if (Interlocked.CompareExchange(ref target, value, current) == current) return;
    }
}
