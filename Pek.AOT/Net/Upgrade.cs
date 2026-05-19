using System.Diagnostics;
using System.Reflection;

using NewLife.Reflection;

using Pek.Extension;
using Pek.Http;
using Pek.Log;
using Pek.Web;

namespace Pek.Net;

/// <summary>升级更新</summary>
/// <remarks>
/// 优先比较版本Version，再比较时间Time。
/// 自动更新的难点在于覆盖正在使用的exe/dll文件，通过改名可以解决。
/// </remarks>
public class Upgrade
{
    /// <summary>名称</summary>
    public String Name { get; set; }

    /// <summary>服务器地址</summary>
    public String Server { get; set; }

    /// <summary>版本</summary>
    public Version? Version { get; set; }

    /// <summary>本地编译时间</summary>
    public DateTime Time { get; set; }

    /// <summary>更新目录</summary>
    public String UpdatePath { get; set; } = "Update";

    /// <summary>目标目录</summary>
    public String DestinationPath { get; set; } = ".";

    /// <summary>超链接信息</summary>
    public Link? Link { get; set; }

    /// <summary>缓存文件。同名文件不再下载，默认false</summary>
    public Boolean CacheFile { get; set; } = true;

    /// <summary>更新源文件</summary>
    public String? SourceFile { get; set; }

    /// <summary>实例化一个升级对象实例，获取当前应用信息</summary>
    public Upgrade()
    {
        Name = nameof(Upgrade);

        var assembly = Assembly.GetEntryAssembly();
        if (assembly != null)
        {
            var name = assembly.GetName();
            if (name != null)
            {
                Version = name.Version;
                Name = name.Name ?? nameof(Upgrade);
            }

            var assemblyX = AssemblyX.Create(assembly);
            if (assemblyX != null) Time = assemblyX.Compile;
        }

        Server = Pek.Setting.Current.PluginServer;
    }

    /// <summary>获取版本信息，检查是否需要更新</summary>
    /// <returns>是否找到更新</returns>
    public Boolean Check()
    {
        DeleteBackup(DestinationPath);

        foreach (var url in Server.Split(',', ';'))
        {
            if (url.IsNullOrEmpty()) continue;

            WriteLog("检查资源包 {0}", url);
            try
            {
                var web = CreateClient();
                var html = web.GetString(url);
                var links = Link.Parse(html, url, item => !item.Name.IsNullOrEmpty() && item.Name.ToLowerInvariant().Contains(Name.ToLowerInvariant()));
                if (links == null || links.Length == 0)
                {
                    WriteLog("找不到资源包");
                    continue;
                }

                if (Version > new Version(0, 0))
                {
                    var link = links.OrderByDescending(item => item.Version).FirstOrDefault()!;
                    if (link.Version > Version)
                    {
                        Link = link;
                        WriteLog("线上版本[{0}]较新 {1}>{2}", link.FullName, link.Version, Version);
                    }
                    else
                        WriteLog("线上版本[{0}]较旧 {1}<={2}", link.FullName, link.Version, Version);
                }
                else
                {
                    var link = links.OrderByDescending(item => item.Time).FirstOrDefault()!;
                    if (link.Time > Time.AddMinutes(10))
                    {
                        Link = link;
                        WriteLog("线上版本[{0}]较新 {1}>{2}", link.FullName, link.Time, Time);
                    }
                    else
                        WriteLog("线上版本[{0}]较旧 {1}<={2}", link.FullName, link.Time, Time);
                }

                return Link != null;
            }
            catch (Exception ex)
            {
                WriteLog("检查失败 {0} {1}", url, ex.Message);
            }
        }

        return false;
    }

    /// <summary>开始更新</summary>
    public void Download()
    {
        var link = Link ?? throw new Exception("No new version available!");
        var url = link.Url;
        if (url.IsNullOrEmpty()) throw new Exception("The upgrade package address is invalid!");

        var file = !link.FullName.IsNullOrEmpty() ? UpdatePath.CombinePath(link.FullName!).GetBasePath() : Path.GetTempFileName();
        Task.Factory.StartNew(() => DownloadAsync(url, file, link.Hash, default), TaskCreationOptions.LongRunning).Unwrap().Wait(30_000);
    }

    /// <summary>开始更新</summary>
    /// <param name="url">下载源</param>
    /// <param name="fileName">文件名</param>
    public void Download(String url, String? fileName)
    {
        var file = !fileName.IsNullOrEmpty() ? UpdatePath.CombinePath(fileName!).GetBasePath() : Path.GetTempFileName();
        if (!CacheFile && File.Exists(file)) File.Delete(file);
        if (!File.Exists(file))
        {
            WriteLog("准备下载 {0} 到 {1}", url, file);

            var stopwatch = Stopwatch.StartNew();
            var web = CreateClient();
            Task.Factory.StartNew(() => web.DownloadFileAsync(url, file, default), TaskCreationOptions.LongRunning).Unwrap().Wait(30_000);

            stopwatch.Stop();
            WriteLog("下载完成！大小{0:n0}字节，耗时{1:n0}ms", file.AsFile().Length, stopwatch.ElapsedMilliseconds);
        }

        SourceFile = file;
    }

    /// <summary>开始更新</summary>
    /// <param name="url">下载源</param>
    /// <param name="fileName">文件名</param>
    /// <param name="expectedHash">预期哈希字符串，支持带算法前缀或自动识别</param>
    /// <param name="cancellationToken">取消通知</param>
    public async Task DownloadAsync(String url, String fileName, String? expectedHash, CancellationToken cancellationToken)
    {
        var file = !fileName.IsNullOrEmpty() ? UpdatePath.CombinePath(fileName!).GetBasePath() : Path.GetTempFileName();
        if (!CacheFile && File.Exists(file)) File.Delete(file);
        if (!File.Exists(file))
        {
            WriteLog("准备下载 {0} 到 {1}", url, file);

            var stopwatch = Stopwatch.StartNew();
            var web = CreateClient();
            await web.DownloadFileAsync(url, file, expectedHash, cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();
            WriteLog("下载完成！大小{0:n0}字节，耗时{1:n0}ms", file.AsFile().Length, stopwatch.ElapsedMilliseconds);
        }

        SourceFile = file;
    }

    /// <summary>检查并执行更新操作</summary>
    /// <returns>是否更新成功</returns>
    public Boolean Update()
    {
        DeleteBackup(DestinationPath);

        var file = SourceFile;
        if (file.IsNullOrEmpty() || !File.Exists(file)) return false;

        WriteLog("发现更新包 {0}", file);
        if (!file.EndsWithIgnoreCase(".zip", ".7z")) return false;

        var temp = Path.GetTempPath().CombinePath(Path.GetFileNameWithoutExtension(file));
        WriteLog("解压缩更新包到临时目录 {0}", temp);
        file.AsFile().Extract(temp, true);

        CopyAndReplace(temp, DestinationPath);
        WriteLog("更新成功！");

        return true;
    }

    /// <summary>启动当前应用的新进程。当前进程退出</summary>
    public void Run()
    {
        var executable = Environment.ProcessPath;
        if (executable.IsNullOrEmpty()) return;

        WriteLog("启动进程 {0}", executable);
        Process.Start(executable);

        WriteLog("退出当前进程");
        if (!Runtime.IsConsole) Process.GetCurrentProcess().CloseMainWindow();
        Environment.Exit(0);
        Process.GetCurrentProcess().Kill();
    }

    private global::System.Net.Http.HttpClient? _client;

    private global::System.Net.Http.HttpClient CreateClient()
    {
        if (_client != null) return _client;

        return _client = new global::System.Net.Http.HttpClient().SetUserAgent();
    }

    /// <summary>删除备份文件</summary>
    /// <param name="dest">目标目录</param>
    [Obsolete("=>DeleteBackup", true)]
    public static void DeleteBuckup(String dest)
    {
        var directory = dest.AsDirectory();
        var files = directory.GetAllFiles("*.del", true);
        foreach (var item in files)
        {
            try
            {
                item.Delete();
            }
            catch { }
        }
    }

    /// <summary>删除备份文件</summary>
    /// <param name="dest">目标目录</param>
    public void DeleteBackup(String dest)
    {
        var directory = dest.AsDirectory();
        var files = directory.GetAllFiles("*.del", true);
        foreach (var item in files)
        {
            WriteLog("Delete {0}", item);
            try
            {
                item.Delete();
            }
            catch { }
        }
    }

    /// <summary>解压缩</summary>
    /// <param name="fileName">文件名</param>
    /// <returns>解压目录</returns>
    public String Extract(String fileName)
    {
        WriteLog("Extract {0}", fileName);

        var source = Path.GetTempPath().CombinePath(Path.GetFileNameWithoutExtension(fileName));
        WriteLog("解压缩更新包到临时目录 {0}", source);
        fileName.AsFile().Extract(source, true);

        return source;
    }

    /// <summary>拷贝并替换。正在使用锁定的文件不可删除，但可以改名</summary>
    /// <param name="source">源目录</param>
    /// <param name="dest">目标目录</param>
    public void CopyAndReplace(String source, String dest)
    {
        WriteLog("CopyAndReplace {0} => {1}", source, dest);

        var directory = source.AsDirectory();
        var root = directory.FullName.EnsureEnd(Path.DirectorySeparatorChar.ToString());
        var normalizedRoot = Path.GetFullPath(root).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).EnsureEnd(Path.DirectorySeparatorChar.ToString());
        foreach (var item in directory.GetAllFiles(null, true))
        {
            var full = Path.GetFullPath(item.FullName).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            var name = full.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase) ? full[normalizedRoot.Length..] : full;
            var target = dest.CombinePath(name).GetBasePath();

            if (target.EndsWithIgnoreCase(".exe.config") || target.EqualIgnoreCase("appsettings.json")) continue;

            WriteLog("Copy {0}", name);
            try
            {
                item.CopyTo(target.EnsureDirectory(true), true);
            }
            catch
            {
                if (File.Exists(target))
                {
                    WriteLog("Move {0}", item);
                    var deleted = target + ".del";
                    if (File.Exists(deleted)) File.Delete(deleted);
                    File.Move(target, deleted);

                    item.CopyTo(target, true);
                }
            }
        }

        WriteLog("Delete {0}", directory.FullName);
        directory.Delete(true);
    }

    /// <summary>日志对象</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>输出日志</summary>
    /// <param name="format">格式化模板</param>
    /// <param name="args">参数</param>
    public void WriteLog(String format, params Object?[] args) => Log?.Info($"[{Name}]" + format, args);
}