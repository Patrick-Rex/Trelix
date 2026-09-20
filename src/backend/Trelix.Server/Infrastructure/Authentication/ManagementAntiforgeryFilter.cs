using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Trelix.Server.Infrastructure.Authentication;

public sealed class ManagementAntiforgeryFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var request = context.HttpContext.Request;
        if (!request.Path.StartsWithSegments("/api/admin") || HttpMethods.IsGet(request.Method)
            || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method))
            return;

        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            context.Result = ApiErrors.Result(context.HttpContext, 400, "antiforgery_failed", "防伪造令牌缺失或无效，请刷新后重试。");
        }
    }
}
