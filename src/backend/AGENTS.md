# 后端公共开发指引

适用于 `src/backend/` 下的所有项目。先遵循 [根指引](../../AGENTS.md) 与 [技术基线](../../docs/development-baseline.md)，涉及业务规则时读取 [产品方案](../../docs/product-plan.md)，涉及模块协作时读取 [架构设计](../../docs/architecture.md)，验收要求见 [质量与验收](../../docs/quality.md)。

## 公共约定

- 服务端按 [目标项目结构](../../docs/architecture.md#目标项目结构) 在单个 Server 宿主内划分业务能力；业务及其专用基础设施留在 Server，Core 承担通用技术基础设施，ServiceDefaults 承担宿主默认配置；不拆分额外分层类库或独立服务。SDK 位于 `src/sdk/`，通过 HTTP 契约接入，不引用服务端程序集或数据库。
- 公共构建属性、SDK、NuGet 包版本和包源按 [统一配置约定](../../docs/development-baseline.md#net-统一配置) 在根目录维护，不在子项目重复声明或覆盖。
- 保留既有公开契约；枚举编码、配置原文、时间格式、分页或路径规则的变化不能作为无关重构顺带发生。
- 异步 I/O 传递 `CancellationToken`，不要用 `.Result` / `.Wait()` 阻塞请求线程；不要用 `Task.Run` 包装数据库调用来伪造异步。
- 优先清楚的 C# 类型与现代语法；对频繁调用路径可以采用缓存委托、静态 lambda 或 EF 编译查询，但必须验证真实收益。
- 使用依赖注入管理生命周期；DbContext 不跨并发操作共享。可测试的时间依赖可使用 `TimeProvider`。

## 技能与模块入口

- C# 符号、引用、实现与调用关系：读 [csharp-lsp](../../.agents/skills/csharp-lsp/SKILL.md)，使用随 Git 提供的 Windows x64 Roslyn CLI 和本机 .NET 10 SDK；只知道名称时先用 `find-symbols` 定位，跨项目查询及命令覆盖范围以技能说明为准，不在本项目内构建工具。
- 保持行为不变的 C# 重构：读 [csharp-refactoring](../../.agents/skills/csharp-refactoring/SKILL.md)。
- 执行 .NET 测试：读 [run-tests](../../.agents/skills/run-tests/SKILL.md)，按需要读取配套的 platform-detection 与 filter-syntax。Server 测试已采用 xUnit v3 + MTP 与真实 SQLite，命令和范围见 [自动化测试](../../docs/local-development.md#server-自动化测试)；不复制上游测试框架偏好。
- Server 的 HTTP、存储和认证实现：读 [Server 指引](Trelix.Server/AGENTS.md)。
- Core 的通用技术实现：先核对 [职责边界](../../docs/architecture.md#通用技术与业务基础设施边界)，按具体能力选择技能；不将业务模型、持久化或权限规则移入 Core。
- 遥测、健康检查、服务发现与 HTTP 弹性：读 [ServiceDefaults 指引](Trelix.ServiceDefaults/AGENTS.md)。

统一启动与解决方案构建见 [本地开发](../../docs/local-development.md)；模块专属验证留在对应模块指引。完整技能选择见 [技能索引](../../.agents/README.md)。
