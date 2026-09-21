using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Trelix.Server.Infrastructure.Authentication;

/// <summary>对管理 API 的非安全 HTTP 方法执行防伪造校验，覆盖登录请求。</summary>
/// <param name="antiforgery">ASP.NET Core 防伪造服务。</param>
public sealed class ManagementAntiforgeryFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    /// <summary>跳过非管理路径及安全方法；防伪造校验失败时返回 400。</summary>
    /// <param name="context">当前 MVC 授权过滤器上下文。</param>
    /// <returns>表示过滤器处理完成的任务。</returns>
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
