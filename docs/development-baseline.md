# Trelix 技术基线

本文记录已确认的技术选择、版本来源和工程约束。采用某项技术不代表已完成接入，具体能力以源码和实际验证为准。产品范围见 [产品方案](product-plan.md)，程序边界见 [架构设计](architecture.md)，文档分工见 [文档索引](README.md)。

## 已确认的选择

| 领域 | 基线 | 版本来源或约束 |
| --- | --- | --- |
| 文本文件格式 | UTF-8 无 BOM、LF、文件末尾换行；`.bat` / `.cmd` 使用 CRLF | [.editorconfig](../.editorconfig)、[.gitattributes](../.gitattributes)；修改后检查实际格式，不代表存量文件全部标准化 |
| 项目架构 | 轻量模块化单体；Server 保留业务并按模块分目录，Core 承担通用技术基础设施，SDK 独立 | 前端、Server、Core、ServiceDefaults、AppHost、SDK、测试、示例及部署资产按 [目标项目结构](architecture.md#目标项目结构) 组织；Server 不再拆分分层类库 |
| 通用技术基础设施 | Core 承担通用异常处理、中间件、序列化辅助和技术组件注册扩展 | 已接入不输出异常正文的通用异常处理器；按 [职责边界](architecture.md#通用技术与业务基础设施边界) 增加实际需要的能力，业务持久化、认证授权与发布通知留在 Server，SDK 不依赖 Core |
| 后端运行时 | .NET 10，使用稳定的现代 .NET/C# 能力 | [Directory.Build.props](../Directory.Build.props) 的 `net10.0`、Nullable、ImplicitUsings；[global.json](../global.json) 的 SDK 策略 |
| 配置 SDK 兼容基线 | 最低支持 .NET 10，目标框架 `net10.0` | SDK、示例及 .NET 测试项目继承根目录统一配置；接入验收在 .NET 10 上执行 |
| .NET 依赖管理 | 集中管理 SDK、构建属性、NuGet 包版本和包源 | 根目录配置，见下文；不启用预览 SDK，不自动跨主版本升级 |
| HTTP API | ASP.NET Core Controllers + 内置 OpenAPI；独立 DTO 和 Problem Details 错误 | 管理与应用 API 分离，见 [HTTP 边界](architecture.md#http-与监听边界)；OpenAPI 包版本源为 [Directory.Packages.props](../Directory.Packages.props) |
| 本地编排 | Aspire 13.5 | AppHost SDK 与 JavaScript 集成包均为 13.5.4，分别由 `global.json` 与 `Directory.Packages.props` 管理 |
| 公共运行能力 | ServiceDefaults 提供 OpenTelemetry、健康检查、服务发现与 HTTP 弹性 | 公共注册由 [ServiceDefaults](../src/backend/Trelix.ServiceDefaults/AGENTS.md) 维护；避免重复注册 |
| 前端框架 | Vue 3 + Vite + Composition API | [package.json](../src/frontend/package.json) 声明 Vue `^3.5.42`、Vite `^8.3.0`；安装版本由锁文件确定 |
| 前端语言 | TypeScript 与 `<script setup lang="ts">` | 已接入 TypeScript 5.9、vue-tsc、tsconfig 与 ESLint TypeScript 支持；`npm run build` 先检查类型再打包，版本由 npm 清单和锁文件管理 |
| Node 与包管理 | Node.js 24 + npm 10+ | [package.json](../src/frontend/package.json)、[.nvmrc](../src/frontend/.nvmrc)；沿用 `package-lock.json` |
| 页面组件 | Element Plus | 已接入 Element Plus 与 `@element-plus/icons-vue`；显式导入组件、图标和所需样式，版本以 npm 清单和锁文件为准 |
| 配置编辑器 | Monaco Editor | 尚未接入；JSON/YAML/Tree 的交互和转换规则见产品方案，实例与 worker 集成遵循前端指引 |
| 数据存储 | SQLite + EF Core 10 | SQLite provider 与 Design 包版本由集中依赖管理；已建立逻辑模型、初始迁移和启动迁移入口；UTC 时间转换为 INTEGER ticks，文件与令牌使用应用维护的并发标记 |
| 管理员认证 | 单个内置管理员 + ASP.NET Core Cookie，无 RBAC | 已接入 PasswordHasher、外部首次初始化、SQLite 会话校验及防伪造；会话规则见产品方案，界面在 M4 接入 |
| 应用访问 | 限定项目与环境的应用只读令牌，使用 Bearer 请求头 | 已接入 256 位随机令牌、SHA-256 摘要、有效期、撤销、原子轮换与多范围授权基础；生产读取与长轮询入口分别随 M3、M5 接入 |
| .NET 测试 | xUnit v3 + Microsoft.Testing.Platform v2 | `xunit.v3.mtp-v2` 的包版本与 ASP.NET Core 测试宿主版本由集中依赖管理；`global.json` 选择原生 MTP 命令模式；集成测试使用真实 SQLite 文件 |
| .NET 配置集成 | 自定义 IConfigurationSource / ConfigurationProvider + 后台监听，一次接入一个文件 | 启动必须拉取成功；运行中失败保留最近成功配置并退避重试；无磁盘缓存，见 [SDK 设计](architecture.md#sdk-与宿主边界) |
| 生产交付 | Linux Docker 单容器、单个 Server 实例，前后端统一交付 | Server 提供前端静态资源与 API，SQLite 挂载持久化；详见 [生产部署](deployment.md) |

SQLite 的选型边界参考 [官方说明](https://www.sqlite.org/whentouse.html)。持久化与备份必须覆盖配置、版本、管理员及应用令牌授权信息。

## .NET 统一配置

四个配置文件位于包含 `Trelix.slnx` 的根目录，使 Server、Core、ServiceDefaults、AppHost、配置 SDK、示例及 .NET 测试项目使用同一套配置；不在模块目录另建同名文件覆盖根配置。

| 文件 | 统一维护的内容 |
| --- | --- |
| [Directory.Build.props](../Directory.Build.props) | 所有 `.csproj` 的 `TargetFramework=net10.0`、`Nullable=enable`、`ImplicitUsings=enable`；不向前端 `.esproj` 注入这些 .NET 属性 |
| [Directory.Packages.props](../Directory.Packages.props) | 显式 NuGet 依赖的版本；启用 Central Package Management，禁用项目级 `VersionOverride` |
| [global.json](../global.json) | .NET SDK 最低 10.0.300，`rollForward=latestMinor`、`allowPrerelease=false`；Aspire AppHost SDK 13.5.4、Visual Studio JavaScript SDK 1.0.6578810 与 MTP 测试命令模式 |
| [nuget.config](../nuget.config) | 唯一包源 `https://api.nuget.org/v3/index.json`；清除继承的包源及禁用源列表，不依赖本机私有源或离线目录 |

新增或调整 NuGet 包时，项目只声明无版本的 `PackageReference`，版本在 `Directory.Packages.props` 维护；Aspire SDK 的隐式依赖由其 SDK 管理。项目保留 `OutputType`、`UserSecretsId`、项目引用等专属设置，不重复声明公共框架、Nullable 或 ImplicitUsings。公共设置的例外与版本升级先讨论，再统一修改根配置。

前端 npm 依赖继续由 `src/frontend/package.json` 与 `package-lock.json` 管理；其 `.esproj` 的 MSBuild SDK 版本由 `global.json` 管理。SDK 选择与目标框架分别维护；`version` 必须填写完整 SDK 版本，`latestMinor` 允许同一大版本内后续小版本、feature band 和补丁，选择已安装且不低于 10.0.300 的最高稳定版 10.x，不跨到 .NET 11，也不自动安装 SDK。该策略不改变 `msbuild-sdks` 中单独指定的项目 SDK 版本。

EF 命令行工具固定在 [.NET 工具清单](../.config/dotnet-tools.json)，与 EF Core 包使用同一版本。其迁移生成入口使用设计时 DbContext；运行数据库由 Server 在启动时迁移。测试项目继承统一构建与依赖配置，直接引用 Server，使用 WebApplicationFactory 验证 HTTP 行为，不为测试接入外部数据库服务。

配置机制参考 [NuGet CPM](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management)、[MSBuild 目录配置](https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-by-directory)、[global.json](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json) 与 [NuGet 配置](https://learn.microsoft.com/en-us/nuget/reference/nuget-config-file)。

## 采用上游技能时的项目约束

- `.agents/skills` 用于开发 Trelix。根目录与分模块 `AGENTS.md` 是项目开发 agent 的入口，不是技能作者或插件维护者指引。
- `csharp-lsp` 仅分发 Git 跟踪的 Windows x64 预编译 EXE 与技能文档，工具源码在独立工程维护；产物使用本机 .NET 10 SDK，不嵌入 Trelix 解决方案或依赖配置。支持按名称查找声明并衔接位置查询；版本、哈希和使用边界见 [技能说明](../.agents/skills/csharp-lsp/SKILL.md#工具路径)。
- 上游 Vue 技能覆盖 TypeScript、Router、Pinia 和测试；按本项目已经确认与安装的能力使用，保持 Composition API。技能安装不表示采用对应依赖。
- 上游 .NET Web API 技能允许 Controllers 与 Minimal APIs，本项目保持 Controllers。EF 查询优化技能只在相关查询任务中启用。
- 编译仅读取 error 与退出码，覆盖上游“检查零 warning”的通用要求。测试结果应照常检查失败与统计；执行前核对实际测试项目、依赖和脚本，未接入或零测试不能报告通过。
- 上游示例中的接口、数据库类型和注册方式需匹配本项目；避免重复注册可观测性，避免为轻量业务引入没有用途的抽象层。
- SQLite 不支持数据库生成的并发 token，DateTimeOffset 等类型的比较/排序也有 provider 限制；建模和迁移时对照 [EF Core SQLite 限制](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations) 验证。

开发启动与检查命令见 [本地开发](local-development.md)，验收场景和验证证据要求见 [质量与验收](quality.md)，基础工程联调见 [M1 验收记录](verification/m1.md)，存储与认证验证见 [M2 验收记录](verification/m2.md)。
