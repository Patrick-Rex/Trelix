using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Trelix.Server.Features.Projects;

namespace Trelix.Server.Features.ConfigFiles;

/// <summary>ConfigFiles的 Minimal API HTTP 边界。</summary>
public static class ConfigFileEndpoints
{
    /// <summary>注册ConfigFiles端点并维护 OpenAPI 元数据。</summary>
    /// <param name="admin">管理员路由组。</param>
    public static void MapConfigFiles(this RouteGroupBuilder admin)
    {
        var group = admin.MapGroup("/projects/{projectId:guid}/environments/{environmentId:guid}/files").WithTags("ConfigFiles");
        group.MapGet("/", List).WithSummary("分页读取文件元数据。");
        group.MapGet("/{fileId:guid}", Get).WithSummary("读取文件和草稿。");
        group.MapPost("/", Create).WithSummary("创建空配置文件。");
        group.MapPut("/{fileId:guid}", Rename).WithSummary("重命名配置文件。");
        group.MapPut("/{fileId:guid}/draft", SaveDraft).WithSummary("保存草稿并递增修订，不影响已发布内容。");
        group.MapDelete("/{fileId:guid}", Delete).WithSummary("原子删除文件及全部草稿和历史。");
    }

    /// <summary>分页读取文件元数据。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="environmentId">环境标识。</param>
    /// <param name="service">文件服务。</param>
    /// <param name="ct">请求取消令牌。</param>
    /// <param name="page">页码。</param>
    /// <param name="pageSize">页大小。</param>
    /// <returns>操作结果；失败由统一错误处理器转换为 Problem Details。</returns>
    private static async Task<Ok<PageResponse<ConfigFileResponse>>> List(Guid projectId, Guid environmentId, ConfigFileService service, CancellationToken ct, [Range(1, 1000000)] int page = 1, [Range(1, 100)] int pageSize = 50)
    {
        return TypedResults.Ok(await service.ListAsync(projectId, environmentId, page, pageSize, ct));
    }

    /// <summary>读取文件和草稿。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="environmentId">环境标识。</param>
    /// <param name="fileId">文件标识。</param>
    /// <param name="service">文件服务。</param>
    /// <param name="ct">请求取消令牌。</param>
    /// <returns>操作结果；失败由统一错误处理器转换为 Problem Details。</returns>
    private static async Task<Ok<DraftResponse>> Get(Guid projectId, Guid environmentId, Guid fileId, ConfigFileService service, CancellationToken ct)
    {
        return TypedResults.Ok(await service.GetAsync(projectId, environmentId, fileId, ct));
    }

    /// <summary>创建空配置文件。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="environmentId">环境标识。</param>
    /// <param name="request">文件名。</param>
    /// <param name="service">文件服务。</param>
    /// <param name="ct">请求取消令牌。</param>
    /// <returns>操作结果；失败由统一错误处理器转换为 Problem Details。</returns>
    private static async Task<Created<ConfigFileResponse>> Create(Guid projectId, Guid environmentId, CreateConfigFileRequest request, ConfigFileService service, CancellationToken ct)
    {
        var result = await service.CreateAsync(projectId, environmentId, request, ct);
        return TypedResults.Created($"/api/admin/projects/{projectId}/environments/{environmentId}/files/{result.Id}", result);
    }

    /// <summary>重命名配置文件。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="environmentId">环境标识。</param>
    /// <param name="fileId">文件标识。</param>
    /// <param name="request">名称和并发基准。</param>
    /// <param name="service">文件服务。</param>
    /// <param name="ct">请求取消令牌。</param>
    /// <returns>操作结果；失败由统一错误处理器转换为 Problem Details。</returns>
    private static async Task<Ok<ConfigFileResponse>> Rename(Guid projectId, Guid environmentId, Guid fileId, RenameConfigFileRequest request, ConfigFileService service, CancellationToken ct)
    {
        return TypedResults.Ok(await service.RenameAsync(projectId, environmentId, fileId, request, ct));
    }

    /// <summary>保存草稿并递增修订，不影响已发布内容。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="environmentId">环境标识。</param>
    /// <param name="fileId">文件标识。</param>
    /// <param name="request">JSON 正文和并发基准。</param>
    /// <param name="service">文件服务。</param>
    /// <param name="ct">请求取消令牌。</param>
    /// <returns>操作结果；失败由统一错误处理器转换为 Problem Details。</returns>
    private static async Task<Ok<DraftResponse>> SaveDraft(Guid projectId, Guid environmentId, Guid fileId, SaveDraftRequest request, ConfigFileService service, CancellationToken ct)
    {
        return TypedResults.Ok(await service.SaveDraftAsync(projectId, environmentId, fileId, request, ct));
    }

    /// <summary>原子删除文件及全部草稿和历史。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="environmentId">环境标识。</param>
    /// <param name="fileId">文件标识。</param>
    /// <param name="concurrencyStamp">并发基准。</param>
    /// <param name="service">文件服务。</param>
    /// <param name="ct">请求取消令牌。</param>
    /// <returns>操作结果；失败由统一错误处理器转换为 Problem Details。</returns>
    private static async Task<NoContent> Delete(Guid projectId, Guid environmentId, Guid fileId, Guid concurrencyStamp, ConfigFileService service, CancellationToken ct)
    {
        await service.DeleteAsync(projectId, environmentId, fileId, concurrencyStamp, ct);
        return TypedResults.NoContent();
    }
}
