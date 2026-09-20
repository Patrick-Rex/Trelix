using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Trelix.Server.Infrastructure;

public static class ApiErrors
{
    public static ObjectResult Result(HttpContext context, int status, string code, string title)
    {
        var problem = new ProblemDetails { Status = status, Title = title };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;
        return new ObjectResult(problem) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }

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
