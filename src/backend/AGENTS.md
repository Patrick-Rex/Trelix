# 后端公共开发指引

适用于 `src/backend/` 下的所有项目。先遵循 [根指引](../../AGENTS.md) 与 [技术基线](../../docs/development-baseline.md)，涉及业务规则时读取 [产品方案](../../docs/product-plan.md)，涉及模块协作时读取 [架构设计](../../docs/architecture.md)，验收要求见 [质量与验收](../../docs/quality.md)。

## 公共约定

- 服务端按 [目标项目结构](../../docs/architecture.md#目标项目结构) 在单个 Server 宿主内划分业务能力；业务及其专用基础设施留在 Server，Core 承担通用技术基础设施，ServiceDefaults 承担宿主默认配置；不拆分额外分层类库或独立服务。SDK 位于 `src/sdk/`，通过 HTTP 契约接入，不引用服务端程序集或数据库。
- 公共构建属性、SDK、NuGet 包版本和包源按 [统一配置约定](../../docs/development-baseline.md#net-统一配置) 在根目录维护，不在子项目重复声明或覆盖。
- 保留既有公开契约；枚举编码、配置原文、时间格式、分页或路径规则的变化不能作为无关重构顺带发生。
- 优先清楚的 C# 类型与现代语法；新增和修改代码按下文 [性能反模式约束](#性能反模式约束) 检查，优化证据遵循 [性能变更验证](../../docs/quality.md#性能变更验证)。
- 使用依赖注入管理生命周期；DbContext 不跨并发操作共享。可测试的时间依赖可使用 `TimeProvider`。

## 性能反模式约束

适用于新增和修改的手写后端代码。先消除多余 I/O、重复查询、无界工作和资源泄漏，再处理已识别热点的分配与 CPU 开销；不因文本搜索命中就判定缺陷。核对接收者类型、调用频率、数据规模、生命周期及现有优化，不能把上游微基准的收益倍数当作本项目结论。

### 异步、并发与资源生命周期

- 请求链路禁止以 `Task.Result`、`Wait()`、`GetAwaiter().GetResult()` 同步等待未完成的异步操作，禁止用 `Thread.Sleep` 等待重试或轮询；使用异步等待并贯穿传递 `CancellationToken`。底层只有同步接口时如实保留同步语义，不用 `Task.Run` 伪造异步，也不为已异步的 I/O 增加线程池调度。
- 除框架要求的事件处理外不使用 `async void`；请求内不得启动无人等待、无人观察异常的任务，也不得让后台任务捕获请求作用域的 DbContext、服务或 `HttpContext`。后台工作需要明确宿主生命周期、独立作用域、异常处理及停止取消。
- 不对大小无界的输入直接使用 `Task.WhenAll`、并行循环或无限增长的队列；并发量、排队容量、超时、取消和背压必须有边界。同一 DbContext 不并行执行操作；异步等待不持有跨请求的全局锁，锁内不执行网络或数据库 I/O。长轮询不得用忙循环或占用线程的等待实现。
- 默认返回 `Task`；仅在高频同步完成且有分配证据时引入 `ValueTask`，同一个实例只消费一次，需要共享结果时先转换为一个 `Task`。不得为微优化删除承担 `using` / `finally` / 异常处理语义的 `await`；不把 `ConfigureAwait(false)` 当作 ASP.NET Core 应用代码的统一性能要求。
- 流、HTTP 响应、`JsonDocument`、取消注册和定时器按所有权及时释放；不返回依赖已释放资源的数据或延迟查询。禁止单例或静态字段长期持有请求对象、跟踪实体和可变请求缓冲区；不通过显式 `GC.Collect()` 掩盖分配或生命周期问题。

### 分配、字符串与集合

- 非语言学标识比较明确使用符合契约的 `StringComparison.Ordinal` / `OrdinalIgnoreCase`，字符串键集合使用对应 `StringComparer`；不先 `ToLower` / `ToUpper` 再比较。不得借优化改变账号、项目、环境或配置键的大小写规则、文化语义及排序结果。
- 热点避免仅为切片、查找、解析而产生 `Substring`、`Split`、`ToArray` 等中间对象，以及循环字符串 `+=`、多层 `Replace` / 格式化造成的重复复制；按消费方式选择 `AsSpan`、`StringBuilder`、`TryParse` / `TryFormat`、UTF-8 字面量。固定字符集合的重复搜索可评估缓存 `SearchValues<T>`；不为一次性短字符串操作堆叠复杂实现。
- 禁止循环内 `stackalloc` 或按不可信长度分配栈内存；仅对大小有界的小缓冲区使用栈分配。大块临时缓冲区确有 GC 压力时再评估 `ArrayPool<T>`，必须在 `finally` 归还，按实际有效长度操作，含敏感数据时清理，归还后不得继续引用。跨 `await` 的缓冲区使用合适的 `Memory<T>` 及所有权，不让 `Span<T>` 跨越挂起点。
- 对字典同一个键的“检查后读取”使用 `TryGetValue`，集合已有 `Count` / `Length` 时不另行枚举计数。避免重复枚举会重新执行计算或 I/O 的 `IEnumerable`；需要快照时只物化一次且结果必须有界，不以无条件 `ToList()` 解决所有枚举问题。
- 不全面禁止 LINQ，尤其保留 EF 的查询表达式及低频代码的可读性；只针对证实有成本的内存热点减少闭包、装箱、委托和中间集合。无捕获的回调可标为 `static`，确定的数据可复用；集合预分配容量必须来自可信且有界的规模。
- `FrozenDictionary` / `FrozenSet` 仅用于构造后不变、读取频繁且构造成本可摊销的数据，不替换需要更新的状态。无继承或框架代理需求的实现类优先 `sealed`，不依赖实例的辅助成员可用 `static`；不得破坏公开扩展点或绕过依赖注入。`CollectionsMarshal`、`unsafe`、自定义对象池和手工内存布局只在明确热点、收益及安全性验证齐备时考虑。

### 序列化、正则、HTTP 与日志

- 沿用 `System.Text.Json` 与宿主已配置的序列化契约，不在每次调用时新建 `JsonSerializerOptions`；复用已配置且使用后不再修改的选项。稳定 DTO 的高频序列化可评估 `JsonSerializerContext`，不得改变命名、空值、枚举、时间和转换器行为；不因此启用 Native AOT 或裁剪。
- 不为传输或解析反复进行对象 → JSON 字符串 → UTF-8 数组转换，也不无界地将请求、响应或配置正文完整复制进内存；按实际需求选择有界缓冲或异步流式处理，保留所需的原文与验证语义。流式响应仍须负责取消、读取超时、资源释放和已开始响应后的错误边界。
- 重复使用的固定正则优先 `[GeneratedRegex]`，不得每次调用重新编译；动态模式不能套用源生成，也不能无限缓存。面对不可信输入明确长度边界和匹配超时，适用时使用 `NonBacktracking`，不为性能移除回溯保护；只需布尔结果时使用 `IsMatch`。
- 不为每次请求直接创建并销毁拥有独立连接池的 `HttpClient` / handler；宿主内优先使用工厂管理的命名或类型化客户端，沿用 ServiceDefaults 的服务发现与弹性配置。已有合理生命周期及连接回收策略的客户端不机械替换；大响应按需使用 `ResponseHeadersRead`，重试约束见 [ServiceDefaults](Trelix.ServiceDefaults/AGENTS.md)。
- 日志使用固定消息模板，避免插值、拼接或为未启用等级提前序列化昂贵参数；热点可使用 `LoggerMessage` 源生成，必要时先检查日志级别。不在正常循环或预期解析失败中反复抛捕异常，可用 `Try*` 分支；保留既有业务错误与异常映射契约。不得通过删除必要诊断或记录敏感正文换取表面收益。

### 缓存与验证边界

- 不引入无容量或无回收策略的缓存、等待者集合、并发字典或任务列表；缓存必须说明键、容量、失效时机与并发填充策略，不能将不可信输入直接变成无限增长的键空间。只读发布快照的缓存不得绕过当前发布指向、管理员会话、令牌有效期、撤销状态和项目环境授权的实时校验。
- 数据库查询与事务遵循 [Server 指引](Trelix.Server/AGENTS.md#sqlite-与配置数据)。优化不得削弱草稿隔离、原子发布、并发控制和撤销即时生效；新的基础设施或缓存一致性规则仍按根指引讨论。编译成功、测试通过或模式命中数不构成性能提升证据。

### 官方参考与采用边界

- [.NET 官方 analyzing-dotnet-performance](https://github.com/dotnet/skills/blob/main/plugins/dotnet-diag/skills/analyzing-dotnet-performance/SKILL.md) 及其 [模式参考](https://github.com/dotnet/skills/tree/main/plugins/dotnet-diag/skills/analyzing-dotnet-performance/references)：采用按热点、分配和正确性分级检查的方法；不照搬固定收益倍数、按出现次数升级严重性或全面替换 API 的建议。
- [microsoft/mcp copilot-instructions.md](https://github.com/microsoft/mcp/blob/main/.github/copilot-instructions.md)：借鉴 `System.Text.Json`、适用的静态成员、工厂管理 HTTP 客户端与变更自检；其强制 AOT、Azure SDK 重试默认值、专用脚本与测试平台不是 Trelix 的工程约定。
- [ASP.NET Core 性能实践](https://learn.microsoft.com/aspnet/core/fundamentals/best-practices?view=aspnetcore-10.0)、[EF Core 高效查询](https://learn.microsoft.com/ef/core/performance/efficient-querying) 与 [JsonSerializerOptions 复用](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/configure-options)：结合本项目 .NET 10、SQLite、Controllers 和现有 ServiceDefaults 使用。
- [.NET 高性能日志](https://learn.microsoft.com/dotnet/core/extensions/logging/high-performance-logging) 与 [正则表达式实践](https://learn.microsoft.com/dotnet/standard/base-types/best-practices-regex)：用于核对日志源生成、正则引擎选择及输入边界。

## 技能与模块入口

- C# 符号、引用、实现与调用关系：读 [csharp-lsp](../../.agents/skills/csharp-lsp/SKILL.md)，使用随 Git 提供的 Windows x64 Roslyn CLI 和本机 .NET 10 SDK；只知道名称时先用 `find-symbols` 定位，跨项目查询及命令覆盖范围以技能说明为准，不在本项目内构建工具。
- 保持行为不变的 C# 重构：读 [csharp-refactoring](../../.agents/skills/csharp-refactoring/SKILL.md)。
- 执行 .NET 测试：读 [run-tests](../../.agents/skills/run-tests/SKILL.md)，按需要读取配套的 platform-detection 与 filter-syntax。Server 测试已采用 xUnit v3 + MTP 与真实 SQLite，命令和范围见 [自动化测试](../../docs/local-development.md#server-自动化测试)；不复制上游测试框架偏好。
- Server 的 HTTP、存储和认证实现：读 [Server 指引](Trelix.Server/AGENTS.md)。
- Core 的通用技术实现：先核对 [职责边界](../../docs/architecture.md#通用技术与业务基础设施边界)，按具体能力选择技能；不将业务模型、持久化或权限规则移入 Core。
- 遥测、健康检查、服务发现与 HTTP 弹性：读 [ServiceDefaults 指引](Trelix.ServiceDefaults/AGENTS.md)。

统一启动与解决方案构建见 [本地开发](../../docs/local-development.md)；模块专属验证留在对应模块指引。完整技能选择见 [技能索引](../../.agents/README.md)。
