## 日志
统一 ILog 接口，内置控制台、文本文件、网络日志、事件监听和基础埋点实现。

当前目录已按 DH.NCore 的 Log 结构对齐，主要差异如下：
- 命名空间已对齐为 Pek.Log。
- XTrace 为主入口，XXTrace 作为兼容入口保留。
- 已补齐基础埋点类型：ITracer、ISpan、ISpanBuilder、ITracerResolver、ITracerFeature。
- 已补齐低依赖日志能力：ActionLog、CodeTimer、PerfCounter、TimeCost、LogEventListener、TraceStream、NetworkLog。
- TextControlLog 依赖 WinForms，当前仓库为跨平台多目标库，尚未引入对应平台依赖，因此暂未迁入。

当前日志输出格式仍然保持 Pek.AOT 现有实现，核心由 WriteLogEventArgs 控制。