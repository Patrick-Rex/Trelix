using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace Trelix.Server.Infrastructure.Middleware;

/// <summary>将业务异常和乐观并发冲突转换为可公开的 Problem Details。</summary>
/// <param name="problems">安全错误响应写入服务。</param>
public sealed class ApiOperationExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    /// <summary>处理明确的业务错误及数据库并发冲突，将其他异常交给后续处理器。</summary>
    /// <param name="context">当前 HTTP 请求上下文。</param>
    /// <param name="exception">当前待处理或记录的异常。</param>
    /// <param name="cancellationToken">取消当前操作的令牌。</param>
    /// <returns>已处理时为 true；不支持的异常为 false。</returns>
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var failure = exception switch
        {
            ApiOperationException operation => operation,
            BadHttpRequestException badRequest => new ApiOperationException(badRequest.StatusCode, "invalid_request", "请求字段缺失或格式无效。"),
            DbUpdateConcurrencyException => new ApiOperationException(409, "concurrent_change", "资源已被其他操作修改，请刷新后重试。"),
            DbUpdateException { InnerException: SqliteException { SqliteExtendedErrorCode: 2067 or 1555 } } =>
                new ApiOperationException(409, "duplicate_resource", "资源标识或版本已存在。"),
            DbUpdateException { InnerException: SqliteException sqlite }
                when sqlite.SqliteExtendedErrorCode == 787 || (sqlite.SqliteExtendedErrorCode == 1811
                    && sqlite.Message.Contains("FOREIGN KEY constraint failed", StringComparison.Ordinal)) =>
                new ApiOperationException(409, "resource_in_use", "资源关联已变化或仍被引用，请刷新后重试。"),
            _ => null
        };
        if (failure is null)
            return false;

        context.Response.StatusCode = failure.Status;
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = failure.Status, Title = failure.Message,
                Extensions = { ["code"] = failure.Code }
            }
        });
        return true;
    }
}
