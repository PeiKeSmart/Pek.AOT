using System.Diagnostics;
using System.Text;

using Pek;

namespace Pek.Extension;

/// <summary>跨平台语音播报提供器</summary>
/// <remarks>
/// <para>该实现不依赖运行时反射，而是调用各平台已有的语音能力。</para>
/// <para>Windows 通过 PowerShell 调用系统语音；macOS 使用 say；Linux 优先 spd-say，其次 espeak-ng 和 espeak。</para>
/// </remarks>
public sealed class PlatformSpeakProvider : ISpeakProvider
{
    private readonly Object _lock = new();
    private readonly List<Process> _processes = [];

    /// <summary>是否支持当前平台</summary>
    public Boolean IsSupported => Runtime.Windows || Runtime.Linux || Runtime.OSX;

    /// <summary>同步播报文本</summary>
    /// <param name="value">文本</param>
    public void Speak(String value)
    {
        if (value.IsNullOrWhiteSpace()) return;

        _ = StartSpeech(value, false);
    }

    /// <summary>异步播报文本</summary>
    /// <param name="value">文本</param>
    public void SpeakAsync(String value)
    {
        if (value.IsNullOrWhiteSpace()) return;

        _ = StartSpeech(value, true);
    }

    /// <summary>取消所有异步播报</summary>
    public void SpeakAsyncCancelAll()
    {
        List<Process> list;
        lock (_lock)
        {
            list = [.. _processes];
            _processes.Clear();
        }

        foreach (var process in list)
        {
            try
            {
                if (!process.HasExited)
                {
#if NETCOREAPP
                    process.Kill(true);
#else
                    process.Kill();
#endif
                }
            }
            catch { }
            finally
            {
                process.Dispose();
            }
        }

        if (Runtime.Linux)
            TryStartProcess("spd-say", ["--stop"], false, false, out _);
    }

    private Boolean StartSpeech(String value, Boolean async)
    {
        if (Runtime.Windows)
            return StartWindowsSpeech(value, async);
        if (Runtime.OSX)
            return StartMacSpeech(value, async);
        if (Runtime.Linux)
            return StartLinuxSpeech(value, async);

        return false;
    }

    private Boolean StartWindowsSpeech(String value, Boolean async)
    {
        var script = "Add-Type -AssemblyName System.Speech; " +
                     "$s = New-Object System.Speech.Synthesis.SpeechSynthesizer; " +
                     "$s.SetOutputToDefaultAudioDevice(); " +
                     "$s.Speak('" + EscapePowerShellLiteral(value) + "');";
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        var arguments = new String[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-EncodedCommand", encoded };

        if (TryStartProcess("pwsh", arguments, async, async, out _)) return true;
        if (TryStartProcess("powershell", arguments, async, async, out _)) return true;

        return false;
    }

    private Boolean StartMacSpeech(String value, Boolean async) => TryStartProcess("say", [value], async, async, out _);

    private Boolean StartLinuxSpeech(String value, Boolean async)
    {
        if (TryStartProcess("spd-say", async ? [value] : ["--wait", value], async, async, out _)) return true;
        if (TryStartProcess("espeak-ng", [value], async, async, out _)) return true;
        if (TryStartProcess("espeak", [value], async, async, out _)) return true;

        return false;
    }

    private Boolean TryStartProcess(String fileName, IEnumerable<String> arguments, Boolean async, Boolean trackProcess, out Process? process)
    {
        process = null;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };

            foreach (var item in arguments)
            {
                startInfo.ArgumentList.Add(item);
            }

            process = Process.Start(startInfo);
            if (process == null) return false;

            if (async)
            {
                if (trackProcess)
                {
                    process.EnableRaisingEvents = true;
                    process.Exited += OnProcessExited;
                    lock (_lock) _processes.Add(process);
                }
                else
                {
                    process.Dispose();
                }
            }
            else
            {
                process.WaitForExit();
                process.Dispose();
            }

            return true;
        }
        catch
        {
            process?.Dispose();
            process = null;
            return false;
        }
    }

    private void OnProcessExited(Object? sender, EventArgs e)
    {
        if (sender is not Process process) return;

        lock (_lock)
        {
            _processes.Remove(process);
        }

        process.Dispose();
    }

    private static String EscapePowerShellLiteral(String value) => value.Replace("'", "''");
}