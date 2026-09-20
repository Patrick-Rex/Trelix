using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Trelix.Server.Features.ApplicationTokens;
using Trelix.Server.Infrastructure.Authentication;

namespace Trelix.Server.Tests;

public sealed class ApplicationTokenTests
{
    private const string TokensPath = "/api/admin/application-tokens";

    private static async Task<IssuedApplicationTokenResponse> IssueAsync(ServerFactory app, HttpClient admin, params TokenScopeRequest[] scopes)
    {
        using var response = await admin.PostAsJsonAsync(TokensPath, new CreateApplicationTokenRequest
        {
            Name = "integration-consumer", ExpiresAt = app.Clock.GetUtcNow().AddHours(1), Scopes = scopes
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.True(response.Headers.CacheControl?.NoStore);
        return (await response.Content.ReadFromJsonAsync<IssuedApplicationTokenResponse>())!;
    }

    private static async Task<HttpStatusCode> ProbeAsync(HttpClient client, string? secret, Guid projectId, Guid environmentId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/__tests/application/{projectId}/{environmentId}");
        if (secret is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    [Fact]
    public async Task MultipleScopesAreValidatedAndOnlyDigestIsStored()
    {
        using var data = new TestDataDirectory();
        await using var app = new ServerFactory(data);
        using var admin = app.NewClient();
        await app.LoginAsync(admin);
        var resources = await app.SeedResourcesAsync();
        var issued = await IssueAsync(app, admin,
            new TokenScopeRequest(resources.First.Id, resources.FirstEnv.Id),
            new TokenScopeRequest(resources.Second.Id, resources.SecondEnv.Id));
        Assert.Equal(2, issued.Token.Scopes.Count);
        Assert.True(ApplicationTokenSecret.HasValidFormat(issued.Secret));
        var hash = await app.WithDbAsync(db => db.ApplicationTokens.Select(x => x.SecretHash).SingleAsync());
        Assert.True(hash == ApplicationTokenSecret.Hash(issued.Secret));
        using var get = await admin.GetAsync($"{TokensPath}/{issued.Token.Id}");
        using var list = await admin.GetAsync(TokensPath);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var responseBodies = await get.Content.ReadAsStringAsync() + await list.Content.ReadAsStringAsync();
        Assert.True(!responseBodies.Contains(issued.Secret) && !responseBodies.Contains(hash));
        Assert.Single((await list.Content.ReadFromJsonAsync<ApplicationTokenListResponse>())!.Items);
        using var consumer = app.NewClient(false);
        Assert.Equal(HttpStatusCode.NoContent, await ProbeAsync(consumer, issued.Secret, resources.First.Id, resources.FirstEnv.Id));
        Assert.Equal(HttpStatusCode.NoContent, await ProbeAsync(consumer, issued.Secret, resources.Second.Id, resources.SecondEnv.Id));
        Assert.Equal(HttpStatusCode.Forbidden, await ProbeAsync(consumer, issued.Secret, resources.First.Id, resources.OtherEnv.Id));
        Assert.Equal(HttpStatusCode.Forbidden, await ProbeAsync(consumer, issued.Secret, resources.Second.Id, resources.FirstEnv.Id));
        Assert.Equal(HttpStatusCode.Forbidden, await ProbeAsync(consumer, issued.Secret, Guid.NewGuid(), Guid.NewGuid()));
        var log = string.Join('\n', app.Logs.Entries);
        Assert.True(!log.Contains(issued.Secret) && !log.Contains(app.Password) && !log.Contains(hash));
    }

    [Theory]
    [InlineData("past")]
    [InlineData("empty-name")]
    [InlineData("empty-scopes")]
    [InlineData("duplicate")]
    [InlineData("wrong-project")]
    [InlineData("missing-environment")]
    [InlineData("null-scope")]
    public async Task RejectsInvalidTokenRequests(string scenario)
    {
        using var data = new TestDataDirectory();
        await using var app = new ServerFactory(data);
        using var admin = app.NewClient();
        await app.LoginAsync(admin);
        var resources = await app.SeedResourcesAsync();
        var scope = new TokenScopeRequest(resources.First.Id, resources.FirstEnv.Id);
        var request = new CreateApplicationTokenRequest { Name = "consumer", ExpiresAt = app.Clock.GetUtcNow().AddHours(1), Scopes = [scope] };
        request = scenario switch
        {
            "past" => request with { ExpiresAt = app.Clock.GetUtcNow() },
            "empty-name" => request with { Name = "   " },
            "empty-scopes" => request with { Scopes = [] },
            "duplicate" => request with { Scopes = [scope, scope] },
            "wrong-project" => request with { Scopes = [scope with { ProjectId = resources.Second.Id }] },
            "missing-environment" => request with { Scopes = [scope with { EnvironmentId = Guid.NewGuid() }] },
            "null-scope" => request with { Scopes = [null!] },
            _ => request
        };
        using var response = await admin.PostAsJsonAsync(TokensPath, request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(0, await app.WithDbAsync(db => db.ApplicationTokens.CountAsync()));
    }

    [Fact]
    public async Task ExpiryIsRequiredAndListIsBounded()
    {
        using var data = new TestDataDirectory();
        await using var app = new ServerFactory(data);
        using var admin = app.NewClient();
        await app.LoginAsync(admin);
        using var missing = await admin.PostAsJsonAsync(TokensPath, new { name = "consumer", scopes = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        using var invalidPage = await admin.GetAsync(TokensPath + "?pageSize=101");
        Assert.Equal(HttpStatusCode.BadRequest, invalidPage.StatusCode);
        using var missingToken = await admin.GetAsync($"{TokensPath}/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missingToken.StatusCode);
    }

    [Fact]
    public async Task SchemesAreIsolatedAndManagementWritesRequireCsrf()
    {
        using var data = new TestDataDirectory();
        await using var app = new ServerFactory(data);
        using var admin = app.NewClient();
        await app.LoginAsync(admin);
        var resources = await app.SeedResourcesAsync();
        var issued = await IssueAsync(app, admin, new TokenScopeRequest(resources.First.Id, resources.FirstEnv.Id));
        Assert.Equal(HttpStatusCode.Unauthorized, await ProbeAsync(admin, null, resources.First.Id, resources.FirstEnv.Id));
        using var consumer = app.NewClient(false);
        consumer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", issued.Secret);
        using var managementRead = await consumer.GetAsync(TokensPath);
        using var managementWrite = await consumer.PostAsync($"{TokensPath}/{issued.Token.Id}/revoke", null);
        Assert.Equal(HttpStatusCode.Unauthorized, managementRead.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, managementWrite.StatusCode);
        admin.DefaultRequestHeaders.Remove(AuthenticationConstants.CsrfHeader);
        using var missing = await admin.PostAsync($"{TokensPath}/{issued.Token.Id}/revoke", null);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        admin.DefaultRequestHeaders.Add(AuthenticationConstants.CsrfHeader, "invalid");
        using var invalid = await admin.PostAsync($"{TokensPath}/{issued.Token.Id}/revoke", null);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, await ProbeAsync(consumer, issued.Secret, resources.First.Id, resources.FirstEnv.Id));
    }

    [Fact]
    public async Task MissingInvalidAndExpiredCredentialsReturn401()
    {
        using var data = new TestDataDirectory();
        await using var app = new ServerFactory(data);
        using var admin = app.NewClient();
        await app.LoginAsync(admin);
        var resources = await app.SeedResourcesAsync();
        var issued = await IssueAsync(app, admin, new TokenScopeRequest(resources.First.Id, resources.FirstEnv.Id));
        using var consumer = app.NewClient(false);
        foreach (var secret in new[] { null, "invalid", ApplicationTokenSecret.Create() })
            Assert.Equal(HttpStatusCode.Unauthorized, await ProbeAsync(consumer, secret, resources.First.Id, resources.FirstEnv.Id));
        app.Clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal(HttpStatusCode.Unauthorized, await ProbeAsync(consumer, issued.Secret, resources.First.Id, resources.FirstEnv.Id));
    }

    [Fact]
    public async Task RotationImmediatelyRevokesOldSecretAndPreservesScopes()
    {
        using var data = new TestDataDirectory();
        await using var app = new ServerFactory(data);
        using var admin = app.NewClient();
        await app.LoginAsync(admin);
        var resources = await app.SeedResourcesAsync();
        var original = await IssueAsync(app, admin, new TokenScopeRequest(resources.First.Id, resources.FirstEnv.Id),
            new TokenScopeRequest(resources.Second.Id, resources.SecondEnv.Id));
        using var rotation = await admin.PostAsJsonAsync($"{TokensPath}/{original.Token.Id}/rotate",
            new RotateApplicationTokenRequest { ExpiresAt = app.Clock.GetUtcNow().AddDays(1) });
        Assert.Equal(HttpStatusCode.Created, rotation.StatusCode);
        var replacement = (await rotation.Content.ReadFromJsonAsync<IssuedApplicationTokenResponse>())!;
        Assert.NotEqual(original.Token.Id, replacement.Token.Id);
        Assert.True(original.Secret != replacement.Secret);
        Assert.Equal(original.Token.Scopes, replacement.Token.Scopes);
        using var consumer = app.NewClient(false);
        Assert.Equal(HttpStatusCode.Unauthorized, await ProbeAsync(consumer, original.Secret, resources.First.Id, resources.FirstEnv.Id));
        Assert.Equal(HttpStatusCode.NoContent, await ProbeAsync(consumer, replacement.Secret, resources.First.Id, resources.FirstEnv.Id));
        using var repeat = await admin.PostAsJsonAsync($"{TokensPath}/{original.Token.Id}/rotate", new { expiresAt = app.Clock.GetUtcNow().AddDays(1) });
        Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode);
        using var revoke = await admin.PostAsync($"{TokensPath}/{replacement.Token.Id}/revoke", null);
        using var repeatedRevoke = await admin.PostAsync($"{TokensPath}/{replacement.Token.Id}/revoke", null);
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, repeatedRevoke.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, await ProbeAsync(consumer, replacement.Secret, resources.First.Id, resources.FirstEnv.Id));
    }

    [Fact]
    public async Task ConcurrentRotationCreatesExactlyOneReplacement()
    {
        using var data = new TestDataDirectory();
        await using var app = new ServerFactory(data);
        using var admin = app.NewClient();
        await app.LoginAsync(admin);
        var resources = await app.SeedResourcesAsync();
        var original = await IssueAsync(app, admin, new TokenScopeRequest(resources.First.Id, resources.FirstEnv.Id));
        var requests = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => admin.PostAsJsonAsync(
            $"{TokensPath}/{original.Token.Id}/rotate", new { expiresAt = app.Clock.GetUtcNow().AddDays(1) })));
        try
        {
            Assert.Single(requests, response => response.StatusCode == HttpStatusCode.Created);
            Assert.Single(requests, response => response.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal(2, await app.WithDbAsync(db => db.ApplicationTokens.CountAsync()));
            Assert.Equal(1, await app.WithDbAsync(db => db.ApplicationTokens.CountAsync(x => x.RevokedAt == null)));
        }
        finally
        {
            foreach (var response in requests)
                response.Dispose();
        }
    }
}
