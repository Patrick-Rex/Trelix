using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Trelix.Server.Features.Projects;

namespace Trelix.Server.Features.Releases;

/// <summary>Releases的 Minimal API HTTP 边界。</summary>
public static class ReleaseEndpoints
{
    /// <summary>注册Releases端点并维护 OpenAPI 元数据。</summary>
    /// <param name="admin">管理员路由组。</param>
    public static void MapReleases(this RouteGroupBuilder admin)
    {
        var group = admin.MapGroup("/projects/{projectId:guid}/environments/{environmentId:guid}/files/{fileId:guid}").WithTags("Releases");
        group.MapGet("/releases", List).WithSummary("分页读取发布历史元数据。");
        group.MapGet("/releases/{version:long}", Get).WithSummary("读取指定历史版本正文。");
        group.MapPost("/releases", Publish).WithSummary("发布指定草稿修订。");
        group.MapPost("/rollback", Rollback).WithSummary("以历史正文生成新版本，保留当前草稿。");
    }

    /// <summary>分页读取发布历史元数据。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="environmentId">环境标识。</param>
    /// <param name="fileId">文件标识。</param>
    /// <param name="service">发布服务。</param>
    /// <param name="ct">请求取消令牌。</param>
    /// <param name="page">页码。</param>
    /// <param name="pageSize">页大小。</param>
    /// <returns>操作结果；失败由统一错误处理器转换为 Problem Details。</returns>
    private static async Task<Ok<PageResponse<ReleaseResponse>>> List(Guid projectId, Guid environmentId, Guid fileId, ReleaseService service, CancellationToken ct, [Range(1, 1000000)] int page = 1, [Range(1, 100)] int pageSize = 50)
    {
        return TypedResults.Ok(await service.ListAsync(projectId, environmentId, fileId, page, pageSize, ct));
    }

    /// <summary>读取指定历史版本正文。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="environmentId">环境标识。</param>
    /// <param name="fileId">文件标识。</param>
    /// <param name="version">发布版本号。</param>
    /// <param name="service">发布服务。</param>
    /// <param name="ct">请求取消令牌。</param>
    /// <returns>操作结果；失败由统一错误处理器转换为 Problem Details。</returns>
    private static async Task<Ok<ReleaseDetailResponse>> Get(Guid projectId, Guid environmentId, Guid fileId, [Range(1, long.MaxValue)] long version, ReleaseService service, CancellationToken ct)
    {
        return TypedResults.Ok(await service.GetAsync(projectId, environmentId, fileId, version, ct));
    }

    /// <summary>发布指定草稿修订。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="environmentId">环境标识。</param>
    /// <param name="fileId">文件标识。</param>
    /// <param name="request">草稿修订与并发基准。</param>
    /// <param name="service">发布服务。</param>
    /// <param name="ct">请求取消令牌。</param>
    /// <returns>操作结果；失败由统一错误处理器转换为 Problem Details。</returns>
    private static async Task<Created<PublicationResponse>> Publish(Guid projectId, Guid environmentId, Guid fileId, PublishRequest request, ReleaseService service, CancellationToken ct)
    {
        var result = await service.PublishAsync(projectId, environmentId, fileId, request, ct);
        return TypedResults.Created($"/api/admin/projects/{projectId}/environments/{environmentId}/files/{fileId}/releases/{result.Release.Version}", result);
    }

    /// <summary>以历史正文生成新版本，保留当前草稿。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="environmentId">环境标识。</param>
    /// <param name="fileId">文件标识。</param>
    /// <param name="request">源版本与并发基准。</param>
    /// <param name="service">发布服务。</param>
    /// <param name="ct">请求取消令牌。</param>
    /// <returns>操作结果；失败由统一错误处理器转换为 Problem Details。</returns>
    private static async Task<Created<PublicationResponse>> Rollback(Guid projectId, Guid environmentId, Guid fileId, RollbackRequest request, ReleaseService service, CancellationToken ct)
    {
        var result = await service.RollbackAsync(projectId, environmentId, fileId, request, ct);
        return TypedResults.Created($"/api/admin/projects/{projectId}/environments/{environmentId}/files/{fileId}/releases/{result.Release.Version}", result);
    }
}
