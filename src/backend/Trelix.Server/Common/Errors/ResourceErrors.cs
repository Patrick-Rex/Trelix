namespace Trelix.Server.Common.Errors;

/// <summary>提供项目和环境层级共用的安全错误。</summary>
internal static class ResourceErrors
{
    /// <summary>创建资源不存在或父资源不匹配的错误。</summary>
    /// <returns>不包含请求输入的资源缺失错误。</returns>
    internal static ApiOperationException Missing() =>
        new(404, "resource_not_found", "资源不存在或不属于指定父资源。");
}
