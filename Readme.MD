# Pek.AOT

Pek.AOT 是面向 NativeAOT 与 Trimming 场景的基础能力库，当前已包含配置系统、日志、线程调度、对象池等基础模块。

## 当前重点

- 配置系统默认输出 XML，显式声明时可切换为 JSON
- XML 配置链路基于已注册的 JsonSerializerContext，不依赖运行时反射对象图遍历
- 支持接近 DH.NCore 的配置声明方式：`[Config("...")] + Config<TConfig, TJsonContext>`

## XXTrace 快速示例

```csharp
using Pek;
using Pek.Logging;

var setting = Setting.Current;

Console.WriteLine($"LogLevel = {setting.LogLevel}");
XXTrace.UseConsole();
XXTrace.WriteLine("Pek.AOT ready");
```

首次运行时会在应用目录生成 `Config/Core.config` 和 `Log/*.log`。

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