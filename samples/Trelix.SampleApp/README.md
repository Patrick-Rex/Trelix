# Trelix .NET 10 示例应用

本示例在 `Build()` 和业务 Options 绑定前调用 `AddTrelixAsync()`。`GET /configuration` 仅展示 `Sample:Message`、`Sample:Enabled`，对照原生 `IConfiguration`、`IOptionsMonitor<SampleOptions>` 和本次请求的 `IOptionsSnapshot<SampleOptions>`。

在管理界面先创建并发布包含 `Sample` 对象的配置文件，复制该文件的 `Trelix` 连接节到本地启动配置。令牌占位符必须通过本项目 User Secrets 或 `Trelix__AccessToken` 外部环境变量替换。其余连接字段也可通过外部配置注入，不将真实地址、令牌或业务配置提交到源码。

```json
{
  "Sample": {
    "Message": "published-example",
    "Enabled": true
  }
}
```

从解决方案根目录独立启动：

```powershell
dotnet run --project .\samples\Trelix.SampleApp\Trelix.SampleApp.csproj
```

监听地址从启动输出或宿主外部配置取得。保存草稿不改变返回值；发布后再次请求可观察三种读取方式的更新。停止 Server 时示例保留最近配置；Server 恢复后自动重试并获取更新。未发布、错误令牌或首次不可达时示例拒绝启动。

Aspire 默认不启动示例。在 AppHost 的外部配置中设置 `Trelix:Sample:Enabled=true`，并配置同节的 `ProjectKey`、`EnvironmentKey`、`FileName`、`AccessToken`；Server 地址由 AppHost 端点注入。准备好已发布文件与令牌再启用；业务环境与 ASP.NET Core 运行环境独立。User Secrets、环境变量及验证步骤见 [通过 AppHost 启用示例](../../docs/local-development.md#通过-apphost-启用示例)。

完整连接与重试说明见 [SDK](../../src/sdk/Trelix.Extensions.Configuration/README.md)，统一操作说明见 [本地开发](../../docs/local-development.md)。
