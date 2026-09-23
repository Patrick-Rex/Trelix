using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.CommandLine;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Trelix.Extensions.Configuration;

/// <summary>在业务配置消费与宿主构建之前完成远程配置接入。</summary>
public static class TrelixHostExtensions
{
    /// <summary>从本地 Trelix 节读取连接信息，首次异步拉取成功后添加配置源和后台监听。</summary>
    /// <typeparam name="TBuilder">现代宿主构建器类型。</typeparam>
    /// <param name="builder">尚未构建且尚未消费业务配置的宿主构建器。</param>
    /// <param name="configure">在本地节绑定后调整连接或重试选项。</param>
    /// <param name="cancellationToken">首次接入的取消信号。</param>
    /// <returns>已经加载远程配置的原构建器。</returns>
    /// <exception cref="TrelixConfigurationException">本地选项、首次读取或响应校验失败。</exception>
    /// <exception cref="InvalidOperationException">该宿主已经接入 Trelix。</exception>
    public static Task<TBuilder> AddTrelixAsync<TBuilder>(this TBuilder builder, Action<TrelixOptions>? configure = null,
        CancellationToken cancellationToken = default) where TBuilder : IHostApplicationBuilder =>
        AddCoreAsync(builder, configure, null, TimeProvider.System, cancellationToken);

    /// <summary>内部接入实现，允许测试替换传输与时钟而不扩张公开配置契约。</summary>
    /// <typeparam name="TBuilder">现代宿主构建器类型。</typeparam>
    /// <param name="builder">尚未构建的宿主构建器。</param>
    /// <param name="configure">本地选项调整动作。</param>
    /// <param name="handler">测试传输，接入取得其所有权。</param>
    /// <param name="time">后台退避时钟。</param>
    /// <param name="ct">首次接入取消信号。</param>
    /// <returns>原构建器。</returns>
    internal static async Task<TBuilder> AddCoreAsync<TBuilder>(TBuilder builder, Action<TrelixOptions>? configure,
        HttpMessageHandler? handler, TimeProvider time, CancellationToken ct) where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (builder.Services.Any(x => x.ServiceType == typeof(TrelixConfigurationState)))
            throw new InvalidOperationException("Each host supports one Trelix configuration registration.");
        // 在首次 await 前占用注册标记，避免并发接入创建多个循环。
        var marker = ServiceDescriptor.Singleton<TrelixConfigurationState>(_ => throw new InvalidOperationException("Trelix registration is incomplete."));
        builder.Services.Add(marker);
        TrelixConfigurationState? state = null;
        TrelixHttpClient? client = null;
        try
        {
            var local = new TrelixOptions();
            try { builder.Configuration.GetSection("Trelix").Bind(local); }
            catch (InvalidOperationException) { throw new TrelixConfigurationException("invalid_options"); }
            configure?.Invoke(local);
            var options = local.Freeze();
            client = new(options, handler);
            var snapshot = await client.ReadAsync(ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            state = new(snapshot, client);
            var source = new TrelixConfigurationSource(state);
            var sources = builder.Configuration.Sources;
            var position = sources.Count;
            var environmentIndex = -1;
            var commandLineIndex = -1;
            for (var i = 0; i < sources.Count; i++)
            {
                if (sources[i] is EnvironmentVariablesConfigurationSource { Prefix: null or "" }) environmentIndex = i;
                if (sources[i] is CommandLineConfigurationSource) commandLineIndex = i;
            }
            // 默认宿主会在本地 JSON 前添加宿主级命令行源；以最后的业务源为准。
            if (environmentIndex >= 0) position = environmentIndex;
            if (commandLineIndex >= 0 && (environmentIndex < 0 || commandLineIndex > environmentIndex))
                position = Math.Min(position, commandLineIndex);
            sources.Insert(position, source);
            builder.Services.Remove(marker);
            builder.Services.AddSingleton(state);
            builder.Services.AddSingleton<IHostedService>(provider => new TrelixListener(state, options,
                provider.GetRequiredService<ILogger<TrelixListener>>(), time));
            return builder;
        }
        catch
        {
            builder.Services.Remove(marker);
            if (state is not null) state.Dispose();
            else client?.Dispose();
            throw;
        }
    }
}
