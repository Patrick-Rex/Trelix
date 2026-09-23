using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Trelix.SampleApp;
using Trelix.Server.Features.ApplicationTokens;
using Trelix.Server.Features.Authentication;
using Trelix.Server.Features.ConfigFiles;
using Trelix.Server.Features.Projects;
using Trelix.Server.Features.Releases;
using Trelix.Server.Infrastructure.Authentication;
using Trelix.Server.Tests;
using static Trelix.Server.Tests.ConfigurationApiFixture;

namespace Trelix.Extensions.Configuration.Tests;

/// <summary>运行真实 AppHost 和其业务进程，验证示例的可选编排及连接参数注入。</summary>
public sealed class AppHostTests
{
    /// <summary>默认编排不依赖已经发布的业务配置。</summary>
    /// <returns>模型检查任务。</returns>
    [Fact]
    public async Task SampleIsDisabledByDefault()
    {
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Trelix_AppHost>(
            ["Trelix:Sample:Enabled=false"], TestContext.Current.CancellationToken);
        Assert.DoesNotContain(builder.Resources, resource => resource.Name == "trelix-sample");
    }

    /// <summary>用随机凭证和独立 SQLite 启动 Server，发布后再启动示例验证真实进程接入。</summary>
    /// <returns>编排验证任务。</returns>
    [Fact]
    public async Task EnabledSampleConsumesInjectedConnection()
    {
        using var data = new TestDataDirectory();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        var ct = deadline.Token;
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Trelix_AppHost>([
            "Trelix:Sample:Enabled=true", "Trelix:Sample:ProjectKey=aspire-test", "Trelix:Sample:EnvironmentKey=test",
            "Trelix:Sample:FileName=sample.json", "Trelix:Sample:AccessToken=pending-test-token"
        ], ct);
        builder.CreateResourceBuilder<ProjectResource>("trelix-server")
            .WithEnvironment("Trelix__Storage__DataDirectory", data.Path)
            .WithEnvironment("Trelix__Administrator__Username", "integration-admin")
            .WithEnvironment("Trelix__Administrator__Password", password);
        // 本测试只启动被测业务进程，避免对现有前端开发进程重复执行 npm ci。
        builder.CreateResourceBuilder<ExecutableResource>("trelix-client").WithExplicitStart();
        builder.CreateResourceBuilder<ExecutableResource>("trelix-client-installer").WithExplicitStart();
        var issuedToken = "";
        builder.CreateResourceBuilder<ProjectResource>("trelix-sample").WithExplicitStart()
            .WithEnvironment(context => context.EnvironmentVariables["Trelix__AccessToken"] = issuedToken)
            .WithHttpHealthCheck("/configuration");
        await using var app = await builder.BuildAsync(ct);
        await app.StartAsync(ct);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("trelix-server", ct);
        using var admin = app.CreateHttpClient("trelix-server", "https");
        var csrf = await admin.GetFromJsonAsync<AntiforgeryResponse>("/api/admin/auth/antiforgery", ct);
        admin.DefaultRequestHeaders.Add(AuthenticationConstants.CsrfHeader, csrf!.RequestToken);
        using var login = await admin.PostAsJsonAsync("/api/admin/auth/login", new { username = "integration-admin", password }, ct);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        await ServerFactory.RefreshCsrfAsync(admin);
        var project = await SendAsync<ProjectResponse>(admin, HttpMethod.Post, "/api/admin/projects", new { key = "aspire-test", displayName = "Aspire" }, HttpStatusCode.Created);
        var environment = await SendAsync<EnvironmentResponse>(admin, HttpMethod.Post, $"/api/admin/projects/{project.Id}/environments",
            new { key = "test", displayName = "Test" }, HttpStatusCode.Created);
        var filesPath = $"/api/admin/projects/{project.Id}/environments/{environment.Id}/files";
        var file = await SendAsync<ConfigFileResponse>(admin, HttpMethod.Post, filesPath, new { name = "sample.json" }, HttpStatusCode.Created);
        var path = filesPath + "/" + file.Id;
        var draft = await SendAsync<DraftResponse>(admin, HttpMethod.Put, path + "/draft",
            new { json = "{\"Sample\":{\"Message\":\"aspire-injected\",\"Enabled\":true}}", file.ConcurrencyStamp });
        await SendAsync<PublicationResponse>(admin, HttpMethod.Post, path + "/releases",
            new { draft.File.DraftRevision, draft.File.ConcurrencyStamp }, HttpStatusCode.Created);
        var token = await SendAsync<IssuedApplicationTokenResponse>(admin, HttpMethod.Post, "/api/admin/application-tokens", new CreateApplicationTokenRequest
        {
            Name = "aspire-test", ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10), Scopes = [new(project.Id, environment.Id)]
        }, HttpStatusCode.Created);
        issuedToken = token.Secret;
        await app.ResourceCommands.ExecuteCommandAsync("trelix-sample", KnownResourceCommands.StartCommand, ct);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("trelix-sample", ct);
        using var consumer = app.CreateHttpClient("trelix-sample", "http");
        var response = await consumer.GetFromJsonAsync<SampleResponse>("/configuration", ct);
        Assert.Equal("aspire-injected", response!.Configuration);
        Assert.Equal(response.Configuration, response.Monitor.Message);
        Assert.Equal(response.Configuration, response.Snapshot.Message);
        await app.StopAsync(ct);
    }
}
