using Microsoft.EntityFrameworkCore;
using Trelix.Core.Models;

namespace Trelix.Server.Persistence;

/// <summary>为已完成过滤、稳定排序与投影的数据库查询提供分页。</summary>
public static class PaginationQueryExtensions
{
    /// <summary>在数据库侧限制条目并异步读取有界结果。</summary>
    /// <typeparam name="T">查询结果类型。</typeparam>
    /// <param name="source">已稳定排序的查询。</param>
    /// <param name="page">已验证的页码，从 1 开始。</param>
    /// <param name="pageSize">已验证的每页最大条目数。</param>
    /// <param name="cancellationToken">取消当前查询的令牌。</param>
    /// <returns>当前页条目与请求的分页参数。</returns>
    public static async Task<PageResponse<T>> ToPageAsync<T>(this IQueryable<T> source, int page, int pageSize, CancellationToken cancellationToken) =>
        new(await source.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken), page, pageSize);
}
