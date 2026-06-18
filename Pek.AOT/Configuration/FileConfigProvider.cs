using Pek.Extension;
using Pek.Log;
using Pek.Threading;
using System.IO;

namespace Pek.Configuration;

/// <summary>文件配置提供者</summary>
/// <remarks>
/// 每个提供者实例对应一个配置文件，支持热更新。
/// 同时使用 FileSystemWatcher 事件驱动和定时器轮询双重机制感知文件变更，
/// 当事件驱动可用时定时器周期自动拉长作为兜底，不可用时定时器周期较短以保证及时感知。
/// AOT 适配：移除 Bind 模型绑定功能，仅保留文件读写与热更新。
/// </remarks>
public abstract class FileConfigProvider : ConfigProvider
{
    #region 属性
    /// <summary>文件名。最高优先级，优先于模型特性指定的文件名</summary>
    public String? FileName { get; set; }

    /// <summary>更新周期。默认5秒，0秒表示不做自动更新。事件驱动可用时自动拉长为60秒兜底轮询</summary>
    public Int32 Period { get; set; } = 5;

    private FileSystemWatcher? _watcher;
    private TimerX? _timer;
    private Boolean _reading;
    private DateTime _lastRefreshTime;
    private DateTime _lastTime;
    #endregion

    #region 构造
    /// <summary>销毁</summary>
    /// <param name="disposing">是否释放托管资源</param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        _timer.TryDispose();

        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnFileChanged;
            _watcher.TryDispose();
            _watcher = null;
        }
    }

    /// <summary>已重载。输出友好信息</summary>
    /// <returns>包含文件名的字符串表示</returns>
    public override String ToString() => $"{GetType().Name} FileName={FileName}";
    #endregion

    #region 方法
    /// <summary>初始化</summary>
    /// <param name="value">配置文件名</param>
    public virtual void Init(String value)
    {
        // 加上文件名
        if (FileName.IsNullOrEmpty() && !value.IsNullOrEmpty())
        {
            // 加上配置目录
            var str = value;
            if (!str.StartsWithIgnoreCase("Config/", "Config\\")) str = "Config".CombinePath(str);

            FileName = str;
        }
    }

    /// <summary>加载配置</summary>
    /// <returns>是否加载成功</returns>
    public override Boolean LoadAll()
    {
        // 准备文件名
        var fileName = FileName;
        if (fileName.IsNullOrEmpty()) throw new ArgumentNullException(nameof(FileName));

        fileName = fileName.GetBasePath();

        IsNew = true;

        if (!File.Exists(fileName)) return false;

        // 读取文件，换个对象，避免数组元素在多次加载后重叠
        var section = new ConfigSection { };
        OnRead(fileName, section);
        Root = section;

        IsNew = false;
        _lastTime = fileName.AsFile().LastWriteTime;

        return true;
    }

    /// <summary>读取配置文件</summary>
    /// <param name="fileName">文件名</param>
    /// <param name="section">配置段</param>
    protected abstract void OnRead(String fileName, IConfigSection section);

    /// <summary>保存配置树到数据源</summary>
    /// <returns>是否保存成功</returns>
    public override Boolean SaveAll()
    {
        // 准备文件名
        var fileName = FileName;
        if (fileName.IsNullOrEmpty()) throw new ArgumentNullException(nameof(FileName));

        fileName = fileName.GetBasePath();
        fileName.EnsureDirectory(true);

        // 写入文件
        OnWrite(fileName, Root);
        _lastTime = fileName.AsFile().LastWriteTime;

        // 通知绑定对象，配置数据有改变
        NotifyChange();

        return true;
    }

    /// <summary>写入配置文件</summary>
    /// <param name="fileName">文件名</param>
    /// <param name="section">配置段</param>
    protected virtual void OnWrite(String fileName, IConfigSection section)
    {
        var str = GetString(section);
        var old = "";
        if (File.Exists(fileName)) old = File.ReadAllText(fileName)?.Trim() ?? "";

        if (str != null && str != old)
        {
            if (old.IsNullOrEmpty())
            {
                XTrace.WriteLine("新建配置：{0}", fileName);
            }
            else
            {
                // 如果文件内容有变化，输出差异
                var i = 0;
                while (i < str.Length && i < old.Length && str[i] == old[i]) i++;

                var s = i > 16 ? i - 16 : 0;
                var e = i + 32 < old.Length ? i + 32 : old.Length;
                var ori = old[s..e].Replace("\r", "\\r").Replace("\n", "\\n");
                var e2 = i + 32 < str.Length ? i + 32 : str.Length;
                var diff = str[s..e2].Replace("\r", "\\r").Replace("\n", "\\n");

                XTrace.WriteLine("更新配置：{0}，原：\"{1}\"，新：\"{2}\"", fileName, ori, diff);
            }

            File.WriteAllText(fileName, str);
        }
    }

    /// <summary>获取字符串形式</summary>
    /// <param name="section">配置段</param>
    /// <returns>配置的字符串表示；默认返回 null</returns>
    public virtual String? GetString(IConfigSection? section = null) => null;
    #endregion

    #region 热更新
    /// <summary>初始化文件监控。同时启用事件驱动和定时器轮询，事件驱动可用时定时器周期较长</summary>
    protected void InitWatcher()
    {
        if (_watcher != null || _timer != null) return;
        lock (this)
        {
            if (_watcher != null || _timer != null) return;

            var fileName = FileName?.GetBasePath();
            if (fileName.IsNullOrEmpty()) return;

            var hasWatcher = false;

            // 尝试使用 FileSystemWatcher 事件驱动
            try
            {
                var directory = Path.GetDirectoryName(fileName);
                var filter = Path.GetFileName(fileName);

                if (!directory.IsNullOrEmpty() && !filter.IsNullOrEmpty())
                {
                    var watcher = new FileSystemWatcher(directory, filter)
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                        EnableRaisingEvents = true
                    };

                    // 订阅文件变更事件
                    watcher.Changed += OnFileChanged;
                    _watcher = watcher;
                    hasWatcher = true;
                }
            }
            catch (Exception ex)
            {
                // FileSystemWatcher 在某些 Linux/Android 系统上可能不支持或不可靠
                XTrace.WriteLine("FileSystemWatcher 创建失败：{0}", ex.Message);
            }

            // 同时启动定时器轮询，事件驱动可用时周期较长作为兜底
            if (_timer == null)
            {
                var p = Period;
                if (p <= 0) p = 5;

                // 事件驱动可用时，定时器作为兜底，周期拉长为60秒
                if (hasWatcher && p < 60) p = 60;

                _timer = new TimerX(DoRefresh, null, p * 1000, p * 1000) { Async = true };
            }
        }
    }

    /// <summary>文件变更事件处理</summary>
    /// <param name="sender">事件源</param>
    /// <param name="e">事件参数</param>
    private void OnFileChanged(Object? sender, FileSystemEventArgs e)
    {
        var now = DateTime.UtcNow;

        // 防抖动：500ms 内只处理一次变更
        if ((now - _lastRefreshTime).TotalMilliseconds < 500) return;

        _lastRefreshTime = now;

        // 延迟 200ms 执行，等待文件写入完成
        ThreadPool.UnsafeQueueUserWorkItem(_ =>
        {
            Thread.Sleep(200);
            DoRefresh(null);
        }, null);
    }

    /// <summary>定时刷新配置</summary>
    /// <param name="state">状态对象</param>
    private void DoRefresh(Object? state)
    {
        if (_reading) return;
        if (FileName.IsNullOrEmpty()) return;

        var fileName = FileName.GetBasePath();
        var fi = fileName.AsFile();
        if (!fi.Exists) return;

        fi.Refresh();
        if (_lastTime.Year > 2000 && fi.LastWriteTime <= _lastTime) return;
        _lastTime = fi.LastWriteTime;

        XTrace.WriteLine("配置文件改变，重新加载 {0}", fileName);

        _reading = true;
        try
        {
            var section = new ConfigSection { };
            OnRead(fileName, section);
            Root = section;

            NotifyChange();
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }
        finally
        {
            _reading = false;
        }
    }
    #endregion
}
