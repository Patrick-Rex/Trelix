using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Trelix.Server.Features.Authentication;
using Trelix.Server.Infrastructure.Authentication;
using Trelix.Server.Persistence.Entities;

namespace Trelix.Server.Tests;

/// <summary>验证管理员初始化、Cookie 会话、防伪造与 API 信息边界。</summary>
public sealed class AuthenticationTests
{
    /// <summary>验证首次启动只创建一个管理员且密码以哈希形式保存。</summary>
    /// <returns>表示测试场景执行完成的任务。</returns>
    [Fact]
    public async Task InitializesOneAdministratorWithPasswordHash()
    {
        using var data = new TestDataDirectory();
        await using var app = new ServerFactory(data);
        var admin = await app.WithDbAsync(db => db.Administrators.SingleAsync());
        Assert.True(admin.PasswordHash != app.Password);
        Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<Administrator>().VerifyHashedPassword(admin, admin.PasswordHash, app.Password));
        Assert.Equal(1, await app.WithDbAsync(db => db.Administrators.CountAsync()));
    }

    /// <summary>验证缺失或不符合约定的初始化凭证使启动失败。</summary>
    /// <param name="username">管理员账号名。</param>
    /// <param name="password">待校验的初始化密码。</param>
    /// <returns>表示测试场景执行完成的任务。</returns>
    [Theory]
    [InlineData("", "")]
    [InlineData("administrator", "")]
    [InlineData("administrator", "short")]
    [InlineData(" administrator", "long-test-input-only")]
    public async Task InvalidBootstrapPreventsStartup(string username, string password)
    {
        using var data = new TestDataDirectory();
        await using var app = new ServerFactory(data) { Username = username, Password = password };
        var failure = Assert.ThrowsAny<Exception>(() => app.NewClient());
        Assert.Contains("Trelix:Administrator", failure.ToString());
    }

    /// <summary>验证登录需要防伪造令牌，认证失败不会泄露账号密码。</summary>
    /// <returns>表示测试场景执行完成的任务。</returns>
    [Fact]
    public async Task LoginRequiresCsrfAndDoesNotRevealCredentials()
    {
        using var data = new TestDataDirectory();
        await using var app = new ServerFactory(data);
        using var client = app.NewClient();
        var request = new LoginRequest { Username = app.Username, Password = app.Password };
        using var anonymous = await client.GetAsync("/api/admin/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Null(anonymous.Headers.Location);
        Assert.Equal("application/problem+json", anonymous.Content.Headers.ContentType?.MediaType);
        using var missing = await client.PostAsJsonAsync("/api/admin/auth/login", request);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        await ServerFactory.RefreshCsrfAsync(client);
        using var wrong = await client.PostAsJsonAsync("/api/admin/auth/login", request with { Password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) });
        using var wrongName = await client.PostAsJsonAsync("/api/admin/auth/login", request with { Username = "unknown" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongName.StatusCode);
        var body = await wrong.Content.ReadAsStringAsync();
        Assert.True(!body.Contains(app.Password) && !body.Contains(app.Username));
        Assert.True(!string.Join('\n', app.Logs.Entries).Contains(app.Password));
    }

    /// <summary>验证生产 Cookie 标记、固定有效期和退出后的旧 Cookie 重放拒绝。</summary>
    /// <returns>表示测试场景执行完成的任务。</returns>
    [Fact]
    public async Task CookieFlagsLifetimeAndLogoutRejectReplay()
    {
        using var data = new TestDataDirectory();
        await using var app = new ServerFactory(data) { EnvironmentName = "Production" };
        using var client = app.NewClient();
        await ServerFactory.RefreshCsrfAsync(client);
        using var login = await client.PostAsJsonAsync("/api/admin/auth/login", new LoginRequest { Username = app.Username, Password = app.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var setCookie = login.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("Trelix.Admin="));
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expires=", setCookie, StringComparison.OrdinalIgnoreCase);
        var session = await login.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.NotNull(session);
        Assert.Equal(app.Clock.GetUtcNow() + TimeSpan.FromHours(8), session.ExpiresAt);

        // The anonymous request token is bound to the previous identity.
        using var staleCsrf = await client.PostAsync("/api/admin/auth/logout", null);
        Assert.Equal(HttpStatusCode.BadRequest, staleCsrf.StatusCode);
        await ServerFactory.RefreshCsrfAsync(client);
        using var logout = await client.PostAsync("/api/admin/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using var replay = app.NewClient(false);
        replay.DefaultRequestHeaders.Add("Cookie", setCookie.Split(';')[0]);
        using var replayResponse = await replay.GetAsync("/api/admin/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, replayResponse.StatusCode);
        Assert.Equal(0, await app.WithDbAsync(db => db.AdministratorSessions.CountAsync()));
    }

    /// <summary>验证会话不会自动续期且退出一个会话不影响另一个会话。</summary>
    /// <returns>表示测试场景执行完成的任务。</returns>
    [Fact]
    public async Task FixedExpiryDoesNotSlideAndOtherSessionSurvivesLogout()
    {
        using var data = new TestDataDirectory();
        await using var app = new ServerFactory(data);
        using var first = app.NewClient();
        using var second = app.NewClient();
        await app.LoginAsync(first);
        await app.LoginAsync(second);
        using var logout = await first.PostAsync("/api/admin/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        app.Clock.Advance(TimeSpan.FromHours(7));
        using var valid = await second.GetAsync("/api/admin/auth/session");
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
        Assert.False(valid.Headers.Contains("Set-Cookie"));
        app.Clock.Advance(TimeSpan.FromHours(1));
        using var expired = await second.GetAsync("/api/admin/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
    }

    /// <summary>验证管理员安全戳变化后已有会话立即失效。</summary>
    /// <returns>表示测试场景执行完成的任务。</returns>
    [Fact]
    public async Task SecurityStampChangeInvalidatesSession()
    {
        using var data = new TestDataDirectory();
        await using var app = new ServerFactory(data);
        using var client = app.NewClient();
        await app.LoginAsync(client);
        await app.WithDbAsync(async db =>
        {
            (await db.Administrators.SingleAsync()).SecurityStamp = Guid.NewGuid();
            return await db.SaveChangesAsync();
        });
        using var response = await client.GetAsync("/api/admin/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>验证同一来源超过登录频率限制后返回 429。</summary>
    /// <returns>表示测试场景执行完成的任务。</returns>
    [Fact]
    public async Task LoginRateLimitReturns429()
    {
        using var data = new TestDataDirectory();
        await using var app = new ServerFactory(data);
        using var client = app.NewClient();
        await ServerFactory.RefreshCsrfAsync(client);
        for (var i = 0; i < 10; i++)
        {
            using var attempt = await client.PostAsJsonAsync("/api/admin/auth/login", new { username = "unknown", password = "invalid" });
            Assert.Equal(HttpStatusCode.Unauthorized, attempt.StatusCode);
        }
        using var limited = await client.PostAsJsonAsync("/api/admin/auth/login", new { username = "unknown", password = "invalid" });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("application/problem+json", limited.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>验证开发健康检查、管理 OpenAPI 分组及安全错误响应。</summary>
    /// <returns>表示测试场景执行完成的任务。</returns>
    [Fact]
    public async Task ApiErrorsOpenApiAndHealthPreserveTheirBoundaries()
    {
        using var data = new TestDataDirectory();
        await using var app = new ServerFactory(data);
        using var client = app.NewClient();
        using var health = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        using var unknown = await client.GetAsync("/api/unknown");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("application/problem+json", unknown.Content.Headers.ContentType?.MediaType);
        var schema = await client.GetFromJsonAsync<JsonElement>("/openapi/admin.json");
        Assert.Equal(8, schema.GetProperty("paths").EnumerateObject().Count());
        Assert.True(schema.GetProperty("paths").TryGetProperty("/api/admin/application-tokens/{id}/rotate", out _));
        await app.LoginAsync(client);
        using var failed = await client.GetAsync("/api/admin/__tests/failure");
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        var body = await failed.Content.ReadAsStringAsync();
        Assert.True(!body.Contains("sensitive-internal-body"));
        Assert.True(!string.Join('\n', app.Logs.Entries).Contains("sensitive-internal-body"));
        Assert.Contains("traceId", body);
    }
}
