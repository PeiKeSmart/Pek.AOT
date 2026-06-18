using System.Diagnostics;

using Pek.Log;

namespace Pek.Helpers;

/// <summary>方法调用辅助类。AOT 安全版</summary>
public static class InvokeHelper
{
    static InvokeHelper() { OnInvokeException = ex => XTrace.WriteException(ex); }

    public static Action<Exception> OnInvokeException { get; set; }

    public static Int64 Profile(Action action) { var sw = Stopwatch.StartNew(); action(); sw.Stop(); return sw.ElapsedMilliseconds; }
    public static Int64 Profile<T>(Action<T> action, T t) { var sw = Stopwatch.StartNew(); action(t); sw.Stop(); return sw.ElapsedMilliseconds; }
    public static Int64 Profile<T1, T2>(Action<T1, T2> action, T1 t1, T2 t2) { var sw = Stopwatch.StartNew(); action(t1, t2); sw.Stop(); return sw.ElapsedMilliseconds; }
    public static Int64 Profile<T1, T2, T3>(Action<T1, T2, T3> action, T1 t1, T2 t2, T3 t3) { var sw = Stopwatch.StartNew(); action(t1, t2, t3); sw.Stop(); return sw.ElapsedMilliseconds; }
    public static async Task<Int64> ProfileAsync(Func<Task> action) { var sw = Stopwatch.StartNew(); await action().ConfigureAwait(false); sw.Stop(); return sw.ElapsedMilliseconds; }
    public static async Task<Int64> ProfileAsync<T>(Func<T, Task> func, T t) { var sw = Stopwatch.StartNew(); await func(t).ConfigureAwait(false); sw.Stop(); return sw.ElapsedMilliseconds; }
    public static async Task<Int64> ProfileAsync<T1, T2>(Func<T1, T2, Task> func, T1 t1, T2 t2) { var sw = Stopwatch.StartNew(); await func(t1, t2).ConfigureAwait(false); sw.Stop(); return sw.ElapsedMilliseconds; }
    public static async Task<Int64> ProfileAsync<T1, T2, T3>(Func<T1, T2, T3, Task> func, T1 t1, T2 t2, T3 t3) { var sw = Stopwatch.StartNew(); await func(t1, t2, t3).ConfigureAwait(false); sw.Stop(); return sw.ElapsedMilliseconds; }

    public static void TryInvoke(Action action) { try { action(); } catch (Exception ex) { OnInvokeException?.Invoke(ex); } }
    public static async Task TryInvokeAsync(Func<Task> func) { try { await func().ConfigureAwait(false); } catch (Exception ex) { OnInvokeException?.Invoke(ex); } }
    public static void TryInvoke<T>(Action<T> action, T t) { try { action(t); } catch (Exception ex) { OnInvokeException?.Invoke(ex); } }
    public static async Task TryInvokeAsync<T>(Func<T, Task> func, T t) { try { await func(t).ConfigureAwait(false); } catch (Exception ex) { OnInvokeException?.Invoke(ex); } }
    public static void TryInvoke<T1, T2>(Action<T1, T2> action, T1 t1, T2 t2) { try { action(t1, t2); } catch (Exception ex) { OnInvokeException?.Invoke(ex); } }
    public static async Task TryInvokeAsync<T1, T2>(Func<T1, T2, Task> func, T1 t1, T2 t2) { try { await func(t1, t2).ConfigureAwait(false); } catch (Exception ex) { OnInvokeException?.Invoke(ex); } }
    public static void TryInvoke<T1, T2, T3>(Action<T1, T2, T3> action, T1 t1, T2 t2, T3 t3) { try { action(t1, t2, t3); } catch (Exception ex) { OnInvokeException?.Invoke(ex); } }
    public static async Task TryInvokeAsync<T1, T2, T3>(Func<T1, T2, T3, Task> func, T1 t1, T2 t2, T3 t3) { try { await func(t1, t2, t3).ConfigureAwait(false); } catch (Exception ex) { OnInvokeException?.Invoke(ex); } }
}
