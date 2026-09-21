using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Trelix.Core.Middleware;

/// <summary>将未处理异常转换为安全错误响应，日志仅记录异常类型和追踪标识。</summary>
/// <param name="problems">安全错误响应写入服务。</param>
/// <param name="logger">诊断日志服务。</param>
public sealed class SafeExceptionHandler(IProblemDetailsService problems, ILogger<SafeExceptionHandler> logger) : IExceptionHandler
{
    /// <summary>记录安全诊断信息并写入通用 500 响应。</summary>
    /// <param name="context">当前 HTTP 请求上下文。</param>
    /// <param name="exception">当前待处理或记录的异常。</param>
    /// <param name="cancellationToken">取消当前操作的令牌。</param>
    /// <returns>响应写入完成后返回 true，表示异常已处理。</returns>
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        // Exception messages and attached values can contain credentials or configuration.
        logger.LogError("Request failed with {ExceptionType}; trace {TraceId}", exception.GetType().Name, context.TraceIdentifier);
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails { Status = 500, Title = "请求处理失败。" }
        });
        return true;
    }
}
