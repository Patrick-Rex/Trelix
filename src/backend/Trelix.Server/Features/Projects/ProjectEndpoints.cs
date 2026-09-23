using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Trelix.Server.Features.Projects;

namespace Trelix.Server.Features.Projects;

/// <summary>Projects的 Minimal API HTTP 边界。</summary>
public static class ProjectEndpoints
{
    /// <summary>注册Projects端点并维护 OpenAPI 元数据。</summary>
    /// <param name="admin">管理员路由组。</param>
    public static void MapProjects(this RouteGroupBuilder admin)
    {
        var group = admin.MapGroup("/projects").WithTags("Projects");
        group.MapGet("/", ListProjects).WithSummary("分页读取项目。");
        group.MapGet("/{projectId:guid}", GetProject).WithSummary("读取项目。");
        group.MapPost("/", CreateProject).WithSummary("创建项目。");
        group.MapPut("/{projectId:guid}", UpdateProject).WithSummary("重命名项目。");
        group.MapDelete("/{projectId:guid}", DeleteProject).WithSummary("删除空项目。");
        group.MapGet("/{projectId:guid}/environments", ListEnvironments).WithSummary("分页读取项目环境。");
        group.MapGet("/{projectId:guid}/environments/{environmentId:guid}", GetEnvironment).WithSummary("读取项目环境。");
        group.MapPost("/{projectId:guid}/environments", CreateEnvironment).WithSummary("创建项目环境。");
        group.MapPut("/{projectId:guid}/environments/{environmentId:guid}", UpdateEnvironment).WithSummary("重命名项目环境。");
        group.MapDelete("/{projectId:guid}/environments/{environmentId:guid}", DeleteEnvironment).WithSummary("删除无文件及授权的环境。");
    }

    /// <summary>分页读取项目。</summary>
    /// <param name="service">项目环境服务。</param>
    /// <param name="ct">请求取消令牌。</param>
    /// <param name="page">页码。</param>
    /// <param name="pageSize">页大小。</param>
    /// <returns>操作结果；失败由统一错误处理器转换为 Problem Details。</returns>
    private static async Task<Ok<PageResponse<ProjectResponse>>> ListProjects(ProjectService service, CancellationToken ct, [Range(1, 1000000)] int page = 1, [Range(1, 100)] int pageSize = 50)
    {
        return TypedResults.Ok(await service.ListProjectsAsync(page, pageSize, ct));
    }

    /// <summary>读取项目。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="service">项目环境服务。</param>
    /// <param name="ct">请求取消令牌。</param>
    /// <returns>操作结果；失败由统一错误处理器转换为 Problem Details。</returns>
    private static async Task<Ok<ProjectResponse>> GetProject(Guid projectId, ProjectService service, CancellationToken ct)
    {
        return TypedResults.Ok(await service.GetProjectAsync(projectId, ct));
    }

    /// <summary>创建项目。</summary>
    /// <param name="request">项目名称。</param>
    /// <param name="service">项目环境服务。</param>
    /// <param name="ct">请求取消令牌。</param>
    /// <returns>操作结果；失败由统一错误处理器转换为 Problem Details。</returns>
    private static async Task<Created<ProjectResponse>> CreateProject(ResourceRequest request, ProjectService service, CancellationToken ct)
    {
        var result = await service.CreateProjectAsync(request, ct);
        return TypedResults.Created($"/api/admin/projects/{result.Id}", result);
    }

    /// <summary>重命名项目。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="request">名称及并发基准。</param>
    /// <param name="service">项目环境服务。</param>
    /// <param name="ct">请求取消令牌。</param>
    /// <returns>操作结果；失败由统一错误处理器转换为 Problem Details。</returns>
    private static async Task<Ok<ProjectResponse>> UpdateProject(Guid projectId, ResourceRequest request, ProjectService service, CancellationToken ct)
    {
        return TypedResults.Ok(await service.UpdateProjectAsync(projectId, request, ct));
    }

    /// <summary>删除空项目。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="concurrencyStamp">并发基准。</param>
    /// <param name="service">项目环境服务。</param>
    /// <param name="ct">请求取消令牌。</param>
    /// <returns>操作结果；失败由统一错误处理器转换为 Problem Details。</returns>
    private static async Task<NoContent> DeleteProject(Guid projectId, Guid concurrencyStamp, ProjectService service, CancellationToken ct)
    {
        await service.DeleteProjectAsync(projectId, concurrencyStamp, ct);
        return TypedResults.NoContent();
    }

    /// <summary>分页读取项目环境。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="service">项目环境服务。</param>
    /// <param name="ct">请求取消令牌。</param>
    /// <param name="page">页码。</param>
    /// <param name="pageSize">页大小。</param>
    /// <returns>操作结果；失败由统一错误处理器转换为 Problem Details。</returns>
    private static async Task<Ok<PageResponse<EnvironmentResponse>>> ListEnvironments(Guid projectId, ProjectService service, CancellationToken ct, [Range(1, 1000000)] int page = 1, [Range(1, 100)] int pageSize = 50)
    {
        return TypedResults.Ok(await service.ListEnvironmentsAsync(projectId, page, pageSize, ct));
    }

    /// <summary>读取项目环境。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="environmentId">环境标识。</param>
    /// <param name="service">项目环境服务。</param>
    /// <param name="ct">请求取消令牌。</param>
    /// <returns>操作结果；失败由统一错误处理器转换为 Problem Details。</returns>
    private static async Task<Ok<EnvironmentResponse>> GetEnvironment(Guid projectId, Guid environmentId, ProjectService service, CancellationToken ct)
    {
        return TypedResults.Ok(await service.GetEnvironmentAsync(projectId, environmentId, ct));
    }

    /// <summary>创建项目环境。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="request">环境名称。</param>
    /// <param name="service">项目环境服务。</param>
    /// <param name="ct">请求取消令牌。</param>
    /// <returns>操作结果；失败由统一错误处理器转换为 Problem Details。</returns>
    private static async Task<Created<EnvironmentResponse>> CreateEnvironment(Guid projectId, ResourceRequest request, ProjectService service, CancellationToken ct)
    {
        var result = await service.CreateEnvironmentAsync(projectId, request, ct);
        return TypedResults.Created($"/api/admin/projects/{projectId}/environments/{result.Id}", result);
    }

    /// <summary>重命名项目环境。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="environmentId">环境标识。</param>
    /// <param name="request">名称及并发基准。</param>
    /// <param name="service">项目环境服务。</param>
    /// <param name="ct">请求取消令牌。</param>
    /// <returns>操作结果；失败由统一错误处理器转换为 Problem Details。</returns>
    private static async Task<Ok<EnvironmentResponse>> UpdateEnvironment(Guid projectId, Guid environmentId, ResourceRequest request, ProjectService service, CancellationToken ct)
    {
        return TypedResults.Ok(await service.UpdateEnvironmentAsync(projectId, environmentId, request, ct));
    }

    /// <summary>删除无文件及授权的环境。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="environmentId">环境标识。</param>
    /// <param name="concurrencyStamp">并发基准。</param>
    /// <param name="service">项目环境服务。</param>
    /// <param name="ct">请求取消令牌。</param>
    /// <returns>操作结果；失败由统一错误处理器转换为 Problem Details。</returns>
    private static async Task<NoContent> DeleteEnvironment(Guid projectId, Guid environmentId, Guid concurrencyStamp, ProjectService service, CancellationToken ct)
    {
        await service.DeleteEnvironmentAsync(projectId, environmentId, concurrencyStamp, ct);
        return TypedResults.NoContent();
    }
}
