# Trelix 项目开发 skills

开发任务从 [根 AGENTS.md](../AGENTS.md) 和 [技术基线](../docs/development-baseline.md) 开始，按 [文档索引](../docs/README.md#按任务读取) 读取相关产品、架构与验收内容，并核对实际源码、依赖和脚本。后端任务再读 [后端公共指引](../src/backend/AGENTS.md) 与相关模块指引，统一启动和联调见 [本地开发](../docs/local-development.md)。本目录保存可随项目共享的技能，不改变用户全局 skills。

本文件是项目技能索引；技能新增、修改、重命名或删除时，按根 `AGENTS.md` 要求同步更新对应条目与使用说明，并检查链接有效性。

业务代码的位置与依赖方向以 [目标项目结构](../docs/architecture.md#目标项目结构) 为准；技能示例不决定项目分层、目录或业务范围。

## 按任务选择

| 技能 | 使用场景 | 项目绑定 |
| --- | --- | --- |
| [vue-best-practices](skills/vue-best-practices/SKILL.md) | Vue 组件、响应式、Vite + Vue 开发 | Composition API，后续业务代码使用 TypeScript |
| [vue-debug-guides](skills/vue-debug-guides/SKILL.md) | Vue 运行错误、异步和响应式问题 | 根据具体问题读取参考 |
| [create-adaptable-composable](skills/create-adaptable-composable/SKILL.md) | 可复用 composable | 用于项目逻辑复用，不开发 skills |
| [vue-testing-best-practices](skills/vue-testing-best-practices/SKILL.md) | Vue 组件与浏览器测试 | 当前没有前端测试工具链；接入遵循根指引 |
| [vue-router-best-practices](skills/vue-router-best-practices/SKILL.md) | Router 导航和路由生命周期 | 使用 Router 时读取；当前没有该依赖 |
| [vue-pinia-best-practices](skills/vue-pinia-best-practices/SKILL.md) | Pinia store 与共享状态 | 使用 Pinia 时读取；当前没有该依赖 |
| [dotnet-webapi](skills/dotnet-webapi/SKILL.md) | API、DTO、OpenAPI、错误处理 | 使用 Minimal APIs、内置验证与 OpenAPI |
| [csharp-lsp](skills/csharp-lsp/SKILL.md) | 按名称查找 C# 符号，以及引用、定义、实现、调用图与字段分析 | Git 跟踪的 Windows x64 预编译 CLI，依赖本机 .NET 10 SDK；为重构提供语义影响分析 |
| [csharp-refactoring](skills/csharp-refactoring/SKILL.md) | 保持行为的 C# 重构 | 项目边界为 `Trelix.slnx` 所在目录 |
| [optimizing-ef-core-queries](skills/optimizing-ef-core-queries/SKILL.md) | EF 查询性能与 N+1 排查 | 需要判断项目技术基线，这个技能参考使用 |
| [configuring-opentelemetry-dotnet](skills/configuring-opentelemetry-dotnet/SKILL.md) | 追踪、指标、日志与 OTLP | 复用 ServiceDefaults 已有注册 |
| [dotnet-dump-analysis](skills/dotnet-dump-analysis/SKILL.md) | .NET dump、Linux coredump、内存增长、LOH 与引用链分析 | 先分析已有快照，按需采集；多时点证据判断泄漏 |
| [run-tests](skills/run-tests/SKILL.md) | .NET 测试执行与命令 | Server 已采用 xUnit v3 + MTP 原生命令模式，仍按实际项目核对，不将零测试视为通过 |
| [platform-detection](skills/platform-detection/SKILL.md) | 识别 .NET 测试框架/平台 | 为 run-tests 提供按需配套能力 |
| [filter-syntax](skills/filter-syntax/SKILL.md) | 按测试、类、分类等筛选 | 使用实际运行平台对应语法 |

无需每次读取所有技能。可自然描述任务，也可显式指定，如“使用 vue-best-practices 实现配置编辑页面”或“使用 dotnet-webapi 增加配置读取端点”。项目 agent 指引负责将任务路由到相关技能。

Dump 分析可直接指定：“使用 dotnet-dump-analysis 分析 `<dump-file>`，定位内存增长原因。”将占位符替换为实际文件；已有文件时先做堆统计、引用链追踪和多时点对比，需要采集时按技能内的 [采集与进阶诊断](skills/dotnet-dump-analysis/references/collection.md) 操作。

## csharp-lsp 的使用

仓库随 Git 跟踪预编译的 Windows x64 `roslyn-tool.exe` 与技能文档；工具源码在独立 `csharp-lsp` 工程维护，Trelix 不嵌入该工程。使用时准备项目基线要求的 .NET 10 SDK，并先还原被分析项目的依赖；无需在 Trelix 内构建工具。产物版本、大小、哈希及平台限制见 [工具路径](skills/csharp-lsp/SKILL.md#工具路径)。

在 Trelix 根目录运行：

```powershell
$roslynTool = (Resolve-Path '.\.agents\skills\csharp-lsp\scripts\roslyn-tool.exe').Path
& $roslynTool --help
& $roslynTool find-symbols --help
```

单项目查询传入相关 `.csproj`（例如 `src/backend/Trelix.Server/Trelix.Server.csproj`）；位置查询和 `find-symbols` 支持根目录的 `Trelix.slnx`。只知道名称时先用 `find-symbols --name <name>`，再把返回的声明位置传给引用或类型查询。源码路径优先使用绝对路径，行列从 1 开始且应落在目标标识符上。字段分析只接收 `.csproj`；`find-usings` 对解决方案仅分析第一个加载项目。完整示例与加载诊断处理见 [csharp-lsp](skills/csharp-lsp/SKILL.md)。

这是项目内的语义分析 CLI，不会自动注册 LSP/MCP 服务，也不执行重命名；实际重构与验证由 `csharp-refactoring` 指导。符号查询默认 JSON，字段分析默认 text，推荐显式传入 `--format json`。`-o` 仅适用于两个字段分析命令。
