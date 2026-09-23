namespace Trelix.Extensions.Configuration;

/// <summary>管理配置源重建期间共享的快照、提供程序和 HTTP 所有权。</summary>
/// <param name="initial">首次异步取得的有效快照。</param>
/// <param name="client">本次接入拥有的客户端。</param>
internal sealed class TrelixConfigurationState(ConfigurationSnapshot initial, TrelixHttpClient client) : IDisposable
{
    private readonly Lock gate = new();
    private readonly HashSet<TrelixConfigurationProvider> providers = [];
    private ConfigurationSnapshot current = initial;
    private int disposed;

    /// <summary>线程安全取得最近成功快照。</summary>
    internal ConfigurationSnapshot Current => Volatile.Read(ref current);
    /// <summary>供唯一后台循环使用的 HTTP 客户端。</summary>
    internal TrelixHttpClient Client { get; } = client;
    /// <summary>表示所有配置提供程序已被释放。</summary>
    internal bool IsDisposed => Volatile.Read(ref disposed) != 0;

    /// <summary>注册新的提供程序；源重建先创建新提供程序再释放旧提供程序。</summary>
    /// <param name="provider">本次配置源生成的提供程序。</param>
    internal void Attach(TrelixConfigurationProvider provider)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            providers.Add(provider);
        }
    }

    /// <summary>解除提供程序注册，并在最后一个提供程序退出时释放连接池。</summary>
    /// <param name="provider">待解除的提供程序。</param>
    internal void Detach(TrelixConfigurationProvider provider)
    {
        lock (gate)
        {
            providers.Remove(provider);
            if (providers.Count == 0)
                Dispose();
        }
    }

    /// <summary>拒绝倒退和重复版本，整体替换快照后通知当前提供程序。</summary>
    /// <param name="next">新读取并校验成功的快照。</param>
    /// <returns>所有业务重载回调是否成功；重复版本不发出通知。</returns>
    internal bool Apply(ConfigurationSnapshot next)
    {
        TrelixConfigurationProvider[] targets;
        lock (gate)
        {
            if (IsDisposed)
                return true;
            if (next.FileId == current.FileId)
            {
                if (next.Version < current.Version)
                    throw new TrelixConfigurationException("stale_response");
                if (next.Version == current.Version)
                    return true;
            }
            Volatile.Write(ref current, next);
            targets = [.. providers];
        }
        var succeeded = true;
        foreach (var provider in targets)
            succeeded &= provider.Reload(next);
        return succeeded;
    }

    /// <summary>关闭客户端，保留可供关闭阶段读取的最近成功内存快照。</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
            Client.Dispose();
    }
}
