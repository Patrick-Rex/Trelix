using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Trelix.Extensions.Configuration;

/// <summary>串行协调监听、读取和原子更新，失败时只重试而不清空快照。</summary>
/// <param name="state">固定连接及最近成功快照。</param>
/// <param name="options">本地固定的超时与重试设置。</param>
/// <param name="logger">仅记录固定类别的日志。</param>
/// <param name="time">可测试的退避时间来源。</param>
internal sealed class TrelixListener(TrelixConfigurationState state, TrelixOptions options,
    ILogger<TrelixListener> logger, TimeProvider time) : BackgroundService
{
    /// <summary>运行唯一更新循环；取消不进入重试，正常 204 不增加失败计数。</summary>
    /// <param name="stoppingToken">宿主停止信号。</param>
    /// <returns>后台循环退出任务。</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var failures = 0;
        while (!stoppingToken.IsCancellationRequested && !state.IsDisposed)
        {
            try
            {
                if (await state.Client.WatchAsync(state.Current, stoppingToken).ConfigureAwait(false))
                {
                    var next = await state.Client.ReadAsync(stoppingToken).ConfigureAwait(false);
                    if (!stoppingToken.IsCancellationRequested && !state.Apply(next))
                        logger.LogWarning("Trelix 配置已更新，但业务重载回调失败。");
                }
                if (failures != 0)
                    logger.LogInformation("Trelix 配置监听已恢复。");
                failures = 0;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested || state.IsDisposed)
            {
                break;
            }
            catch (ObjectDisposedException) when (state.IsDisposed)
            {
                break;
            }
            catch (TrelixConfigurationException ex)
            {
                failures = Math.Min(failures + 1, 30);
                var delay = RetryDelay(options, failures, Random.Shared.NextDouble());
                logger.LogWarning("Trelix 配置更新失败，保留最近配置。类别 {Code}，HTTP 状态 {Status}，重试间隔 {DelayMs} 毫秒。",
                    ex.Code, (int?)ex.StatusCode, delay.TotalMilliseconds);
                try
                {
                    await Task.Delay(delay, time, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    /// <summary>计算带抖动且位于配置上下限内的指数退避。</summary>
    /// <param name="options">退避范围。</param>
    /// <param name="failures">连续失败次数，从一开始。</param>
    /// <param name="jitter">零到一之间的抖动样本。</param>
    /// <returns>下一次重试前等待的间隔。</returns>
    internal static TimeSpan RetryDelay(TrelixOptions options, int failures, double jitter)
    {
        var ceiling = Math.Min(options.RetryMaxDelay.TotalMilliseconds,
            options.RetryMinDelay.TotalMilliseconds * Math.Pow(2, Math.Min(failures, 30)));
        return TimeSpan.FromMilliseconds(options.RetryMinDelay.TotalMilliseconds
            + (ceiling - options.RetryMinDelay.TotalMilliseconds) * Math.Clamp(jitter, 0, 1));
    }

    /// <summary>取消并等待后台循环，再关闭连接池。</summary>
    /// <param name="cancellationToken">宿主关闭期限。</param>
    /// <returns>监听停止任务。</returns>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try { await base.StopAsync(cancellationToken).ConfigureAwait(false); }
        finally { state.Dispose(); }
    }

    /// <summary>兜底取消后台循环并释放接入资源。</summary>
    public override void Dispose()
    {
        base.Dispose();
        state.Dispose();
    }
}
