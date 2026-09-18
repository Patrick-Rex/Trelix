# Trelix

Trelix 是面向配置管理员与 .NET 应用开发者的轻量级配置中心。配置按 Project → Env → ConfigFile 组织，正文统一存储 JSON；保存草稿与发布分离，支持历史版本、回滚生成新版本和并发保护。

首版包含 JSON/YAML/Tree 管理工作区、内置管理员 Cookie 登录、应用只读令牌、已发布配置读取与长轮询、.NET 配置 SDK。生产采用 Linux Docker 单容器、单实例，前后端统一交付，SQLite 挂载持久化；不依赖外部 IAM 或 SSO 平台。

当前已建立 TypeScript + Element Plus 最小应用壳（侧边栏、顶部栏与工作区空状态），并移除前后端天气及 Vue 欢迎示例。上述首版业务能力按里程碑继续接入；当前工程能力见 [前端 README](src/frontend/README.md#当前应用壳)。

## 文档入口

完整分工与按任务阅读顺序见 [文档索引](docs/README.md)。

| 关注内容 | 文档 |
| --- | --- |
| 产品目标、首版范围与业务规则 | [产品方案](docs/product-plan.md) |
| 目标项目结构、程序边界、模块职责与数据流 | [架构设计](docs/architecture.md) |
| 技术选择、版本来源与工程约束 | [技术基线](docs/development-baseline.md) |
| 阶段交付与完成条件 | [交付里程碑](docs/milestones.md)、[质量与验收](docs/quality.md) |
| 环境准备、启动与联调 | [本地开发](docs/local-development.md) |
| 生产交付与持久化 | [生产部署](docs/deployment.md) |
| AI 与人的开发协作 | [开发指引](AGENTS.md)、[项目技能索引](.agents/README.md) |

## 目标项目组成

项目采用轻量模块化单体，服务端按业务目录组织并单进程部署。完整目录树与项目依赖见 [目标项目结构](docs/architecture.md#目标项目结构)，项目和目录随对应功能建立。

| 目录 | 职责 | 开发入口 |
| --- | --- | --- |
| `src/frontend` | Vue Web 管理界面 | [README](src/frontend/README.md)、[前端指引](src/frontend/AGENTS.md) |
| `src/backend` | 后端公共 C# 开发与技能入口 | [后端公共指引](src/backend/AGENTS.md) |
| `src/backend/Trelix.Server` | ASP.NET Core API、配置业务、业务持久化与静态资源入口 | [Server 指引](src/backend/Trelix.Server/AGENTS.md) |
| `src/backend/Trelix.Core` | 与配置中心业务无关的通用技术基础设施 | [后端公共指引](src/backend/AGENTS.md)、[职责边界](docs/architecture.md#通用技术与业务基础设施边界) |
| `src/backend/Trelix.ServiceDefaults` | 遥测、健康检查、服务发现与 HTTP 弹性 | [ServiceDefaults 指引](src/backend/Trelix.ServiceDefaults/AGENTS.md) |
| `src/Trelix.AppHost` | Aspire 前后端本地编排 | [AppHost 指引](src/Trelix.AppHost/AGENTS.md) |
| `src/sdk/Trelix.Extensions.Configuration` | 最低支持 .NET 10 的业务应用配置 SDK | [SDK 设计](docs/architecture.md#sdk-与宿主边界) |
| `tests`、`src/frontend/tests` | 服务端、SDK 与前端验证 | [质量与验收](docs/quality.md) |
| `samples/Trelix.SampleApp` | SDK 接入与配置重载示例 | [配置集成](docs/product-plan.md#net-配置集成) |
| `deploy/docker` | Linux Docker 构建与部署资产 | [生产部署](docs/deployment.md) |

解决方案与通用配置位于包含 `Trelix.slnx` 的根目录，工作范围限于此目录。管理界面与 Server 统一交付，SDK 作为独立类库供业务应用引用，不依赖 Server 的业务程序集或数据库。

开发命令统一维护在 [本地开发](docs/local-development.md)，前端独立操作见 [前端 README](src/frontend/README.md)。文档与规则的更新遵循 [维护规则](docs/README.md#文档维护规则)。
