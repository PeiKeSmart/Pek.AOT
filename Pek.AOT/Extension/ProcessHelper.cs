using System.ComponentModel;
using System.Diagnostics;
using System.Text;

using Pek.Log;

namespace Pek.Extension;

/// <summary>进程辅助</summary>
public static class ProcessHelper
{
    /// <summary>以隐藏窗口执行命令行</summary>
    /// <param name="cmd">文件名</param>
    /// <param name="arguments">命令参数</param>
    /// <param name="msWait">等待毫秒数</param>
    /// <param name="output">输出回调</param>
    /// <param name="onExit">退出回调</param>
    /// <param name="working">工作目录</param>
    /// <returns>退出代码</returns>
    public static Int32 Run(this String cmd, String? arguments = null, Int32 msWait = 0, Action<String?>? output = null, Action<Process>? onExit = null, String? working = null) => RunNew(cmd, arguments, msWait, output, Encoding.UTF8, onExit, working);

    /// <summary>以隐藏窗口执行命令行，支持指定输出编码</summary>
    /// <param name="cmd">文件名</param>
    /// <param name="arguments">命令参数</param>
    /// <param name="msWait">等待毫秒数</param>
    /// <param name="output">输出回调</param>
    /// <param name="encoding">输出编码</param>
    /// <param name="onExit">退出回调</param>
    /// <param name="working">工作目录</param>
    /// <returns>退出代码</returns>
    public static Int32 RunNew(this String cmd, String? arguments = null, Int32 msWait = 0, Action<String?>? output = null, Encoding? encoding = null, Action<Process>? onExit = null, String? working = null)
    {
        if (XTrace.Log.Level <= LogLevel.Debug) XTrace.WriteLine("Run {0} {1} {2}", cmd, arguments, msWait);

        encoding ??= Encoding.UTF8;

        using var process = new Process();
        var startInfo = process.StartInfo;
        startInfo.FileName = cmd;
        if (arguments != null) startInfo.Arguments = arguments;
        startInfo.WindowStyle = ProcessWindowStyle.Hidden;
        startInfo.CreateNoWindow = true;
        if (!String.IsNullOrWhiteSpace(working)) startInfo.WorkingDirectory = working;

        if (msWait > 0)
        {
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.StandardOutputEncoding = encoding;
            startInfo.StandardErrorEncoding = encoding;

            if (output != null)
            {
                process.OutputDataReceived += (_, eventArgs) => output(eventArgs.Data);
                process.ErrorDataReceived += (_, eventArgs) => output(eventArgs.Data);
            }
            else
            {
                process.OutputDataReceived += (_, eventArgs) => { if (eventArgs.Data != null) XTrace.WriteLine(eventArgs.Data); };
                process.ErrorDataReceived += (_, eventArgs) => { if (eventArgs.Data != null) XTrace.Log.Error(eventArgs.Data); };
            }
        }

        if (onExit != null)
        {
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => onExit(process);
        }

        process.Start();
        if (msWait > 0)
        {
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        if (msWait == 0) return -1;
        if (msWait < 0)
            process.WaitForExit();
        else if (!process.WaitForExit(msWait))
        {
#if NETCOREAPP
            process.Kill(true);
#else
            process.Kill();
#endif
            return -1;
        }

        return process.ExitCode;
    }

    /// <summary>执行命令并等待返回</summary>
    /// <param name="cmd">命令</param>
    /// <param name="arguments">参数</param>
    /// <param name="msWait">等待时间</param>
    /// <param name="returnError">是否返回错误输出</param>
    /// <returns>输出内容</returns>
    public static String? Execute(this String cmd, String? arguments = null, Int32 msWait = 0, Boolean returnError = false) => Execute(cmd, arguments, msWait, returnError, null);

    /// <summary>执行命令并等待返回</summary>
    /// <param name="cmd">命令</param>
    /// <param name="arguments">参数</param>
    /// <param name="msWait">等待时间</param>
    /// <param name="returnError">是否返回错误输出</param>
    /// <param name="outputEncoding">输出编码</param>
    /// <returns>输出内容</returns>
    public static String? Execute(this String cmd, String? arguments, Int32 msWait, Boolean returnError, Encoding? outputEncoding)
    {
        try
        {
            if (XTrace.Log.Level <= LogLevel.Debug) XTrace.WriteLine("Execute {0} {1}", cmd, arguments);

            var startInfo = new ProcessStartInfo(cmd, arguments ?? String.Empty)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = outputEncoding,
                StandardErrorEncoding = outputEncoding,
            };

            using var process = Process.Start(startInfo);
            if (process == null) return null;

            if (msWait > 0 && !process.WaitForExit(msWait))
            {
#if NETCOREAPP
                process.Kill(true);
#else
                process.Kill();
#endif
                return null;
            }

            var result = process.StandardOutput.ReadToEnd();
            if (result.IsNullOrEmpty() && returnError) result = process.StandardError.ReadToEnd();

            return result;
        }
        catch (Win32Exception ex)
        {
            if (XTrace.Log.Level <= LogLevel.Debug) XTrace.Log.Error(ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            if (XTrace.Log.Level <= LogLevel.Debug) XTrace.Log.Error(ex.Message);
            return null;
        }
    }
}