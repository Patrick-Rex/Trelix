using Microsoft.Extensions.Options;
using Trelix.Server.Features.Distribution;

namespace Trelix.Server.Infrastructure.Notifications;

/// <summary>只唤醒发布监听者；持久化发布记录仍是变化结果的唯一依据。</summary>
public interface IReleaseNotifications
{
    /// <summary>注册一个有界、可释放的文件等待者。</summary>
    /// <param name="fileId">已通过授权解析的文件标识。</param>
    /// <returns>拥有本次等待注册的句柄。</returns>
    ReleaseSubscription Subscribe(Guid fileId);

    /// <summary>在事务提交后唤醒指定文件的等待者。</summary>
    /// <param name="fileId">已提交发布的文件标识。</param>
    void Notify(Guid fileId);
}

/// <summary>拥有一个等待任务及对应清理动作；释放可以重复调用。</summary>
/// <param name="signal">由通知管理器完成的等待任务。</param>
/// <param name="release">移除本次等待注册的动作。</param>
public sealed class ReleaseSubscription(Task signal, Action release) : IDisposable
{
    private Action? cleanup = release;

    /// <summary>仅表示应重新查询持久化状态的信号。</summary>
    public Task Signal { get; } = signal;

    /// <summary>释放等待注册，不再占用容量。</summary>
    public void Dispose() => Interlocked.Exchange(ref cleanup, null)?.Invoke();
}

/// <summary>通过短锁维护有总容量上限的等待者，最后一个等待者退出时移除文件条目。</summary>
/// <param name="options">监听容量配置。</param>
public sealed class ReleaseNotifications(IOptions<DistributionOptions> options) : IReleaseNotifications
{
    private readonly Lock gate = new();
    private readonly Dictionary<Guid, HashSet<TaskCompletionSource>> waiters = [];
    private int count;

    /// <summary>当前注册总数，用于诊断资源释放。</summary>
    public int ActiveCount { get { lock (gate) return count; } }

    /// <inheritdoc />
    public ReleaseSubscription Subscribe(Guid fileId)
    {
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (gate)
        {
            if (count >= options.Value.MaxConcurrentListeners)
                throw new ApiOperationException(503, "listener_capacity_exceeded", "监听容量已满，请稍后重试。");
            if (!waiters.TryGetValue(fileId, out var group))
                waiters.Add(fileId, group = []);
            group.Add(signal);
            count++;
        }
        return new(signal.Task, () => Remove(fileId, signal));
    }

    /// <inheritdoc />
    public void Notify(Guid fileId)
    {
        lock (gate)
        {
            if (waiters.TryGetValue(fileId, out var group))
                foreach (var signal in group)
                    signal.TrySetResult();
        }
    }

    /// <summary>删除完成或取消的注册，同时回收不再使用的文件条目。</summary>
    /// <param name="fileId">所属文件标识。</param>
    /// <param name="signal">本次注册的信号。</param>
    private void Remove(Guid fileId, TaskCompletionSource signal)
    {
        lock (gate)
        {
            if (!waiters.TryGetValue(fileId, out var group) || !group.Remove(signal))
                return;
            count--;
            if (group.Count == 0)
                waiters.Remove(fileId);
        }
    }
}
