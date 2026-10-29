namespace Trelix.Server.Common.Pagination;

/// <summary>管理接口分页参数的默认值和上限。</summary>
public static class PaginationDefaults
{
    /// <summary>未指定页码时读取的第一页。</summary>
    public const int Page = 1;
    /// <summary>未指定页大小时返回的最大条目数。</summary>
    public const int PageSize = 50;
    /// <summary>允许请求的最大页码。</summary>
    public const int MaxPage = 1_000_000;
    /// <summary>允许请求的最大页大小。</summary>
    public const int MaxPageSize = 100;
}
