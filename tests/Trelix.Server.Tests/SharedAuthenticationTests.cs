using System.Diagnostics;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Trelix.Server.Infrastructure;
using Trelix.Server.Infrastructure.Authentication;
using Trelix.Server.Persistence;
using Trelix.Server.Persistence.Entities;

namespace Trelix.Server.Tests;

/// <summary>验证共享身份、令牌生命周期及错误响应规则的可观察行为。</summary>
public sealed class SharedAuthenticationTests
{
    /// <summary>收集测试请求实际执行的 SQLite 读取语句。</summary>
    private sealed class ReadCommandCapture : DbCommandInterceptor
    {
        internal ConcurrentQueue<string> Commands { get; } = new();

        /// <inheritdoc />
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Commands.Enqueue(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    /// <summary>在混合身份下只使用应用身份，并保持无效或重复身份的处理方式。</summary>
    /// <returns>表示授权验证完成的任务。</returns>
    [Fact]
    public async Task ApplicationAuthorizationSelectsTheApplicationIdentity()
    {
        using var data = new TestDataDirectory();
        await using var app = new ServerFactory(data);
        using var client = app.NewClient();
        var resources = await app.SeedResourcesAsync();
        var resource = new ApplicationResource(resources.First.Id, resources.FirstEnv.Id);
        var token = new ApplicationToken
        {
            Name = "test", SecretHash = ApplicationTokenSecret.Hash(ApplicationTokenSecret.Create()),
            CreatedAt = app.Clock.GetUtcNow(), ExpiresAt = app.Clock.GetUtcNow().AddHours(1),
            Scopes = [new TokenScope { EnvironmentId = resources.FirstEnv.Id }]
        };
        await app.WithDbAsync(async db =>
        {
            db.ApplicationTokens.Add(token);
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        await using var scope = app.Services.CreateAsyncScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var admin = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "Cookie");
        var invalid = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "invalid")], AuthenticationConstants.ApplicationScheme);
        var missing = new ClaimsIdentity([], AuthenticationConstants.ApplicationScheme);
        var valid = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, token.Id.ToString())], AuthenticationConstants.ApplicationScheme);
        Assert.True((await authorization.AuthorizeAsync(new ClaimsPrincipal([admin, valid]), resource, new ApplicationScopeRequirement())).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(new ClaimsPrincipal(admin), resource, new ApplicationScopeRequirement())).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(new ClaimsPrincipal([admin, invalid]), resource, new ApplicationScopeRequirement())).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(new ClaimsPrincipal([admin, missing]), resource, new ApplicationScopeRequirement())).Succeeded);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await authorization.AuthorizeAsync(new ClaimsPrincipal([invalid, missing]), resource, new ApplicationScopeRequirement()));
    }

    /// <summary>令牌在到期瞬间立即失效，读取和监听入口都拒绝它。</summary>
    /// <returns>表示真实认证验证完成的任务。</returns>
    [Fact]
    public async Task ApplicationTokenExpiresAtItsExactBoundary()
    {
        await using var fixture = await ConfigurationApiFixture.CreateAsync();
        await fixture.SaveAsync("{}");
        await fixture.PublishAsync();
        var issued = await fixture.IssueAsync();
        using var consumer = fixture.Consumer(issued.Secret);
        using var before = await consumer.GetAsync(fixture.ReadPath, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        fixture.App.Clock.Advance(issued.Token.ExpiresAt - fixture.App.Clock.GetUtcNow());
        using var expired = await consumer.GetAsync(fixture.ReadPath, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        var watchPath = fixture.ReadPath.Replace("configuration?", "configuration/watch?", StringComparison.Ordinal)
            + $"&knownConfigFileId={fixture.File.Id}&knownVersion=1";
        using var watch = await consumer.GetAsync(watchPath, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, watch.StatusCode);
    }

    /// <summary>监听身份检查实际查询发布身份，但不会加载配置正文。</summary>
    /// <returns>表示 SQLite 投影验证完成的任务。</returns>
    [Fact]
    public async Task WatchIdentityQueryDoesNotLoadJson()
    {
        var capture = new ReadCommandCapture();
        await using var fixture = await ConfigurationApiFixture.CreateAsync(services =>
            services.AddDbContext<TrelixDbContext>((_, options) => options.AddInterceptors(capture)));
        await fixture.SaveAsync("{\"key\":\"value\"}");
        await fixture.PublishAsync();
        using var consumer = fixture.Consumer((await fixture.IssueAsync()).Secret);
        capture.Commands.Clear();
        var path = fixture.ReadPath.Replace("configuration?", "configuration/watch?", StringComparison.Ordinal)
            + $"&knownConfigFileId={fixture.File.Id}&knownVersion=2";
        using var response = await consumer.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var query = Assert.Single(capture.Commands.Where(command => command.Contains("JOIN \"Releases\"", StringComparison.Ordinal)));
        Assert.DoesNotContain("\"Json\"", query, StringComparison.Ordinal);
    }

    /// <summary>已指定的错误码与追踪标识不被默认补全覆盖。</summary>
    [Fact]
    public void ProblemDetailsPreserveExplicitExtensions()
    {
        var http = new DefaultHttpContext { TraceIdentifier = "request-id" };
        var problem = new ProblemDetails { Status = 401, Extensions = { ["code"] = "specific", ["traceId"] = "existing" } };
        ApiErrors.Customize(new ProblemDetailsContext { HttpContext = http, ProblemDetails = problem });
        Assert.Equal("specific", problem.Extensions["code"]);
        Assert.Equal("existing", problem.Extensions["traceId"]);
        var result = ApiErrors.Result(http, 401, "explicit", "safe");
        Assert.Equal("explicit", result.ProblemDetails.Extensions["code"]);
        Assert.Equal(Activity.Current?.Id ?? "request-id", result.ProblemDetails.Extensions["traceId"]);
    }
}
