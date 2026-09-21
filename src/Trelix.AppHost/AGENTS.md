# AppHost 开发指引

遵循 [根指引](../../AGENTS.md)、[技术基线](../../docs/development-baseline.md) 与 [产品方案](../../docs/product-plan.md)。开发与生产的运行边界见 [架构设计](../../docs/architecture.md#运行边界)。

- 当前为 Aspire AppHost SDK 13.5.4、`net10.0`，JavaScript 集成包为 13.5.4；SDK 版本由根目录 `global.json` 管理，公共构建属性与包版本继承根目录配置，遵循 [统一配置约定](../../docs/development-baseline.md#net-统一配置)。`AppHost.cs` 注册 `trelix-server` 与 `trelix-client`。
- 负责本地资源编排、依赖引用、配置传递与启动顺序；不放业务逻辑、配置持久化和业务 API。首版生产由 Server 在 Linux Docker 单容器运行，AppHost 不作为生产入口，见 [生产部署](../../docs/deployment.md)。
- 显式设置 `AspireUseCliBundle=false`，保持通过 NuGet 还原编排依赖，支持 IDE 与 `dotnet run` 启动；按 [ASPIRE010 官方说明](https://aspire.dev/zh-cn/diagnostics/aspire010/) 仅在 AppHost 定向抑制该 CLI bundle 功能提示，不扩大到其他警告。
- 首版单实例 SQLite 不需要独立数据库容器。新增容器、缓存、消息系统或多实例方案需要先讨论。
- 前端通过 `AddViteApp("trelix-client", "../frontend")` 编排，目录相对于 AppHost；使用 `WithNpm(installCommand: "ci")` 安装锁定依赖、`WithReference(server)` 注入服务地址、`WaitFor(server)` 等待后端启动。
- Vite 已自行配置 HTTPS 开发证书，将 `AddViteApp` 默认的 `http` 端点改为 `https` 名称和协议，避免面板链接与实际监听协议不符；不额外添加重复端点。Aspire 通过 `--port` 参数指定 Vite 监听端口，优先于前端独立启动时的端口配置。
- 调整服务名、端点或前端编排时，同步检查 Vite 中的 `services__trelix-server__https__0` 与开发启动配置。
- Server 显式使用 `https` launch profile，确保前端收到 HTTPS 后端地址，不因 AppHost 的启动方式不同而回退到固定端口。
- 使用配置或本地 secrets 承载敏感值，不把凭证写入编排代码。
- 首版采用内置管理员账号、Cookie 登录和应用只读令牌，不编排外部 IAM/SSO 服务；管理员初始化凭证与业务应用读取令牌分别注入，不互相复用。
- 业务应用接入 Trelix 时由 AppHost 显式注入连接信息；`launchSettings.json` 中声明 Aspire Profile 不会自动完成连接配置。业务配置环境与 ASP.NET Core 运行环境分别传递。
- SDK 示例按 [目标项目结构](../../docs/architecture.md#目标项目结构) 放在 `samples/Trelix.SampleApp`；需要联合验证时编排该业务进程并注入地址、项目、环境、文件和应用令牌，不把 SDK 逻辑放入 AppHost。
- 改动后在根目录执行 `dotnet build .\src\Trelix.AppHost\Trelix.AppHost.csproj --nologo -v:q -clp:ErrorsOnly`，只读取 error 与退出码；需要验证编排时运行 AppHost 并检查相关服务实际启动。
- 统一启动、环境准备与联调步骤统一维护在 [本地开发](../../docs/local-development.md#统一启动与联调)。
