using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Channels;
using System.Xml;

using Pek.IO;
using Pek.Log;
using Pek.Xml;

namespace Pek.Configuration;

/// <summary>
/// 配置重新加载委托
/// </summary>
/// <returns>重新加载的配置对象</returns>
public delegate object ConfigReloadDelegate();

/// <summary>
/// 配置安全重载委托
/// </summary>
/// <param name="config">重载后的配置对象</param>
/// <param name="error">失败原因</param>
/// <returns>是否成功</returns>
public delegate Boolean ConfigTryReloadDelegate(out object? config, out string? error);

/// <summary>
/// 配置变更事件参数（增强版本）
/// </summary>
public class ConfigChangedEventArgs : EventArgs
{
    public Type ConfigType { get; }
    public object OldConfig { get; }
    public object NewConfig { get; }
    public string ConfigName { get; }
    public List<ConfigPropertyChange> PropertyChanges { get; }
    
    public ConfigChangedEventArgs(Type configType, object oldConfig, object newConfig, List<ConfigPropertyChange> propertyChanges)
    {
        ConfigType = configType;
        OldConfig = oldConfig;
        NewConfig = newConfig;
        ConfigName = configType.Name;
        PropertyChanges = propertyChanges;
    }
    
    /// <summary>
    /// 检查指定属性是否发生变更
    /// </summary>
    public bool HasPropertyChanged(string propertyName)
    {
        return PropertyChanges.Any(c => c.PropertyName.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// 获取指定属性的变更信息
    /// </summary>
    public ConfigPropertyChange? GetPropertyChange(string propertyName)
    {
        return PropertyChanges.FirstOrDefault(c => c.PropertyName.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// 是否有任何属性变更
    /// </summary>
    public bool HasChanges => PropertyChanges.Count > 0;
}

/// <summary>
/// 配置变更队列项
/// </summary>
internal class ConfigChangeQueueItem
{
    public string FilePath { get; set; } = string.Empty;
    public Type ConfigType { get; set; } = typeof(object);
    public DateTime QueueTime { get; set; } = DateTime.Now;
    public int RetryCount { get; set; } = 0;
}

/// <summary>
/// 配置管理器（极简版本）
/// </summary>
public static class ConfigManager
{
    private const String LogScope = "Pek.Configuration";

    // 核心数据存储
    private static readonly ConcurrentDictionary<Type, object> _configs = new();
    private static readonly ConcurrentDictionary<Type, JsonSerializerOptions> _serializerOptions = new();
    private static readonly ConcurrentDictionary<Type, string> _configFileNames = new();
    private static readonly ConcurrentDictionary<Type, ConfigFileFormat> _configFileFormats = new();
    private static readonly ConcurrentDictionary<string, Type> _filePathToConfigType = new();
    private static readonly ConcurrentDictionary<Type, ConfigReloadDelegate> _configReloadDelegates = new();
    private static readonly ConcurrentDictionary<Type, ConfigTryReloadDelegate> _configTryReloadDelegates = new();
    
    // 文件监控
    private static FileWatcher? _fileWatcher;
    private static readonly object _watcherLock = new();
    
    // 简化的防抖机制
    private static readonly ConcurrentDictionary<string, DateTime> _lastSaveTimes = new();
    private static readonly TimeSpan _saveIgnoreInterval = TimeSpan.FromMilliseconds(1000);
    
    // Channel 配置变更处理
    private static readonly ChannelWriter<ConfigChangeQueueItem> _channelWriter;
    private static readonly ChannelReader<ConfigChangeQueueItem> _channelReader;
    private static readonly CancellationTokenSource _cancellationTokenSource = new();
    private static readonly Task _queueProcessorTask;
    
    // 自动清理机制 - Channel版本析构函数清理
    private static readonly ChannelCleanupHelper _cleanupHelper = new();
    
    // 唯一事件
    public static event EventHandler<ConfigChangedEventArgs>? ConfigChanged;

    // 静态构造函数
    static ConfigManager()
    {
        // 初始化 Channel
        var channel = Channel.CreateUnbounded<ConfigChangeQueueItem>();
        _channelWriter = channel.Writer;
        _channelReader = channel.Reader;
        
        // 启动队列处理器
        _queueProcessorTask = Task.Run(ProcessChangeQueueAsync);
        
        // 默认日志记录（简化版本）
        ConfigChanged += (sender, e) =>
        {
            // 记录变更日志
            if (e.HasChanges)
            {
                var changes = string.Join(", ", e.PropertyChanges.Take(3).Select(c => c.ToString()));
                var moreInfo = e.PropertyChanges.Count > 3 ? $" 等{e.PropertyChanges.Count}个属性" : "";
                WriteConfigLog("Change", $"配置变更 Config={e.ConfigName} Detail={changes}{moreInfo}");
            }
            
            // ConfigManager 已经在 ReloadConfigInternal 中更新了 _configs 缓存
            // Config<T>.Current 会自动从 ConfigManager.GetConfig<T>() 获取最新实例
            // 无需额外的实例同步操作
        };
        
    }

    /// <summary>
    /// Channel自动清理辅助类 - 通过析构函数实现自动资源释放
    /// </summary>
    private sealed class ChannelCleanupHelper
    {
        private volatile bool _isDisposed = false;

        /// <summary>
        /// 析构函数 - 在垃圾回收时自动清理Channel相关资源
        /// </summary>
        ~ChannelCleanupHelper()
        {
            if (!_isDisposed)
            {
                PerformCleanup();
            }
        }

        /// <summary>
        /// 手动清理资源
        /// </summary>
        public void Dispose()
        {
            if (!_isDisposed)
            {
                PerformCleanup();
                _isDisposed = true;
                GC.SuppressFinalize(this); // 抑制析构函数调用
            }
        }

        /// <summary>
        /// 执行实际的清理操作
        /// </summary>
        private void PerformCleanup()
        {
            try
            {
                WriteConfigLog("Cleanup", "开始自动清理配置系统资源");

                // 1. 停止Channel写入
                try
                {
                    _channelWriter?.Complete();
                    WriteConfigLog("Cleanup", "Channel写入已停止");
                }
                catch (Exception ex)
                {
                    WriteConfigLog("Cleanup", $"停止Channel写入时出错 Error={TrimLogMessage(ex.Message)}");
                }
                
                // 2. 取消后台任务
                try
                {
                    _cancellationTokenSource?.Cancel();
                    WriteConfigLog("Cleanup", "后台任务取消信号已发送");
                }
                catch (Exception ex)
                {
                    WriteConfigLog("Cleanup", $"取消后台任务时出错 Error={TrimLogMessage(ex.Message)}");
                }
                
                // 3. 等待后台任务完成（有超时限制，避免析构函数阻塞）
                if (_queueProcessorTask != null && !_queueProcessorTask.IsCompleted)
                {
                    // 在析构函数中使用较短的超时时间
                    if (_queueProcessorTask.Wait(TimeSpan.FromSeconds(3)))
                    {
                        WriteConfigLog("Cleanup", "后台任务已正常完成");
                    }
                    else
                    {
                        WriteConfigLog("Cleanup", "后台任务未能在3秒内完成，继续清理");
                    }
                }
                
                // 4. 清理文件监控器
                lock (_watcherLock)
                {
                    if (_fileWatcher != null)
                    {
                        try
                        {
                            _fileWatcher.Stop();
                            _fileWatcher = null;
                            WriteConfigLog("Cleanup", "文件监控器已自动停止");
                        }
                        catch (Exception ex)
                        {
                            WriteConfigLog("Cleanup", $"停止文件监控器时出错 Error={TrimLogMessage(ex.Message)}");
                        }
                    }
                }
                
                // 5. 释放CancellationTokenSource
                try
                {
                    _cancellationTokenSource?.Dispose();
                    WriteConfigLog("Cleanup", "CancellationTokenSource已释放");
                }
                catch (Exception ex)
                {
                    WriteConfigLog("Cleanup", $"释放CancellationTokenSource时出错 Error={TrimLogMessage(ex.Message)}");
                }

                WriteConfigLog("Cleanup", "配置系统资源自动清理完成");
            }
            catch (Exception ex)
            {
                // 清理过程中的异常不应该抛出，静默处理
                try
                {
                    WriteConfigLog("Cleanup", $"自动清理过程中发生异常 Error={TrimLogMessage(ex.Message)}");
                }
                catch
                {
                    // 如果连日志都无法记录，则完全静默
                }
            }
        }
    }
    
    /// <summary>
    /// 注册配置类型
    /// </summary>
    /// <typeparam name="TConfig">配置类型</typeparam>
    /// <param name="serializerOptions">序列化选项</param>
    /// <param name="fileName">配置文件名（可选）</param>
    /// <param name="fileFormat">配置文件格式</param>
    public static void RegisterConfig<TConfig>(JsonSerializerOptions serializerOptions, string? fileName = null, ConfigFileFormat fileFormat = ConfigFileFormat.Xml)
        where TConfig : Config, new()
    {
        var configType = typeof(TConfig);
        var resolvedFormat = ResolveConfigFileFormat(configType, fileFormat);
        _serializerOptions[configType] = serializerOptions;
        _configFileNames[configType] = ResolveConfigFileName(configType, fileName, resolvedFormat);
        _configFileFormats[configType] = resolvedFormat;
        
        // 注册配置重载委托（消除反射依赖）
        _configReloadDelegates[configType] = () => ReloadConfigInternal<TConfig>();
        _configTryReloadDelegates[configType] = (out object? config, out string? error) => TryReloadConfigInternal<TConfig>(out config, out error);
        
        // 建立文件路径到配置类型的映射
        var filePath = GetConfigFilePath(configType);
        _filePathToConfigType[filePath] = configType;
        
        // 始终初始化文件监控器（自动重新加载始终启用）
        InitializeFileWatcher();
    }

    /// <summary>
    /// 获取配置实例（性能优化版本）
    /// </summary>
    /// <typeparam name="TConfig">配置类型</typeparam>
    /// <param name="forceReload">是否强制重新加载</param>
    /// <returns></returns>
    public static TConfig GetConfig<TConfig>(bool forceReload = false) where TConfig : Config, new()
    {
        var configType = typeof(TConfig);

        // 性能优化：先检查缓存，避免不必要的锁争用
        if (!forceReload && _configs.TryGetValue(configType, out var cachedConfig))
        {
            return (TConfig)cachedConfig;
        }

        // 双重检查锁模式，避免重复加载
        lock (_configs)
        {
            if (!forceReload && _configs.TryGetValue(configType, out cachedConfig))
            {
                return (TConfig)cachedConfig;
            }

            var config = LoadConfig<TConfig>();
            _configs[configType] = config;
            return config;
        }
    }

    /// <summary>
    /// 获取配置实例（通过Type）
    /// </summary>
    /// <param name="configType">配置类型</param>
    /// <param name="forceReload">是否强制重新加载</param>
    /// <returns>配置实例</returns>
    public static object GetConfig(Type configType, bool forceReload = false)
    {
        // 性能优化：先检查缓存，避免不必要的锁争用
        if (!forceReload && _configs.TryGetValue(configType, out var cachedConfig))
        {
            return cachedConfig;
        }

        // 双重检查锁模式，避免重复加载
        lock (_configs)
        {
            if (!forceReload && _configs.TryGetValue(configType, out cachedConfig))
            {
                return cachedConfig;
            }

            // 使用重载委托加载配置
            if (_configReloadDelegates.TryGetValue(configType, out var reloadDelegate))
            {
                var config = reloadDelegate();
                _configs[configType] = config;
                return config;
            }
            else
            {
                throw new InvalidOperationException($"配置类型 {configType.Name} 未注册");
            }
        }
    }

    /// <summary>
    /// 尝试获取序列化选项
    /// </summary>
    /// <param name="configType">配置类型</param>
    /// <param name="options">序列化选项</param>
    /// <returns>是否获取成功</returns>
    public static bool TryGetSerializerOptions(Type configType, out JsonSerializerOptions options)
    {
        return _serializerOptions.TryGetValue(configType, out options!);
    }

    /// <summary>
    /// 设置配置实例缓存
    /// </summary>
    /// <param name="config">配置实例</param>
    public static void SetConfig(Config config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));

        _configs[config.GetType()] = config;
    }

    /// <summary>
    /// 是否新的配置文件
    /// </summary>
    /// <param name="configType">配置类型</param>
    /// <returns>是否不存在对应配置文件</returns>
    public static Boolean IsNew(Type configType)
    {
        if (configType == null) throw new ArgumentNullException(nameof(configType));

        return !File.Exists(GetConfigFilePath(configType));
    }

    /// <summary>
    /// 获取配置类型对应的源生成类型信息。
    /// </summary>
    /// <param name="configType">配置类型</param>
    /// <param name="options">序列化选项</param>
    /// <returns>类型信息</returns>
    internal static JsonTypeInfo GetJsonTypeInfo(Type configType, JsonSerializerOptions options)
    {
        var typeInfo = options.GetTypeInfo(configType);
        if (typeInfo == null)
        {
            throw new InvalidOperationException($"配置类型 {configType.Name} 未注册 JsonTypeInfo");
        }

        return typeInfo;
    }

    /// <summary>
    /// 使用源生成类型信息序列化配置对象。
    /// </summary>
    /// <param name="config">配置对象</param>
    /// <param name="configType">配置类型</param>
    /// <param name="options">序列化选项</param>
    /// <returns>JSON 字符串</returns>
    internal static String SerializeConfig(Object config, Type configType, JsonSerializerOptions options)
    {
        var typeInfo = GetJsonTypeInfo(configType, options);
        return JsonSerializer.Serialize(config, typeInfo);
    }

    /// <summary>
    /// 使用源生成类型信息反序列化配置对象。
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="configType">配置类型</param>
    /// <param name="options">序列化选项</param>
    /// <returns>配置对象</returns>
    internal static Object? DeserializeConfig(String json, Type configType, JsonSerializerOptions options)
    {
        var typeInfo = GetJsonTypeInfo(configType, options);
        return JsonSerializer.Deserialize(json, typeInfo);
    }

    /// <summary>
    /// 加载配置（简化版本）
    /// </summary>
    private static TConfig LoadConfig<TConfig>() where TConfig : Config, new()
    {
        if (TryLoadConfig<TConfig>(true, out var config, out _)) return config;

        throw new InvalidOperationException($"配置类型 {typeof(TConfig).Name} 加载失败");
    }

    private static Boolean TryLoadConfig<TConfig>(Boolean createDefaultOnError, out TConfig config, out String? error) where TConfig : Config, new()
    {
        var configType = typeof(TConfig);
        error = null;
        config = default!;

        if (!_serializerOptions.TryGetValue(configType, out var options))
        {
            error = $"配置类型 {configType.Name} 未注册序列化选项";
            if (!createDefaultOnError) return false;
            throw new InvalidOperationException(error);
        }

        try
        {
            var filePath = GetConfigFilePath(configType);
            if (File.Exists(filePath))
            {
                var content = File.ReadAllText(filePath);
                
                if (String.IsNullOrWhiteSpace(content))
                {
                    config = CreateAndPersistDefaultConfig<TConfig>(configType, options);
                    return true;
                }

                try
                {
                    var format = GetConfigFileFormat(configType);
                    if (format == ConfigFileFormat.Xml)
                    {
                        try
                        {
                            var xmlConfig = XmlHelper.ToXmlEntity(content, configType, options) as TConfig;
                            if (xmlConfig != null)
                            {
                                config = FinalizeLoadedConfig(xmlConfig, configType, options, sourceContent: content);
                                return true;
                            }
                        }
                        catch (JsonException jsonEx)
                        {
                            error = $"配置文件XML内容错误: {filePath}, 错误: {jsonEx.Message}";
                            if (!createDefaultOnError) return false;

                            WriteConfigLog("Load", error);
                            config = CreateAndPersistDefaultConfig<TConfig>(configType, options);
                            return true;
                        }

                        config = CreateAndPersistDefaultConfig<TConfig>(configType, options);
                        return true;
                    }

                    var jsonConfig = DeserializeConfig(content, configType, options) as TConfig;
                    if (jsonConfig != null)
                    {
                        config = FinalizeLoadedConfig(jsonConfig, configType, options);
                        return true;
                    }

                    config = CreateAndPersistDefaultConfig<TConfig>(configType, options);
                    return true;
                }
                catch (JsonException jsonEx)
                {
                    error = $"配置文件JSON格式错误: {filePath}, 错误: {jsonEx.Message}";
                    if (!createDefaultOnError) return false;

                    WriteConfigLog("Load", error);
                    config = CreateAndPersistDefaultConfig<TConfig>(configType, options);
                    return true;
                }
                catch (XmlException xmlEx)
                {
                    if (GetConfigFileFormat(configType) == ConfigFileFormat.Xml)
                    {
                        var trimmed = content.TrimStart();
                        if (trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal))
                        {
                            if (!createDefaultOnError)
                            {
                                error = $"配置文件XML格式错误，待按旧JSON迁移: {filePath}, 错误: {xmlEx.Message}";
                                return false;
                            }

                            WriteConfigLog("Migrate", $"配置文件XML格式错误，尝试按旧JSON迁移 Path={filePath} Error={TrimLogMessage(xmlEx.Message)}");
                            config = TryLoadLegacyJsonAndMigrate<TConfig>(configType, options, content);
                            return true;
                        }

                        error = $"配置文件XML格式错误: {filePath}, 错误: {xmlEx.Message}";
                        if (!createDefaultOnError) return false;

                        WriteConfigLog("Load", error);
                        config = CreateAndPersistDefaultConfig<TConfig>(configType, options);
                        return true;
                    }

                    throw;
                }
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
            if (!createDefaultOnError) return false;

            XXTrace.WriteException(ex);
        }

        config = CreateAndPersistDefaultConfig<TConfig>(configType, options);
        return true;
    }

    /// <summary>
    /// 创建并静默持久化默认配置，确保首次访问即可生成配置文件。
    /// </summary>
    /// <typeparam name="TConfig">配置类型</typeparam>
    /// <param name="configType">配置类型</param>
    /// <param name="options">序列化选项</param>
    /// <returns>默认配置实例</returns>
    private static TConfig CreateAndPersistDefaultConfig<TConfig>(Type configType, JsonSerializerOptions options)
        where TConfig : Config, new()
    {
        var config = new TConfig();
        config = FinalizeLoadedConfig(config, configType, options, persistOnChange: false);
        TryWriteConfigFile(config, configType, options, writeLog: false);
        return config;
    }

    private static TConfig FinalizeLoadedConfig<TConfig>(TConfig config, Type configType, JsonSerializerOptions options, Boolean persistOnChange = true, String? sourceContent = null)
        where TConfig : Config, new()
    {
        String? before = null;
        Boolean shouldPersist = false;
        if (persistOnChange)
            before = SerializeConfig(config, configType, options);

        try
        {
            config.InvokeOnLoaded();
        }
        catch (Exception ex)
        {
            XXTrace.WriteException(ex);
        }

        if (persistOnChange)
        {
            var after = SerializeConfig(config, configType, options);
            if (!String.Equals(before, after, StringComparison.Ordinal)) shouldPersist = true;

            if (GetConfigFileFormat(configType) == ConfigFileFormat.Xml && !String.IsNullOrWhiteSpace(sourceContent))
            {
                var currentXml = config.ToXml(configType, options);
                if (!String.Equals(NormalizeContentForCompare(sourceContent), NormalizeContentForCompare(currentXml), StringComparison.Ordinal))
                    shouldPersist = true;
            }

            if (shouldPersist)
                TryWriteConfigFile(config, configType, options, writeLog: false);
        }

        return config;
    }

    private static String NormalizeContentForCompare(String content) => content.Replace("\r\n", "\n").Trim();

    /// <summary>
    /// 保存配置（性能优化版本）
    /// </summary>
    /// <param name="config">配置实例</param>
    public static void SaveConfig(Config config)
    {
        var configType = config.GetType();

        if (!_serializerOptions.TryGetValue(configType, out var options))
        {
            throw new InvalidOperationException($"配置类型 {configType.Name} 未注册序列化选项");
        }

        try
        {
            TryWriteConfigFile(config, configType, options, writeLog: true);
        }
        catch (Exception ex)
        {
            XXTrace.WriteException(ex);
            throw new InvalidOperationException($"保存配置文件失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 写入配置文件。
    /// </summary>
    /// <param name="config">配置实例</param>
    /// <param name="configType">配置类型</param>
    /// <param name="options">序列化选项</param>
    /// <param name="writeLog">是否写入日志</param>
    private static void TryWriteConfigFile(Config config, Type configType, JsonSerializerOptions options, Boolean writeLog)
    {
        var filePath = GetConfigFilePath(configType);

        // 记录保存时间，用于过滤掉代码保存触发的文件监控事件
        _lastSaveTimes[filePath] = DateTime.Now;

        var format = GetConfigFileFormat(configType);
        var content = format == ConfigFileFormat.Xml
            ? config.ToXml(configType, options)
            : SerializeConfig(config, configType, options);

        // 确保目录存在
        var directory = Path.GetDirectoryName(filePath);
        if (!String.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // 原子性写入：先写入临时文件，再替换原文件
        var tempFilePath = $"{filePath}.tmp";
        File.WriteAllText(tempFilePath, content);

        // 原子性替换文件
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        File.Move(tempFilePath, filePath);

        if (writeLog)
        {
            WriteConfigLog("Save", $"保存配置文件 Path={filePath}");
        }

        // 更新缓存
        _configs[configType] = config;
    }

    /// <summary>
    /// 获取配置文件路径
    /// </summary>
    private static string GetConfigFilePath(Type configType)
    {
        var fileName = _configFileNames.TryGetValue(configType, out var name) ? name : configType.Name;
        
        // 获取应用程序根目录下的Config文件夹
        var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var configDir = Path.Combine(appDirectory, "Config");

        if (!Directory.Exists(configDir))
        {
            Directory.CreateDirectory(configDir);
        }

        return Path.Combine(configDir, fileName);
    }

    private static ConfigFileFormat GetConfigFileFormat(Type configType) => _configFileFormats.TryGetValue(configType, out var format) ? format : ConfigFileFormat.Xml;

    private static ConfigFileFormat ResolveConfigFileFormat(Type configType, ConfigFileFormat fallbackFormat)
    {
        if (configType.GetCustomAttribute<JsonConfigFileAttribute>() != null) return ConfigFileFormat.Json;
        if (configType.GetCustomAttribute<XmlConfigFileAttribute>() != null) return ConfigFileFormat.Xml;

        var config = configType.GetCustomAttribute<ConfigAttribute>();
        if (String.IsNullOrWhiteSpace(config?.Provider)) return fallbackFormat;

        if (String.Equals(config.Provider, "json", StringComparison.OrdinalIgnoreCase)) return ConfigFileFormat.Json;
        if (String.Equals(config.Provider, "xml", StringComparison.OrdinalIgnoreCase)) return ConfigFileFormat.Xml;

        throw new InvalidOperationException($"配置类型 {configType.Name} 使用了不受支持的 Provider '{config.Provider}'。当前 Pek.AOT 仅支持 xml/json 本地配置。");
    }

    private static String ResolveConfigFileName(Type configType, String? fileName, ConfigFileFormat fileFormat)
    {
        if (String.IsNullOrWhiteSpace(fileName))
        {
            var attributeFileName = fileFormat == ConfigFileFormat.Json
                ? configType.GetCustomAttribute<JsonConfigFileAttribute>()?.FileName
                : configType.GetCustomAttribute<XmlConfigFileAttribute>()?.FileName;

            if (String.IsNullOrWhiteSpace(attributeFileName))
                attributeFileName = configType.GetCustomAttribute<ConfigAttribute>()?.Name;

            fileName = String.IsNullOrWhiteSpace(attributeFileName) ? GetDefaultConfigName(configType) : attributeFileName;
        }

        return Path.HasExtension(fileName) ? fileName : $"{fileName}.config";
    }

    private static String GetDefaultConfigName(Type configType)
    {
        var name = configType.Name;

        if (name.Length > 6 && name.EndsWith("Config", StringComparison.Ordinal))
            return name[..^6];

        if (name.Length > 7 && name.EndsWith("Setting", StringComparison.Ordinal))
            return name[..^7];

        return name;
    }

    private static TConfig TryLoadLegacyJsonAndMigrate<TConfig>(Type configType, JsonSerializerOptions options, String content)
        where TConfig : Config, new()
    {
        try
        {
            var config = DeserializeConfig(content, configType, options) as TConfig;
            if (config != null)
            {
                WriteConfigLog("Migrate", $"检测到旧JSON配置并迁移为XML Path={GetConfigFilePath(configType)}");
                TryWriteConfigFile(config, configType, options, writeLog: false);
                return config;
            }
        }
        catch (Exception ex)
        {
            XXTrace.WriteException(ex);
        }

        return CreateAndPersistDefaultConfig<TConfig>(configType, options);
    }
    
    /// <summary>
    /// 初始化文件监控器
    /// </summary>
    private static void InitializeFileWatcher()
    {
        lock (_watcherLock)
        {
            if (_fileWatcher != null)
            {
                return;
            }
            
            try
            {
                // 获取Config目录路径
                var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                var configDir = Path.Combine(appDirectory, "Config");
                
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }
                
                // 创建文件监控器
                _fileWatcher = new FileWatcher(new[] { configDir });
                _fileWatcher.EventHandler += OnConfigFileChanged;
                _fileWatcher.Start();
                
                WriteConfigLog("Watch", $"配置文件监控器已启动 Path={configDir}");
            }
            catch (Exception ex)
            {
                XXTrace.WriteException(ex);
            }
        }
    }
    
    /// <summary>
    /// 配置文件变更事件处理 - 简化版本
    /// </summary>
    private static void OnConfigFileChanged(object? sender, FileWatcherEventArgs args)
    {
        // 快速过滤：只处理 .config 文件的修改事件
        if (!args.FullPath.EndsWith(".config", StringComparison.OrdinalIgnoreCase) ||
            args.ChangeTypes != WatcherChangeTypes.Changed ||
            !_filePathToConfigType.TryGetValue(args.FullPath, out var configType))
        {
            return;
        }

        // 检查是否是代码保存导致的变更（防抖机制）
        if (IsCodeSaveTriggered(args.FullPath))
        {
            WriteConfigLog("Watch", $"忽略代码保存触发的文件变更 Path={args.FullPath}");
            return;
        }

        // 创建并入队配置变更项
        var queueItem = new ConfigChangeQueueItem
        {
            FilePath = args.FullPath,
            ConfigType = configType,
            QueueTime = DateTime.Now,
            RetryCount = 0
        };

        if (_channelWriter.TryWrite(queueItem))
        {
            WriteConfigLog("Queue", $"配置变更已加入队列 Config={configType.Name} Path={args.FullPath}");
        }
        else
        {
            WriteConfigLog("Queue", $"配置变更入队失败 Config={configType.Name} Path={args.FullPath}");
        }
    }

    /// <summary>
    /// 检查是否为代码保存触发的变更
    /// </summary>
    private static bool IsCodeSaveTriggered(string filePath)
    {
        if (_lastSaveTimes.TryGetValue(filePath, out var lastSaveTime))
        {
            return DateTime.Now - lastSaveTime < _saveIgnoreInterval;
        }
        return false;
    }

    /// <summary>
    /// Channel队列处理器 - 简化版本
    /// </summary>
    private static async Task ProcessChangeQueueAsync()
    {
        var processedCount = 0;

        try
        {
            await foreach (var item in _channelReader.ReadAllAsync(_cancellationTokenSource.Token).ConfigureAwait(false))
            {
                try
                {
                    // 简化处理：直接处理配置变更，不做复杂的重复过滤
                    if (await ProcessSingleConfigChangeAsync(item).ConfigureAwait(false))
                    {
                        processedCount++;
                        WriteConfigLog("Reload", $"配置变更处理成功 Config={item.ConfigType.Name}");
                    }
                    else
                    {
                        WriteConfigLog("Reload", $"配置变更处理失败 Config={item.ConfigType.Name}");
                    }
                }
                catch (Exception ex)
                {
                    XXTrace.WriteException(ex);
                }
            }
        }
        catch (OperationCanceledException)
        {
            WriteConfigLog("Reload", "配置变更处理器已取消");
        }
        catch (Exception ex)
        {
            XXTrace.WriteException(ex);
        }

        WriteConfigLog("Reload", $"配置变更处理器已停止 Count={processedCount}");
    }

    /// <summary>
    /// 处理单个配置变更 - 简化版本
    /// </summary>
    private static async Task<bool> ProcessSingleConfigChangeAsync(ConfigChangeQueueItem item)
    {
        try
        {
            // 文件存在性检查
            if (!File.Exists(item.FilePath))
            {
                return true; // 文件不存在视为成功处理
            }

            // 简单延迟，确保编辑器或外部进程完成文件写入
            await Task.Delay(500, _cancellationTokenSource.Token).ConfigureAwait(false);
            await WaitForFileStableAsync(item.FilePath).ConfigureAwait(false);

            // 重新加载配置并触发事件。手工保存时可能短暂读到半写入内容，因此允许短暂重试。
            for (var i = 0; i < 3; i++)
            {
                if (TryReloadAndTriggerEvents(item, out var error)) return true;

                if (i < 2)
                {
                    await Task.Delay(250, _cancellationTokenSource.Token).ConfigureAwait(false);
                    await WaitForFileStableAsync(item.FilePath).ConfigureAwait(false);
                }
                else if (!String.IsNullOrWhiteSpace(error))
                {
                    WriteConfigLog("Reload", $"配置重载失败，保留旧配置 Config={item.ConfigType.Name} Error={TrimLogMessage(error)}");
                }
            }

            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            XXTrace.WriteException(ex);
            return false;
        }
    }

    private static async Task WaitForFileStableAsync(String filePath)
    {
        DateTime? lastWriteTime = null;

        for (var i = 0; i < 5; i++)
        {
            _cancellationTokenSource.Token.ThrowIfCancellationRequested();

            if (!File.Exists(filePath)) return;

            var currentWriteTime = File.GetLastWriteTimeUtc(filePath);
            if (lastWriteTime != null && currentWriteTime == lastWriteTime.Value) return;

            lastWriteTime = currentWriteTime;
            await Task.Delay(150, _cancellationTokenSource.Token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 重新加载配置并触发事件
    /// </summary>
    private static bool TryReloadAndTriggerEvents(ConfigChangeQueueItem item, out String? error)
    {
        error = null;

        // 获取旧配置
        var oldConfig = _configs.TryGetValue(item.ConfigType, out var cached) ? cached : null;

        // 重新加载配置
        if (!_configTryReloadDelegates.TryGetValue(item.ConfigType, out var reloadDelegate))
        {
            error = $"未找到配置重载委托: {item.ConfigType.Name}";
            return false;
        }

        if (!reloadDelegate(out var newConfig, out error) || newConfig == null)
        {
            return false;
        }

        WriteConfigLog("Reload", $"配置重新加载成功 Config={item.ConfigType.Name}");

        // 触发配置变更事件
        ConfigChanged?.Invoke(null, new ConfigChangedEventArgs(item.ConfigType, oldConfig ?? newConfig, newConfig, GetPropertyChanges(oldConfig, newConfig)));

        return true;
    }

    /// <summary>
    /// 比较两个配置对象并获取属性变更信息（使用独立的JSON对比工具）
    /// </summary>
    private static List<ConfigPropertyChange> GetPropertyChanges(object? oldConfig, object newConfig)
    {
        var configType = newConfig.GetType();
        
        try
        {
            // 使用独立的JSON对比工具类
            if (_serializerOptions.TryGetValue(configType, out var options))
            {
                return ConfigJsonComparer.GetPropertyChanges(oldConfig, newConfig, configType, options);
            }
            else
            {
                // 回退到简单比较
                return ConfigJsonComparer.GetPropertyChangesSimple(oldConfig, newConfig);
            }
        }
        catch (Exception ex)
        {
            XXTrace.WriteException(ex);
            return ConfigJsonComparer.GetPropertyChangesSimple(oldConfig, newConfig);
        }
    }

    /// <summary>
    /// 内部重新加载配置方法
    /// </summary>
    /// <typeparam name="TConfig">配置类型</typeparam>
    /// <returns>重新加载的配置实例</returns>
    private static TConfig ReloadConfigInternal<TConfig>() where TConfig : Config, new()
    {
        var configType = typeof(TConfig);
        var config = LoadConfig<TConfig>();
        _configs[configType] = config;
        return config;
    }

    private static Boolean TryReloadConfigInternal<TConfig>(out object? config, out String? error) where TConfig : Config, new()
    {
        var configType = typeof(TConfig);
        if (TryLoadConfig<TConfig>(false, out var loaded, out error))
        {
            _configs[configType] = loaded;
            config = loaded;
            return true;
        }

        config = null;
        return false;
    }

    private static String TrimLogMessage(String message) => message.Length <= 160 ? message : message[..160] + "...";

    private static void WriteConfigLog(String stage, String message) => XXTrace.WriteScope(LogScope, stage, message);
}