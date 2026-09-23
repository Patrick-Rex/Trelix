using Microsoft.Extensions.Configuration;

namespace Trelix.Extensions.Configuration;

/// <summary>为每次配置源重建创建独立提供程序，共享同一接入状态和监听循环。</summary>
/// <param name="state">本次接入拥有的内存状态。</param>
internal sealed class TrelixConfigurationSource(TrelixConfigurationState state) : IConfigurationSource
{
    /// <inheritdoc />
    public IConfigurationProvider Build(IConfigurationBuilder builder) => new TrelixConfigurationProvider(state);
}

/// <summary>只同步加载已经取得的快照，不在配置提供程序内执行网络请求。</summary>
internal sealed class TrelixConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly TrelixConfigurationState state;
    private int disposed;

    /// <summary>注册本次提供程序，并取得当前完整快照。</summary>
    /// <param name="state">共享的接入状态。</param>
    internal TrelixConfigurationProvider(TrelixConfigurationState state)
    {
        this.state = state;
        state.Attach(this);
    }

    /// <inheritdoc />
    public override void Load() => Data = state.Current.Data;

    /// <summary>替换完整数据后发送重载信号，隔离业务回调异常。</summary>
    /// <param name="snapshot">已完成校验的新快照。</param>
    /// <returns>业务重载回调是否全部成功。</returns>
    internal bool Reload(ConfigurationSnapshot snapshot)
    {
        Data = snapshot.Data;
        try
        {
            OnReload();
            return true;
        }
        catch (Exception)
        {
            // 业务 Options/变更回调不能终止配置监听，快照已经成功更新。
            return false;
        }
    }

    /// <summary>解除本次注册；最后一个提供程序退出后释放接入资源。</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
            state.Detach(this);
    }
}
