---
applyTo: "Pek.AOT/**"
---

# AOT 协作指令

适用于 `Pek.AOT` 仓库中的 AOT 兼容实现、配置系统、日志系统、定时调度与兼容层开发任务。

---

## 1. 定位与边界

### 1.1 仓库定位

本仓库不是泛化的 NativeAOT 教程仓库，而是 **PeiKeSmart/NewLife 常用能力的 AOT 可用实现与兼容替代**。

当前重点包括：

- `Pek.Configuration`：AOT 兼容配置系统
- `Pek.Logging`：AOT 可用日志与 `XXTrace` 配置
- `Threading`：避免动态代理依赖的定时与调度能力
- `IO` / `Runtime` / `MachineInfo`：基础运行时与路径能力
- `NewLife.*` 兼容命名空间：为旧调用方提供迁移友好的 API 表面

### 1.2 本指令负责内容

**包含**：

- 新增或修改 AOT 兼容类型
- 配置类、序列化上下文、注册逻辑
- 对 `NewLife.*` 兼容 API 的迁移与补齐
- 避免裁剪失败、反射失效、动态代码依赖的实现改造
- 与 AOT 行为强相关的 README、样例同步

**不包含**：

- XCode 数据建模与实体生成
- 网络协议与 Socket 开发
- 性能基准测试设计

---

## 2. 核心原则

### 2.1 先复用现有 AOT 模式，再新增实现

新增能力前，先从本仓库检索是否已有相同模式：

- 配置类优先复用 `Config<TConfig>` + `RegisterForAot<TJsonContext>()`
- 序列化优先复用 `System.Text.Json` 源生成上下文
- 兼容层优先保持既有命名空间、类型名、成员签名

禁止为了“更现代”而随意改坏旧调用方兼容性。

### 2.2 优先消除根因，不做表面 AOT 兼容

当问题根因是反射、动态代码、隐式序列化、静态初始化顺序时，应直接改造根因，禁止只加注释或回避异常。

### 2.3 优先做 AOT 等价实现，不要直接删除能力

对于上游已有能力，正确策略不是“凡是不适合 AOT 就删掉”，而是**先尝试用 AOT 友好的方式做等价实现**，尽量保持目录结构、类型名、成员签名和行为语义与上游一致。

只有在满足以下任一条件时，才允许暂缓迁移或明确降级：

- 能力本身离不开动态代理、运行时代码生成或通用反射映射，且无法改写为静态注册、源生成、显式映射表或委托表
- 能力本身强依赖特定 UI 平台（如 WinForm/WPF 控件）或当前仓库不承载的平台模型
- 为了保留该能力，必须引入与当前迁移目标无关的大块重型依赖，导致 AOT 主链显著复杂化或不可裁剪

如果某项能力暂缓迁移，必须明确说明：

- 为什么它不能直接用 AOT 等价方式落地
- 当前是否保留 API 表面兼容
- 后续若要继续推进，需要补哪些最小相关部分

### 2.4 保持多目标框架一致行为

当前项目目标框架为 `net8.0;net9.0;net10.0`。新增能力必须评估三个目标框架下行为是否一致，不允许只在单一目标框架成立。

---

## 3. 配置系统规则

### 3.1 新增配置类的标准写法

新增配置类时，必须同时满足以下要求：

1. 配置类继承 `Config<TConfig>`
2. 配置类提供静态构造函数
3. 静态构造函数中调用 `RegisterForAot<TJsonContext>()`
4. 提供对应的 `JsonSerializerContext` 类型
5. 用 `[JsonSerializable(typeof(...))]` 显式标注配置类型及其依赖类型

推荐模式：

```csharp
public class AppSetting : Config<AppSetting>
{
    static AppSetting() => RegisterForAot<AppSettingJsonContext>();
}

[JsonSerializable(typeof(AppSetting))]
public partial class AppSettingJsonContext : JsonSerializerContext
{
}
```

### 3.2 序列化类型必须显式登记

新增配置属性后，如果属性类型不是简单基础类型，必须检查对应 `JsonSerializerContext` 是否已覆盖：

- 枚举
- 自定义对象
- 集合元素类型
- `Dictionary<TKey, TValue>`
- 嵌套对象图

**禁止**假设运行时反射会自动兜底。

### 3.3 配置序列化必须走已注册选项

涉及配置持久化、快照、比较、重载时，优先通过 `ConfigManager.TryGetSerializerOptions()` 获取已注册的 `JsonSerializerOptions`，不要绕开注册表直接使用默认序列化选项。

进一步要求：

- 优先通过 `ConfigManager.SerializeConfig()` / `ConfigManager.DeserializeConfig()` 复用统一序列化入口
- 禁止在配置链路中调用 `JsonSerializer.Serialize(Object, Type, JsonSerializerOptions)`、`JsonSerializer.Deserialize(String, Type, JsonSerializerOptions)` 等依赖运行时类型分析的动态重载
- 必须确保 `JsonSerializerOptions.GetTypeInfo(type)` 可以拿到对应 `JsonTypeInfo`，否则视为注册不完整

### 3.4 注意静态初始化顺序

配置类、日志类存在初始化先后依赖时：

- 不要在静态构造函数首段写日志
- 不要在类型初始化阶段做重 IO、重依赖调用
- 优先先完成注册，再做惰性加载

如果修改 `XXTrace`、`ConfigManager`、配置默认值回退逻辑，必须检查是否重新引入初始化环。

### 3.5 配置初始化触发方式

`Config<TConfig>.Current` 的初始化应优先依赖类型自身的静态构造触发，不要重新引入 `RuntimeHelpers.RunClassConstructor(typeof(TConfig).TypeHandle)` 这类会带来裁剪分析不确定性的写法。

推荐方式：

- 通过访问静态成员或创建 `new TConfig()` 触发派生类静态构造
- 注册逻辑保持在配置类型自己的静态构造函数中

---

## 4. AOT 兼容实现规则

### 4.1 禁止优先引入以下实现方式

以下方式在本仓库中默认视为高风险，除非已有同类先例且有明确必要性，否则不要新增：

- 基于程序集扫描的自动注册
- 依赖 `Type.GetType()` + 约定字符串的核心路径
- `Activator.CreateInstance()` 驱动的通用工厂
- `MethodInfo.Invoke()`、`PropertyInfo.GetValue()` 等热点反射调用
- `Expression.Compile()`、运行时代码生成、动态代理
- 依赖裁剪器保留隐式成员而不做显式声明

允许存在少量兼容性反射，但必须证明：

1. 不在高频路径
2. 有失败兜底
3. 不影响 AOT 主链路

### 4.2 优先采用显式、静态、可裁剪的实现

优先顺序：

1. 泛型静态注册
2. 源生成上下文
3. `JsonTypeInfo` / 显式映射表 / 委托表
4. 小范围兼容性反射兜底

### 4.3 完全禁止 suppress 掩盖 AOT 风险

本仓库定位就是 AOT 主用库，因此**完全不允许**使用任何 suppress 来压掉裁剪或 AOT 风险警告，包括但不限于：

- `[UnconditionalSuppressMessage("Trimming", ...)]`
- `[UnconditionalSuppressMessage("Aot", ...)]`
- 任何针对 IL2026 / IL3050 / IL2067 / IL2070 等问题的 suppress
- 任何只有 `Justification`、但没有消除根因的“解释性压警告”做法

出现此类警告时，唯一正确处理方式是：

1. 改实现，消除运行时反射、动态创建、动态代码生成等根因
2. 改成静态注册、源生成、`JsonTypeInfo`、显式映射、委托表等 AOT 友好方案
3. 必要时收窄能力边界，宁可减少通用性，也不要保留高风险主链路

如果一段逻辑仍依赖运行时反射、`Activator.CreateInstance(Type)`、`PropertyInfo.GetValue/SetValue`、通用对象图遍历等能力，就算可以通过 suppress 消除警告，也**一律视为不符合本仓库要求**。

对配置/XML/序列化链路执行更严格标准：

- 必须优先复用已注册的 `JsonTypeInfo`
- 必须优先通过显式类型信息、映射表或委托完成转换
- 不允许在主链路中保留通用反射读写器
- 不允许为了兼容旧实现而用 suppress 掩盖真实风险

### 4.4 兼容层改造规则

若为了兼容旧代码新增 `NewLife.*` 命名空间类型：

- 优先保持原 API 名称和主要签名稳定
- 行为可以内部转发到 `Pek.*` 实现
- 不要为兼容层再复制一套完整逻辑，优先复用主实现
- 若发现兼容层与主实现重复，优先提取公共逻辑，而不是双份维护

进一步要求：

- 做 AOT 等价实现时，不得因为实现替换而随意修改上游已有的方法名、成员名、局部结构名或辅助函数名，即使这些成员是 `private`
- 在不破坏编译兼容与行为语义的前提下，参数名也应尽量与上游保持一致，避免仅因命名差异造成对照迁移困难
- 对 `public`/`protected` API，若参数名参与命名参数调用或会影响生成文档，原则上不得无故改名
- 若原实现因 AOT/裁剪限制无法直接保留，只能在**保持原命名和调用关系尽量不变**的前提下替换实现体，优先采用同名重载、同名包装或内部静态分发
- 只有在名称本身会直接造成歧义、冲突或错误，且无法通过同名重载、显式类型、包装层等方式解决时，才允许调整内部名称或参数名；此时必须在说明中明确原因

---

## 5. 文档与样例同步

以下变更通常需要同步文档或样例：

- 新增配置类注册方式
- 修改配置文件名约定
- 新增 `JsonSerializable` 依赖类型
- 修改 `TimerX` / `XXTrace` 等对外使用方式
- 调整兼容命名空间或迁移方式

优先检查并同步：

- `Pek.AOT/Configuration/README.md`
- 根目录 `Readme.MD`
- `Samples/TimerXSample`
- `Samples/XTraceSample`

文档内容必须与当前代码一致。若发现 README 仍引用旧 API（如旧注册方法名），应在本次改动中一并修正。

---

## 6. 验证要求

### 6.1 代码变更的最低验证

涉及源码修改时至少做到：

1. 编译受影响项目
2. 检查新增 `JsonSerializerContext` 是否成功生成
3. 检查配置注册路径是否仍可触发

### 6.2 以下场景要提高验证强度

| 场景 | 至少验证 |
|------|---------|
| 修改配置系统 | 编译 `Pek.AOT.csproj`，并验证配置加载/保存链路 |
| 修改日志配置 | 编译相关样例或验证 `Setting` 注册路径 |
| 修改定时器/线程调度 | 编译样例并检查公开 API 无回归 |
| 修改兼容层公共 API | 检查旧命名空间调用是否仍能通过编译 |

如果无法执行运行验证，必须明确说明未验证部分与风险点。

---

## 7. 常见反模式（禁止）

- ❌ 新增配置类但未注册 `RegisterForAot<TJsonContext>()`
- ❌ 新增复杂属性类型但遗漏 `[JsonSerializable]`
- ❌ 在配置链路中直接调用默认 `JsonSerializer.Serialize/Deserialize`
- ❌ 在配置链路中使用 `Type + JsonSerializerOptions` 的动态重载，导致 IL2026 / IL3050
- ❌ 依赖反射扫描自动发现配置类
- ❌ 在本仓库任何位置使用 `UnconditionalSuppressMessage` 或同类 suppress 掩盖裁剪/AOT 风险
- ❌ 为兼容旧 API 复制整份业务逻辑，而不是复用 `Pek.*` 实现
- ❌ 修改静态初始化逻辑时重新引入 `XXTrace` 与 `ConfigManager` 的环形依赖
- ❌ 让 `JsonSerializerContext` 继承另一个也带 `[JsonSerializable]` 的上下文，导致源生成成员隐藏冲突
- ❌ 只改代码不改 README / 样例，导致仓库示例失真
- ❌ 仅验证单一目标框架就宣称 AOT 兼容完成

---

## 8. 工作流

触发检查 → 检索现有 AOT 实现 → 识别反射/动态依赖 → 选定静态化方案 → 实施 → 编译验证 → 同步文档/样例 → 说明风险

处理 AOT 相关任务时，默认优先检查：

- 是否已存在同类 `JsonSerializerContext`
- 是否已存在兼容层转发实现
- 是否会影响静态初始化顺序
- 是否需要同步 README 和 Sample

---

（完）