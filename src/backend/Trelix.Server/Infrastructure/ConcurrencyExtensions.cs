using Trelix.Core.Abstractions;

namespace Trelix.Server.Infrastructure;

/// <summary>校验客户端与持久化资源之间的并发基准。</summary>
public static class ConcurrencyExtensions
{
    /// <summary>校验调用方提供了非空且仍有效的并发标记。</summary>
    /// <param name="resource">当前持久化资源。</param>
    /// <param name="expected">调用方基准。</param>
    /// <exception cref="ApiOperationException">缺少基准或基准已过期。</exception>
    public static void RequireStamp(this IConcurrencyStamped resource, Guid? expected)
    {
        if (expected is null || expected == Guid.Empty)
            throw new ApiOperationException(400, "invalid_request", "必须提供资源的并发基准。");
        if (expected != resource.ConcurrencyStamp)
            throw new ApiOperationException(409, "concurrent_change", "资源已被其他操作修改，请刷新后重试。");
    }
}
