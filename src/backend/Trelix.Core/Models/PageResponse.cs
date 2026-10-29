namespace Trelix.Core.Models;

/// <summary>有界列表响应；条目数量等于页大小时可请求下一页。</summary>
/// <typeparam name="T">列表条目契约。</typeparam>
/// <param name="Items">当前页条目。</param>
/// <param name="Page">从 1 开始的页码。</param>
/// <param name="PageSize">每页最大条目数。</param>
public sealed record PageResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize);
