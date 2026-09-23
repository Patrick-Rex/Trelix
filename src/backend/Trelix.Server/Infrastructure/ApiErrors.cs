using System.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Trelix.Server.Infrastructure;

/// <summary>统一生成带业务错误码和追踪标识的 Problem Details。</summary>
public static class ApiErrors
{
    /// <summary>使用可公开的信息构造指定状态码的错误响应。</summary>
    /// <param name="context">当前 HTTP 请求上下文。</param>
    /// <param name="status">HTTP 响应状态码。</param>
    /// <param name="code">客户端可识别的业务错误码。</param>
    /// <param name="title">允许公开的错误提示。</param>
    /// <returns>内容类型为 application/problem+json 的 Minimal API 结果。</returns>
    public static ProblemHttpResult Result(HttpContext context, int status, string code, string title)
    {
        var problem = new ProblemDetails { Status = status, Title = title };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;
        return TypedResults.Problem(problem);
    }

    /// <summary>按状态码补充默认业务错误码及追踪标识，保留已有扩展值。</summary>
    /// <param name="context">待补充扩展字段的 Problem Details 上下文。</param>
    public static void Customize(ProblemDetailsContext context)
    {
        context.ProblemDetails.Extensions.TryAdd("code", context.ProblemDetails.Status switch
        {
            400 => "invalid_request",
            401 => "authentication_required",
            403 => "access_denied",
            404 => "not_found",
            405 => "method_not_allowed",
            409 => "conflict",
            429 => "rate_limited",
            _ => "request_failed"
        });
        context.ProblemDetails.Extensions.TryAdd("traceId", Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
    }
}
