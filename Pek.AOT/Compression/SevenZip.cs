using Pek.Extension;
using Pek.Log;
using Pek.Web;

namespace Pek.Compression;

/// <summary>7Zip</summary>
public class SevenZip
{
    private static readonly String _7z = String.Empty;

    static SevenZip()
    {
        var path = String.Empty;
        var setting = Setting.Current;

        if (path.IsNullOrEmpty())
        {
            path = TryResolveLocal("7z.exe", setting.PluginPath);
            if (path.IsNullOrEmpty()) path = TryResolveLocal("7z", setting.PluginPath);
        }

        if (path.IsNullOrEmpty())
        {
            XTrace.WriteLine("准备下载7z扩展包");

            var url = setting.PluginServer;
            using var client = new WebClientX
            {
                Log = XTrace.Log
            };
            var dir = setting.PluginPath;
            _ = client.DownloadLinkAndExtract(url, "7z", dir);
            path = TryResolveLocal("7z.exe", dir);
            if (path.IsNullOrEmpty()) path = TryResolveLocal("7z", dir);
        }

        if (!path.IsNullOrEmpty()) _7z = path.GetFullPath();
        XTrace.WriteLine("7Z目录 {0}", _7z);
    }

    /// <summary>是否可用</summary>
    public static Boolean IsAvailable => !_7z.IsNullOrEmpty() && File.Exists(_7z);

    /// <summary>压缩文件或目录</summary>
    /// <param name="path">源路径</param>
    /// <param name="destFile">目标文件</param>
    public void Compress(String path, String destFile)
    {
        EnsureAvailable();
        if (Directory.Exists(path)) path = path.GetFullPath().EnsureEnd("\\") + "*";

        Run($"a \"{destFile}\" \"{path}\" -mx9 -ssw");
    }

    /// <summary>解压缩文件</summary>
    /// <param name="file">压缩文件</param>
    /// <param name="destDir">目标目录</param>
    /// <param name="overwrite">是否覆盖</param>
    public void Extract(String file, String destDir, Boolean overwrite = false)
    {
        EnsureAvailable();
        destDir.EnsureDirectory(false);

        var arguments = $"x \"{file}\" -o\"{destDir}\" -y -r";
        arguments += overwrite ? " -aoa" : " -aos";
        Run(arguments);
    }

    private static String TryResolveLocal(String fileName, String pluginPath)
    {
        var path = fileName.GetFullPath();
        if (File.Exists(path)) return path;

        path = pluginPath.CombinePath(fileName).GetFullPath();
        if (File.Exists(path)) return path;

        path = Path.Combine("7z", fileName).GetFullPath();
        if (File.Exists(path)) return path;

        path = Path.Combine("..", "7z", fileName).GetFullPath();
        if (File.Exists(path)) return path;

        return String.Empty;
    }

    private static void EnsureAvailable()
    {
        if (IsAvailable) return;
        throw new FileNotFoundException("Unable to locate 7z executable.", _7z);
    }

    private static Int32 Run(String arguments) => _7z.Run(arguments, 5000);
}