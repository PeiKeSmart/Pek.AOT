# Pek.Configuration 配置系统

## 概述

`Pek.Configuration` 是面向 AOT 场景的强类型配置系统。

当前配置链路有两个核心约束：

1. 配置类型必须注册 `JsonSerializerContext`
2. 配置文件默认使用 XML 保存，只有显式声明时才使用 JSON

配置文件统一保存在应用根目录下的 `Config/` 目录中，默认扩展名仍为 `.config`，但内容格式由注册时的 `ConfigFileFormat` 决定。

## 当前行为

- 默认格式：XML
- 显式格式：JSON
- 首次访问 `Config<T>.Current` 时，如果文件不存在，会按默认值立即生成配置文件
- 如果某个配置已改为 XML 默认格式，但磁盘上仍是旧 JSON 内容，系统会先按旧 JSON 读入，再自动回写为 XML
- 配置文件变更会通过 `ConfigManager.ConfigChanged` 事件通知

## 使用方法

### 1. 默认 XML 配置

```csharp
using System.Text.Json.Serialization;
using Pek.Configuration;

public class AppConfig : Config<AppConfig>
{
    public String ApiUrl { get; set; } = "https://api.example.com";
    public Int32 MaxRetries { get; set; } = 3;
    public Boolean EnableLogging { get; set; } = true;

    static AppConfig() => RegisterForAot<AppConfigJsonContext>();
}

[JsonSerializable(typeof(AppConfig))]
public partial class AppConfigJsonContext : JsonSerializerContext
{
}
```

上面这种写法不传 `fileFormat`，默认就是 `ConfigFileFormat.Xml`。

### 2. 显式使用 JSON 配置

```csharp
using System.Text.Json.Serialization;
using Pek.Configuration;

[JsonConfigFile("Database")]
public class DatabaseConfig : Config<DatabaseConfig>
{
    public String ConnectionString { get; set; } = "Server=localhost;Database=App;";
    public Int32 MaxConnections { get; set; } = 50;

    static DatabaseConfig() => RegisterForAot<DatabaseConfigJsonContext>(
        fileFormat: ConfigFileFormat.Json);
}

[JsonSerializable(typeof(DatabaseConfig))]
public partial class DatabaseConfigJsonContext : JsonSerializerContext
{
}
```

### 3. 指定 XML 文件名

```csharp
using System.Text.Json.Serialization;
using Pek.Configuration;

[XmlConfigFile("Core")]
public class CoreConfig : Config<CoreConfig>
{
    public String ServiceName { get; set; } = "Demo";

    static CoreConfig() => RegisterForAot<CoreConfigJsonContext>();
}

[JsonSerializable(typeof(CoreConfig))]
public partial class CoreConfigJsonContext : JsonSerializerContext
{
}
```

该配置默认保存到 `Config/Core.config`，内容为 XML。

## 访问与保存

```csharp
var config = AppConfig.Current;

config.EnableLogging = false;
config.Save();

AppConfig.Reload();
```

## 配置事件

当前公开事件是 `ConfigManager.ConfigChanged`，不是旧文档中的 `AnyConfigChanged`。

```csharp
ConfigManager.ConfigChanged += (sender, e) =>
{
    if (e.ConfigType == typeof(AppConfig) && e.NewConfig is AppConfig newConfig)
    {
        Console.WriteLine($"配置 {e.ConfigName} 已更新：{newConfig.ApiUrl}");
    }
};
```

也可以利用 `PropertyChanges` 做更细粒度判断：

```csharp
ConfigManager.ConfigChanged += (sender, e) =>
{
    if (e.ConfigType != typeof(AppConfig)) return;
    if (!e.HasPropertyChanged(nameof(AppConfig.ApiUrl))) return;

    var change = e.GetPropertyChange(nameof(AppConfig.ApiUrl));
    Console.WriteLine($"ApiUrl 变更：{change?.OldValue} -> {change?.NewValue}");
};
```

## AOT 约束

### 1. 配置类型必须注册类型信息

- 每个配置类都必须调用 `RegisterForAot<TJsonContext>()`
- `JsonSerializerContext` 必须显式声明配置类型及其依赖类型
- 配置链路统一复用 `ConfigManager` 中已注册的 `JsonSerializerOptions`

### 2. XML 链路不再使用通用反射对象图遍历

当前 XML 读写实现基于已注册的 `JsonTypeInfo` 做中间转换：

- 对象 -> `JsonNode` -> XML
- XML -> `JsonNode` -> 对象

这意味着：

- XML 配置仍然要求对应类型已经注册 `JsonSerializerContext`
- 不再允许在配置主链路中依赖 `Activator.CreateInstance(Type)`、`PropertyInfo.GetValue/SetValue` 之类的运行时反射
- 不应通过 `UnconditionalSuppressMessage` 掩盖主链路的裁剪/AOT 警告

## 高级用法

### 1. 指定文件名和 JSON 格式

```csharp
static AppConfig()
{
    RegisterForAot<AppConfigJsonContext>(
        fileName: "AppSettings",
        fileFormat: ConfigFileFormat.Json,
        writeIndented: true,
        useCamelCase: true);
}
```

### 2. 指定 XML 格式且保留原属性名

```csharp
static AppConfig()
{
    RegisterForAot<AppConfigJsonContext>(
        fileName: "AppSettings",
        fileFormat: ConfigFileFormat.Xml,
        useCamelCase: false);
}
```

## 注意事项

1. `RegisterForAot<TJsonContext>()` 默认就是 XML，不必每次重复传 `ConfigFileFormat.Xml`
2. 只有明确要兼容旧 JSON 文件或继续输出 JSON 时，才传 `ConfigFileFormat.Json`
3. 如果配置属性新增了复杂类型、集合元素或字典值类型，必须同步补 `JsonSerializable`
4. `ConfigManager.ConfigChanged` 是当前唯一公开配置变更事件
5. 配置首次初始化仍然通过访问静态成员触发，不使用 `RuntimeHelpers.RunClassConstructor`
6. XML 与 JSON 都走同一套已注册类型信息，配置主链路不应再引入额外反射实现