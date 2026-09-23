using Microsoft.AspNetCore.Antiforgery;

namespace Trelix.Server.Infrastructure.Authentication;

/// <summary>在请求正文绑定之前校验管理写请求的防伪造令牌，覆盖匿名登录。</summary>
/// <param name="next">下一请求处理委托。</param>
public sealed class ManagementAntiforgeryMiddleware(RequestDelegate next)
{
    /// <summary>校验管理写请求；失败时返回安全的 Problem Details。</summary>
    /// <param name="context">当前请求。</param>
    /// <param name="antiforgery">防伪造服务。</param>
    /// <returns>请求处理任务。</returns>
    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery)
    {
        var request = context.Request;
        if (request.Path.StartsWithSegments("/api/admin") && !HttpMethods.IsGet(request.Method)
            && !HttpMethods.IsHead(request.Method) && !HttpMethods.IsOptions(request.Method))
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                await ApiErrors.Result(context, 400, "antiforgery_failed", "防伪造令牌缺失或无效，请刷新后重试。").ExecuteAsync(context);
                return;
            }
        }
        await next(context);
    }
}
