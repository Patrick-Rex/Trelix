# 本地开发

本文统一维护跨前后端的环境准备、启动与联调说明。技术版本见 [技术基线](development-baseline.md)，开发约束见 [根指引](../AGENTS.md)。本文命令用于现有工程，代码核对不表示已经执行成功。

## 环境准备

- .NET SDK 按根目录 [global.json](../global.json) 选择：最低 10.0.300，允许更高的稳定版 10.x，不跨大版本。
- 前端使用 Node.js 24、npm 10+ 与现有 `package-lock.json`；安装及独立前端开发见 [前端 README](../src/frontend/README.md)。
- ASP.NET Core HTTPS 开发证书须受本机信任，Vite 复用现有开发证书。
- 使用本机 HTTP 代理时，确保 `localhost`、`127.0.0.1` 和 `::1` 绕过代理，否则 Aspire 面板可能无法连接本地资源服务。
- SDK、公共构建属性、NuGet 版本与包源按 [统一配置约定](development-baseline.md#net-统一配置) 在根目录维护。
- 使用 C# 语义分析技能时，需另行提供本机 Roslyn CLI；可执行文件不随仓库分发，准备步骤见 [技能索引](../.agents/README.md#csharp-lsp-的使用)。该工具不是项目构建或启动的依赖。

## 统一启动与联调

以下命令从包含 `Trelix.slnx` 的项目根目录执行：

```powershell
dotnet run --project .\src\Trelix.AppHost\Trelix.AppHost.csproj --launch-profile https
```

也可以在 Visual Studio 中将 `Trelix.AppHost` 设为启动项目。AppHost 统一启动 `trelix-server` 和 `trelix-client`，通过 npm 安装资源执行 `npm ci`，等待 Server 启动后运行 Vite。

在 Aspire 面板打开 `trelix-client` 的 HTTPS 地址访问管理界面。Vite 端口由 Aspire 分配，代理请求访问 AppHost 注入的 Server 地址；不要同时手动启动另一套前端。检查应用壳页面、侧栏切换，并直接访问前端地址下的 `/health`、`/alive`，确认 Development 环境的健康检查代理可用。页面当前不调用业务 API，原天气示例已移除。

独立前端启动使用 `DEV_SERVER_PORT` 或默认 58302，后端须另行启动；具体操作见 [前端 README](../src/frontend/README.md#独立启动)。服务名、HTTPS 端点和启动顺序的实现约束见 [AppHost 指引](../src/Trelix.AppHost/AGENTS.md)。

以上用于本地开发。首版生产采用 Linux Docker 单容器统一交付，部署契约见 [生产部署](deployment.md)；当前生产打包与部署尚未实现。

## 构建与验证

从项目根目录构建整个解决方案：

```powershell
dotnet build .\Trelix.slnx --nologo -v:q -clp:ErrorsOnly
```

按根指引只读取 error 与退出码；子构建有额外输出时存临时日志，仅提取 error。按修改范围缩小到相关项目，具体命令见 [Server](../src/backend/Trelix.Server/AGENTS.md#验证)、[ServiceDefaults](../src/backend/Trelix.ServiceDefaults/AGENTS.md) 和 [AppHost](../src/Trelix.AppHost/AGENTS.md) 指引。

Server 保留对前端 `.esproj` 的构建引用，后端构建可能涉及 Node/npm 工具链。后端编译成功不表示前端生产构建或类型检查通过；前端命令见 [前端 README](../src/frontend/README.md#构建与检查)，验收要求见 [前端指引](../src/frontend/AGENTS.md#命令与验证)。

执行测试与类型检查前，核对实际测试项目及前端 `package.json` 中的依赖和脚本；不能将未接入检查或零测试报告为通过。行为与文档验收见 [质量与验收](quality.md)。纯文档变更只检查内容、链接与文件格式，无需构建业务项目。
