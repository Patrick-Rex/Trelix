# 本地开发

本文统一维护跨前后端的环境准备、启动与联调说明。技术版本见 [技术基线](development-baseline.md)，开发约束见 [根指引](../AGENTS.md)。基础工程联调见 [M1 验收记录](verification/m1.md)，存储与认证验证见 [M2 验收记录](verification/m2.md)。

## 环境准备

- .NET SDK 按根目录 [global.json](../global.json) 选择：最低 10.0.300，允许更高的稳定版 10.x，不跨大版本。
- 前端使用 Node.js 24、npm 10+ 与现有 `package-lock.json`；安装及独立前端开发见 [前端 README](../src/frontend/README.md)。
- ASP.NET Core HTTPS 开发证书须受本机信任，Vite 复用现有开发证书。
- 使用本机 HTTP 代理时，确保回环主机名及 IPv4、IPv6 回环地址绕过代理，否则 Aspire 面板可能无法连接本地资源服务。
- SDK、公共构建属性、NuGet 版本与包源按 [统一配置约定](development-baseline.md#net-统一配置) 在根目录维护。
- C# 语义分析技能随 Git 提供 Windows x64 预编译 EXE，使用本机 .NET 10 SDK，无需在 Trelix 内构建工具；使用前还原被分析项目的依赖。准备步骤、按名称定位符号的入口及平台限制见 [技能索引](../.agents/README.md#csharp-lsp-的使用)。该工具不是项目构建或启动的依赖。

### 首次拉取后的本机准备

证书信任、Aspire CLI 安装和代理环境变量的准备步骤见[项目 README](../README.md#首次拉取后的本机准备)。这些设置属于开发机环境，不随 Git 同步；完成准备后按下文统一启动。

## Server 存储与首次初始化

Server 的 User Secrets 标识已在项目中配置。首次启动前，用 IDE 的“管理用户机密”或 `dotnet user-secrets` 在 Server 项目对应的外部机密存储中配置以下键，不把实际值写入仓库或调用样例：

| 配置键 | 用途 |
| --- | --- |
| `Trelix:Administrator:Username` | 首个管理员账号名，1–128 个字符，无首尾空白 |
| `Trelix:Administrator:Password` | 首个管理员密码，12–1024 个字符 |
| `Trelix:Storage:DataDirectory` | SQLite 与 Data Protection 密钥的存放目录；开发环境可省略，默认使用 Server 内容根目录下被 Git 忽略的 `.trelix-data` |

环境变量使用双下划线替代冒号。非 Development 环境必须显式配置数据目录。程序在开始处理请求前应用 EF 迁移，再检查是否需要初始化管理员；已有管理员时忽略初始化凭证，可从外部配置移除。缺少有效首次凭证时启动失败，不创建默认账号。

数据目录包含 `trelix.db`、SQLite 运行时伴随文件及 `keys` 子目录。保持同一目录可在重启后恢复数据与未过期会话；不要清空密钥目录。目录包含敏感认证数据，不应提交、公开共享或作为静态资源发布。

管理员 API 样例见 [Trelix.Server.http](../src/backend/Trelix.Server/Trelix.Server.http)。HTTP 客户端先获取 `/api/admin/auth/antiforgery` 并保存其 Cookie，登录请求携带返回的 `X-Trelix-CSRF` 请求令牌。登录成功后重新获取绑定管理员身份的防伪造令牌，供退出及后续管理写请求使用。`/api/admin/auth/session` 查询当前会话；会话固定 8 小时、不自动续期，退出后旧 Cookie 失效。交互式文档入口见下文 [API 文档](#api-文档)。

令牌管理使用 `/api/admin/application-tokens`，支持创建、分页查询、单个查询，以及 `/{id}/revoke`、`/{id}/rotate`。创建接受 `name`、`expiresAt` 与 `scopes`，每个 scope 包含 `projectId` 和 `environmentId`；名称最多 200 字符，范围为 1–100 个且不得重复环境，服务端验证归属。分页默认每页 50 条，最多 100 条。轮换接受新的 `expiresAt`，成功返回 201 和新令牌资源位置；原文只在创建或轮换响应的 `secret` 中返回一次。管理列表包含已过期和撤销记录，客户端按元数据展示状态。

M2 未提供项目/环境管理 API，因此空数据库不能创建有效范围的应用令牌；端到端业务使用从 M3 接入资源管理开始。M2 集成测试通过测试夹具建立资源，再验证真实令牌管理 API 与认证方案；不要向正常运行数据库手工植入生产资源来替代后续管理功能。

EF 工具版本在 [.NET 工具清单](../.config/dotnet-tools.json) 固定，迁移位于 [Persistence/Migrations](../src/backend/Trelix.Server/Persistence/Migrations)。修改模型后，从项目根目录执行：

```powershell
dotnet tool restore
dotnet build .\src\backend\Trelix.Server\Trelix.Server.csproj --nologo -v:q -clp:ErrorsOnly
dotnet ef migrations add <MigrationName> --project .\src\backend\Trelix.Server\Trelix.Server.csproj --output-dir Persistence/Migrations --no-build
```

`<MigrationName>` 替换为本次迁移名。设计时工厂只用于生成模型迁移，不初始化管理员、不连接运行数据库；实际迁移在 Server 启动时执行。生成的文件仍须按技术基线检查 UTF-8 无 BOM、LF 和末尾换行。

## 统一启动与联调

先完成上述 Server 外部初始化配置；以下命令从包含 `Trelix.slnx` 的项目根目录执行：

```powershell
dotnet run --project .\src\Trelix.AppHost\Trelix.AppHost.csproj --launch-profile https
```

也可以在 Visual Studio 中将 `Trelix.AppHost` 设为启动项目。AppHost 统一启动 `trelix-server` 和 `trelix-client`，通过 npm 安装资源执行 `npm ci`，等待 Server 启动后运行 Vite。

在 Aspire 面板打开 `trelix-client` 的 HTTPS 地址访问管理界面。Vite 端口由 Aspire 分配，代理请求访问 AppHost 注入的 Server 地址；不要同时手动启动另一套前端。检查应用壳页面、侧栏切换，并直接访问前端地址下的 `/health`、`/alive`，确认 Development 环境的健康检查代理可用。页面当前不调用业务 API，原天气示例已移除。

独立前端启动使用 `DEV_SERVER_PORT` 或 `vite.config.js` 中的默认端口，后端须另行启动；具体操作见 [前端 README](../src/frontend/README.md#独立启动)。服务名、HTTPS 端点和启动顺序的实现约束见 [AppHost 指引](../src/Trelix.AppHost/AGENTS.md)。

以上用于本地开发。首版生产采用 Linux Docker 单容器统一交付，部署契约见 [生产部署](deployment.md)；当前生产打包与部署尚未实现。

### API 文档

仅当 Server 的 ASP.NET Core 环境为 `Development` 时，提供 Scalar UI 与 OpenAPI JSON。启动后从 Aspire 面板获取 `trelix-server` 的 HTTPS 地址，在该地址下访问 `/scalar`；默认展示管理 API，可切换应用 API。也可直接访问 `/scalar/admin` 或 `/scalar/application`。文档入口使用 Server 地址，前端 Vite 不代理 Scalar。

两组 JSON 文档分别位于 `/openapi/admin.json` 与 `/openapi/application.json`；当前应用分组尚无公开读取入口，随分发 API 接入。Server 生成 XML 文档供 OpenAPI 展示契约说明。Scalar 使用包内脚本，关闭默认外部字体及 Agent；文档匿名可读，执行管理 API 仍须按上述 Cookie 与防伪造流程认证。`Production`、`Staging` 等非开发环境不映射文档页面、脚本和 JSON 端点。

## 构建与验证

从项目根目录构建整个解决方案：

```powershell
dotnet build .\Trelix.slnx --nologo -v:q -clp:ErrorsOnly
```

按根指引只读取 error 与退出码；子构建有额外输出时存临时日志，仅提取 error。按修改范围缩小到相关项目，具体命令见 [Server](../src/backend/Trelix.Server/AGENTS.md#验证)、[ServiceDefaults](../src/backend/Trelix.ServiceDefaults/AGENTS.md) 和 [AppHost](../src/Trelix.AppHost/AGENTS.md) 指引。

Server 保留对前端 `.esproj` 的构建引用，后端构建可能涉及 Node/npm 工具链。后端编译成功不表示前端生产构建或类型检查通过；前端命令见 [前端 README](../src/frontend/README.md#构建与检查)，验收要求见 [前端指引](../src/frontend/AGENTS.md#命令与验证)。

执行测试与类型检查前，核对实际测试项目及前端 `package.json` 中的依赖和脚本；不能将未接入检查或零测试报告为通过。行为与文档验收见 [质量与验收](quality.md)。纯文档变更只检查内容、链接与文件格式，无需构建业务项目。

### Server 自动化测试

[Server 测试项目](../tests/Trelix.Server.Tests/Trelix.Server.Tests.csproj) 使用 xUnit v3 与 MTP，根 `global.json` 已选择原生 MTP 模式。从项目根目录执行：

```powershell
dotnet test --project .\tests\Trelix.Server.Tests\Trelix.Server.Tests.csproj --verbosity quiet
```

该命令默认构建项目；仅在当前代码已构建时追加 `--no-build`。原生 MTP 使用 `--project`，不使用 VSTest 的位置项目参数或桥接分隔符。构建输出按根指引仅检查 error 与退出码，测试检查实际通过、失败和跳过数量。

测试通过 WebApplicationFactory 在进程内启动 Server，使用独立 SQLite 文件、Data Protection 目录及运行时随机凭证，不需要真实管理员机密或开放监听端口。临时文件位于 Git 忽略的 `artifacts/m2-tests`，正常结束后清理；测试用授权探针只注册到测试宿主，不包含在 Server 发布程序集。重启验证关闭并重新创建宿主、重开同一数据库和密钥目录，不替代 M6 的真实进程与容器恢复验证。

[OpenAPI 测试](../tests/Trelix.Server.Tests/OpenApiTests.cs) 验证开发环境的 Scalar 页面、本地脚本、两组 JSON 文档与 XML 契约说明，并检查 Production、Staging 不注册文档端点。
