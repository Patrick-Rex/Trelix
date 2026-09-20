# Trelix

Trelix 是面向配置管理员与 .NET 应用开发者的轻量级配置中心。配置按 Project → Env → ConfigFile 组织，正文统一存储 JSON；保存草稿与发布分离，支持历史版本、回滚生成新版本和并发保护。

首版包含 JSON/YAML/Tree 管理工作区、内置管理员 Cookie 登录、应用只读令牌、已发布配置读取与长轮询、.NET 配置 SDK。生产采用 Linux Docker 单容器、单实例，前后端统一交付，SQLite 挂载持久化；不依赖外部 IAM 或 SSO 平台。

M1 工程基础已完成，构建、TypeScript 类型检查、Aspire HTTPS 联调及最小应用壳验收见 [M1 验收记录](docs/verification/m1.md)。当前已建立 TypeScript + Element Plus 应用壳（侧边栏、顶部栏与工作区空状态），并移除前后端天气及 Vue 欢迎示例。上述首版业务能力按里程碑继续接入；当前界面能力见 [前端 README](src/frontend/README.md#当前应用壳)。

## 首次拉取后的本机准备

先按[环境准备](docs/local-development.md#环境准备)安装所需的 .NET SDK、Node.js 和 npm。开发依赖由 AppHost 启动流程还原，前端使用锁文件执行 `npm ci`。

开发证书及其信任状态属于本机环境，不随 Git 同步。从项目根目录检查并信任 ASP.NET Core HTTPS 开发证书：

```powershell
dotnet dev-certs https --trust
dotnet dev-certs https --check --trust
```

若 Aspire 仍提示缺少受信任的开发证书，使用与 AppHost SDK 同版本的 Aspire CLI 完成信任。尚未安装 CLI 时，可读取 `global.json` 中的版本安装：

```powershell
$aspireVersion = (Get-Content .\global.json -Raw | ConvertFrom-Json).'msbuild-sdks'.'Aspire.AppHost.Sdk'
dotnet tool install --global Aspire.Cli --version $aspireVersion
aspire certs trust
```

Windows 出现证书信任确认窗口时完成系统确认，再启动 AppHost。CLI 仅用于本机准备，统一启动仍按[本地开发说明](docs/local-development.md#统一启动与联调)执行。命令说明见 [Aspire 证书信任](https://aspire.dev/reference/cli/commands/aspire-certs-trust/)。

如果设置了 `HTTP_PROXY` 或 `HTTPS_PROXY`，检查 `NO_PROXY` 是否覆盖回环主机名及 IPv4、IPv6 回环地址，追加时保留已有绕过项。缺少绕过设置可能导致 Aspire 本机资源连接失败；配置从当前终端和用户环境变量获取，不写入仓库。修改用户环境变量后，重新打开终端或重启 Visual Studio，使新启动的 AppHost 继承设置。

准备完成后，在 Visual Studio 中将 `Trelix.AppHost` 设为启动项目，或使用[统一启动命令](docs/local-development.md#统一启动与联调)。从 Aspire 面板打开 `trelix-client` 的 HTTPS 地址，检查页面与侧栏，并通过前端地址下的 `/health`、`/alive` 验证代理；两者应返回 `Healthy`。

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

启动与构建命令统一维护在 [本地开发](docs/local-development.md)，前端独立操作见 [前端 README](src/frontend/README.md)。文档与规则的更新遵循 [维护规则](docs/README.md#文档维护规则)。
