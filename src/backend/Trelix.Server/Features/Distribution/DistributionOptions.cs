namespace Trelix.Server.Features.Distribution;

/// <summary>单实例监听的等待时间与资源容量，启动时验证。</summary>
public sealed class DistributionOptions
{
    /// <summary>正常长轮询等待时间；SDK 的监听超时必须留有传输余量。</summary>
    public TimeSpan WaitTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>整个实例同时持有的最大等待注册数。</summary>
    public int MaxConcurrentListeners { get; set; } = 1024;
}
