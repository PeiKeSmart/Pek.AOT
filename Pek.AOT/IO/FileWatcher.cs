namespace Pek.IO;

/// <summary>文件变更事件参数</summary>
public class FileWatcherEventArgs : EventArgs
{
    /// <summary>完整文件路径</summary>
    public String FullPath { get; }

    /// <summary>文件变更类型</summary>
    public WatcherChangeTypes ChangeTypes { get; }

    /// <summary>初始化事件参数</summary>
    /// <param name="fullPath">完整文件路径</param>
    /// <param name="changeTypes">文件变更类型</param>
    public FileWatcherEventArgs(String fullPath, WatcherChangeTypes changeTypes)
    {
        FullPath = fullPath;
        ChangeTypes = changeTypes;
    }
}

/// <summary>最小可用的文件监控器</summary>
public class FileWatcher : IDisposable
{
    private readonly List<FileSystemWatcher> _watchers = [];

    /// <summary>文件变更事件</summary>
    public event EventHandler<FileWatcherEventArgs>? EventHandler;

    /// <summary>初始化文件监控器</summary>
    /// <param name="paths">要监控的目录列表</param>
    public FileWatcher(IEnumerable<String> paths)
    {
        foreach (var item in paths)
        {
            if (String.IsNullOrWhiteSpace(item)) continue;

            var watcher = new FileSystemWatcher(item)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                Filter = "*.config"
            };

            watcher.Changed += OnChanged;
            watcher.Created += OnChanged;
            watcher.Renamed += OnRenamed;

            _watchers.Add(watcher);
        }
    }

    /// <summary>启动监控</summary>
    public void Start()
    {
        foreach (var item in _watchers)
        {
            item.EnableRaisingEvents = true;
        }
    }

    /// <summary>停止监控</summary>
    public void Stop()
    {
        foreach (var item in _watchers)
        {
            item.EnableRaisingEvents = false;
        }
    }

    private void OnChanged(Object sender, FileSystemEventArgs e) => EventHandler?.Invoke(this, new FileWatcherEventArgs(e.FullPath, e.ChangeType));

    private void OnRenamed(Object sender, RenamedEventArgs e) => EventHandler?.Invoke(this, new FileWatcherEventArgs(e.FullPath, WatcherChangeTypes.Changed));

    /// <summary>释放资源</summary>
    public void Dispose()
    {
        foreach (var item in _watchers)
        {
            item.Dispose();
        }

        _watchers.Clear();
    }
}