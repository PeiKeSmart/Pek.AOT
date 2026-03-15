using System.Diagnostics;
using Pek.Log;

namespace Pek.Threading;

/// <summary>线程池助手</summary>
public static class ThreadPoolX
{
    static ThreadPoolX()
    {
        ThreadPool.GetMinThreads(out var workerThreads, out var completionPortThreads);
        var target = Math.Min(64, Environment.ProcessorCount * 4);
        if (workerThreads < target || completionPortThreads < target)
        {
            ThreadPool.SetMinThreads(Math.Max(workerThreads, target), Math.Max(completionPortThreads, target));
        }

#if NET7_0_OR_GREATER
        AppContext.SetData("System.Threading.ThreadPool.Blocking.MaxDelayMs", 50);
#endif
    }

    /// <summary>初始化线程池</summary>
    public static void Init() { }

    /// <summary>投递线程池任务</summary>
    /// <param name="callback">回调方法</param>
    [DebuggerHidden]
    public static void QueueUserWorkItem(Action callback)
    {
        if (callback == null) return;

        ThreadPool.UnsafeQueueUserWorkItem(_ =>
        {
            try
            {
                callback();
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }
        }, null);
    }

    /// <summary>投递线程池任务</summary>
    /// <typeparam name="T">状态类型</typeparam>
    /// <param name="callback">回调方法</param>
    /// <param name="state">状态对象</param>
    [DebuggerHidden]
    public static void QueueUserWorkItem<T>(Action<T> callback, T state)
    {
        if (callback == null) return;

        ThreadPool.UnsafeQueueUserWorkItem(_ =>
        {
            try
            {
                callback(state);
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }
        }, null);
    }
}
