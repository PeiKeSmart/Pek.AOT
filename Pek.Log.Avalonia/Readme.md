# Pek.Log.Avalonia

Pek.Log.Avalonia 为 Pek.AOT 提供面向 Avalonia 的日志输出扩展。

## 特性

- 基于 Avalonia Dispatcher，将日志安全投递到 UI 线程
- 保持 Pek.Log 现有日志格式，不改变 XTrace 主链输出
- 支持与文件日志、控制台日志组合使用
- 适合 NativeAOT / Trimming 场景下的 Avalonia 应用日志展示

## 快速使用

```csharp
using System.Collections.ObjectModel;
using Pek.Log;
using Pek.Log.Avalonia;

var logs = new ObservableCollection<String>();
var avaloniaLog = new AvaloniaCollectionLog(logs);

XTrace.UseAvalonia(avaloniaLog);
XTrace.WriteLine("Avalonia log ready");
```

## 说明

- 默认保留现有 XTrace.Log，并追加 Avalonia 输出
- 如需仅输出到 Avalonia，可调用 `UseAvalonia(log, false)`
- UI 层建议直接绑定 `ObservableCollection<String>`