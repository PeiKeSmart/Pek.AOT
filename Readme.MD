# Pek.AOT

Pek.AOT 是面向 NativeAOT 与 Trimming 场景的基础能力库，当前已包含配置系统、日志、线程调度、对象池等基础模块。

仓库同时包含可独立打包的 Avalonia 日志扩展项目 Pek.Log.Avalonia，用于将 Pek.Log 输出投递到 Avalonia UI。

## 当前重点

- 配置系统默认输出 XML，显式声明时可切换为 JSON
- XML 配置链路基于已注册的 JsonSerializerContext，不依赖运行时反射对象图遍历
- 支持接近 DH.NCore 的配置声明方式：`[Config("...")] + Config<TConfig, TJsonContext>`

## XTrace 快速示例

```csharp
using Pek;
using Pek.Log;

var setting = Setting.Current;

Console.WriteLine($"LogLevel = {setting.LogLevel}");
XTrace.UseConsole();
XTrace.WriteLine("Pek.AOT ready");
```

首次运行时会在应用目录生成 `Config/Core.config` 和 `Log/*.log`。

## Avalonia 日志扩展

可使用 Pek.Log.Avalonia 将日志输出到 Avalonia 绑定集合：

```csharp
using System.Collections.ObjectModel;
using Pek.Log;
using Pek.Log.Avalonia;

var logs = new ObservableCollection<String>();
var avaloniaLog = new AvaloniaCollectionLog(logs);

XTraceAvaloniaExtensions.UseAvalonia(avaloniaLog);
XTrace.WriteLine("Avalonia log ready");
```

仓库内已提供可直接运行的桌面示例 `Samples/AvaloniaLogSample`，用于演示按钮日志、异常日志和后台定时日志如何同步投递到 Avalonia 列表。

## 示例项目

- `Samples/AvaloniaLogSample`：Avalonia UI 日志展示示例
- `Samples/TimerXSample`：TimerX 调度与并发行为示例
- `Samples/XTraceSample`：最小日志入口示例，代码已统一使用 `Pek.Log` / `XTrace`

## 配置示例

```csharp
using System.ComponentModel;
using System.Text.Json.Serialization;
using Pek.Configuration;

[DisplayName("网络设置")]
[Config("Socket")]
public class SocketSetting : Config<SocketSetting, SocketSettingJsonContext>
{
	[Description("网络调试")]
	public Boolean Debug { get; set; }

	[Description("会话超时时间。默认20*60秒")]
	public Int32 SessionTimeout { get; set; } = 20 * 60;
}

[JsonSerializable(typeof(SocketSetting))]
public partial class SocketSettingJsonContext : JsonSerializerContext
{
}
```

更多配置细节见 `Pek.AOT/Configuration/README.md`。