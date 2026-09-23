using System.Threading.Channels;

namespace Trelix.Extensions.Configuration.Tests;

/// <summary>让测试显式观察和触发退避定时器，避免按墙上时间猜测后台循环进度。</summary>
internal sealed class ControlledTimeProvider : TimeProvider
{
    private readonly Channel<ControlledTimer> timers = Channel.CreateUnbounded<ControlledTimer>();

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ControlledTimer(callback, state, dueTime);
        timers.Writer.TryWrite(timer);
        return timer;
    }

    /// <summary>等待下一次真实退避注册。</summary>
    /// <returns>可手动触发的定时器。</returns>
    internal async Task<ControlledTimer> NextAsync() =>
        await timers.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask()
            .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
}

/// <summary>记录释放状态并提供同步触发点的测试定时器。</summary>
/// <param name="callback">Task.Delay 注册的回调。</param>
/// <param name="state">回调状态。</param>
/// <param name="dueTime">本次等待间隔。</param>
internal sealed class ControlledTimer(TimerCallback callback, object? state, TimeSpan dueTime) : ITimer
{
    internal TimeSpan DueTime { get; } = dueTime;
    internal bool Disposed { get; private set; }

    /// <summary>在测试允许重试时触发到期回调。</summary>
    internal void Fire() => callback(state);

    /// <inheritdoc />
    public bool Change(TimeSpan dueTime, TimeSpan period) => throw new NotSupportedException();

    /// <inheritdoc />
    public void Dispose() => Disposed = true;

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
