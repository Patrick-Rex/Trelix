using Microsoft.Extensions.Options;
using Trelix.Extensions.Configuration;

namespace Trelix.SampleApp;

/// <summary>展示首次异步接入后，以原生配置和 Options 消费少量公开示例字段。</summary>
public static class SampleApplication
{
    /// <summary>创建独立示例宿主，所有业务绑定都在远程配置首次加载之后执行。</summary>
    /// <param name="args">本地启动及命令行覆盖参数，真实令牌应使用外部配置注入。</param>
    /// <param name="configure">测试或集成宿主在 SDK 接入之前进行的本地配置。</param>
    /// <param name="cancellationToken">首次拉取取消信号。</param>
    /// <returns>已构建、尚未开始监听 HTTP 的示例应用。</returns>
    public static async Task<WebApplication> CreateAsync(string[] args, Action<WebApplicationBuilder>? configure = null,
        CancellationToken cancellationToken = default)
    {
        var builder = WebApplication.CreateBuilder(args);
        try
        {
            configure?.Invoke(builder);
            await builder.AddTrelixAsync(cancellationToken: cancellationToken);
            builder.Services.Configure<SampleOptions>(builder.Configuration.GetSection("Sample"));
            var app = builder.Build();
            app.MapGet("/configuration", Read);
            return app;
        }
        catch
        {
            ((IDisposable)builder.Configuration).Dispose();
            throw;
        }
    }

    /// <summary>只返回约定的公开示例值，不转储连接参数或任意业务配置。</summary>
    /// <param name="configuration">宿主原有配置系统。</param>
    /// <param name="monitor">能随提供程序通知更新的 Options。</param>
    /// <param name="snapshot">本次请求作用域的 Options。</param>
    /// <returns>三种原生读取方式下的示例值。</returns>
    private static SampleResponse Read(IConfiguration configuration, IOptionsMonitor<SampleOptions> monitor,
        IOptionsSnapshot<SampleOptions> snapshot) => new(configuration["Sample:Message"], monitor.CurrentValue, snapshot.Value);
}

/// <summary>公开展示用的两个示例配置字段，不承载凭证或连接字符串。</summary>
public sealed class SampleOptions
{
    /// <summary>用于观察重载的示例消息。</summary>
    public string? Message { get; set; }
    /// <summary>用于展示类型化绑定的功能开关。</summary>
    public bool Enabled { get; set; }
}

/// <summary>示例端点的有限输出。</summary>
/// <param name="Configuration">原生配置读取的消息。</param>
/// <param name="Monitor">当前监视器中的示例字段。</param>
/// <param name="Snapshot">本次请求作用域中的示例字段。</param>
public sealed record SampleResponse(string? Configuration, SampleOptions Monitor, SampleOptions Snapshot);
