using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Trelix.Server.Infrastructure.Middleware;

public sealed class ApiOperationExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var failure = exception switch
        {
            ApiOperationException operation => operation,
            DbUpdateConcurrencyException => new ApiOperationException(409, "concurrent_change", "资源已被其他操作修改，请刷新后重试。"),
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
