# Trelix.Extensions.Configuration

面向 .NET 10 现代宿主的单文件配置 SDK，通过 HTTP 读取并长轮询已发布配置。

```csharp
using Trelix.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);
await builder.AddTrelixAsync();
// 此后再绑定业务 Options、读取业务配置以及 Build。
```

同样支持 `Host.CreateApplicationBuilder`。本地 `Trelix` 节字段为 `ServerUrl`、`ProjectKey`、`EnvironmentKey`、`FileName`、`AccessToken`；管理界面可以生成前四项及占位令牌，真实令牌通过 User Secrets 或 `Trelix__AccessToken` 等外部配置注入。

一个宿主只注册一次。连接参数在首次拉取前固定，修改连接或轮换令牌后重启应用。服务地址接受 HTTP/HTTPS 和路径前缀，不接受 URL 凭证、查询或片段。首次读取必须成功，失败以 `TrelixConfigurationException.Code` 和可选 `StatusCode` 提供安全诊断。

配置优先级为命令行 > 无前缀环境变量 > Trelix > User Secrets > 本地 JSON。先完成本地配置源的注册，再调用异步入口；后续自行增加的配置源遵守 .NET 后添加者优先规则。SDK 不替换容器中的 `IConfiguration`。

运行中失败保留最近成功配置，恢复后整体替换；删除后同名重建按新文件 ID 自动接入。没有磁盘缓存。使用 `IOptionsMonitor<T>` 或新作用域中的 `IOptionsSnapshot<T>` 消费更新；业务负责验证自身 Options 和重新应用已创建资源。

可通过本地节或 `configure` 调整 `ReadTimeout`（默认 10 秒）、`WatchTimeout`（40 秒）、`RetryMinDelay`（1 秒）、`RetryMaxDelay`（30 秒），配置节使用 TimeSpan 字符串。各间隔必须大于零且不超过一天，最大退避不小于最小退避；监听超时必须大于服务端 `Trelix:Distribution:WaitTimeout` 并预留传输余量。SDK 使用独立连接池，不继承宿主默认 HTTP 重试处理器。

示例与操作步骤见 [示例应用](../../../samples/Trelix.SampleApp/README.md) 和 [本地开发](../../../docs/local-development.md)。宿主必须正常停止并释放；构建前放弃启动时释放 `builder.Configuration`，以关闭首次接入创建的客户端。
