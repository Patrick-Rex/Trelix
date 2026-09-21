using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Trelix.Server.Tests;

/// <summary>验证 Scalar 文档、静态资源及 OpenAPI 的开发环境边界。</summary>
public sealed class OpenApiTests
{
    /// <summary>验证匿名访问 Scalar 可加载两组文档、本地脚本及 XML 契约说明。</summary>
    /// <returns>表示开发环境文档验证完成的任务。</returns>
    [Fact]
    public async Task DevelopmentServesScalarAndBothOpenApiDocuments()
    {
        using var data = new TestDataDirectory();
        await using var app = new ServerFactory(data);
        using var client = app.NewClient();
        using var entry = await client.GetAsync("/scalar");
        Assert.Equal(HttpStatusCode.Found, entry.StatusCode);
        Assert.NotNull(entry.Headers.Location);
        var documentUri = new Uri(entry.RequestMessage!.RequestUri!, entry.Headers.Location);
        Assert.Equal("/scalar/", documentUri.AbsolutePath);
        using var response = await client.GetAsync(documentUri);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("openapi/admin.json", html);
        Assert.Contains("openapi/application.json", html);
        var script = Regex.Match(html, "src=\"([^\"]*scalar[^\"]*\\.js)\"");
        Assert.True(script.Success, "Scalar must reference its bundled JavaScript.");
        var scriptPath = script.Groups[1].Value;
        Assert.Equal("scalar.js", scriptPath);
        using var asset = await client.GetAsync("/scalar/" + scriptPath);
        Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
        Assert.Contains("javascript", asset.Content.Headers.ContentType?.MediaType ?? "");

        using var direct = await client.GetAsync("/scalar/admin");
        Assert.Equal(HttpStatusCode.OK, direct.StatusCode);
        Assert.Contains("openapi/admin.json", await direct.Content.ReadAsStringAsync());

        var admin = await client.GetFromJsonAsync<JsonElement>("/openapi/admin.json");
        Assert.True(admin.GetProperty("paths").TryGetProperty("/api/admin/auth/login", out _));
        var login = admin.GetProperty("components").GetProperty("schemas").GetProperty("LoginRequest");
        Assert.Contains("管理员登录凭证", login.GetProperty("description").GetString());
        var application = await client.GetFromJsonAsync<JsonElement>("/openapi/application.json");
        Assert.Empty(application.GetProperty("paths").EnumerateObject());
    }

    /// <summary>验证非开发环境不注册 Scalar 页面、脚本及 OpenAPI JSON 端点。</summary>
    /// <param name="environmentName">要验证的非开发宿主环境。</param>
    /// <returns>表示环境边界验证完成的任务。</returns>
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task NonDevelopmentDoesNotMapDocumentation(string environmentName)
    {
        using var data = new TestDataDirectory();
        await using var app = new ServerFactory(data) { EnvironmentName = environmentName };
        using var client = app.NewClient();
        var endpoints = app.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>();
        Assert.DoesNotContain(endpoints, endpoint =>
            endpoint.RoutePattern.RawText?.Contains("scalar", StringComparison.OrdinalIgnoreCase) == true
            || endpoint.RoutePattern.RawText?.Contains("openapi", StringComparison.OrdinalIgnoreCase) == true);

        foreach (var path in new[] { "/scalar/admin", "/openapi/admin.json", "/openapi/application.json" })
        {
            using var response = await client.GetAsync(path);
            var content = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("Scalar.createApiReference", content);
            Assert.DoesNotContain("\"openapi\":", content);
        }
    }
}
