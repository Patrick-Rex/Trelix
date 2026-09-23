using System.ComponentModel.DataAnnotations;

namespace Trelix.Server.Features.Projects;

/// <summary>创建或修改项目、环境的标识和显示名；标识按原值区分大小写。</summary>
public sealed record ResourceRequest
{
    /// <summary>所属范围内唯一的业务标识。</summary>
    [Required, MaxLength(128)]
    public required string Key { get; init; }
    /// <summary>管理界面显示名称。</summary>
    [Required, MaxLength(200)]
    public required string DisplayName { get; init; }
    /// <summary>修改时必需的并发基准；创建时不使用。</summary>
    public Guid? ConcurrencyStamp { get; init; }
}

/// <summary>项目元数据及后续写入所需的并发基准。</summary>
/// <param name="Id">稳定的项目标识。</param>
/// <param name="Key">项目业务标识。</param>
/// <param name="DisplayName">显示名称。</param>
/// <param name="ConcurrencyStamp">并发基准。</param>
public sealed record ProjectResponse(Guid Id, string Key, string DisplayName, Guid ConcurrencyStamp);

/// <summary>环境元数据及所属项目。</summary>
/// <param name="Id">稳定的环境标识。</param>
/// <param name="ProjectId">所属项目标识。</param>
/// <param name="Key">项目内唯一的业务标识。</param>
/// <param name="DisplayName">显示名称。</param>
/// <param name="ConcurrencyStamp">并发基准。</param>
public sealed record EnvironmentResponse(Guid Id, Guid ProjectId, string Key, string DisplayName, Guid ConcurrencyStamp);

/// <summary>有界列表响应；条目数量等于页大小时可请求下一页。</summary>
/// <typeparam name="T">列表条目契约。</typeparam>
/// <param name="Items">当前页条目。</param>
/// <param name="Page">从 1 开始的页码。</param>
/// <param name="PageSize">每页最大条目数。</param>
public sealed record PageResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize);
