using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Trelix.Server.Features.ConfigFiles;
using Trelix.Server.Features.Projects;
using static Trelix.Server.Tests.ConfigurationApiFixture;

namespace Trelix.Server.Tests;

/// <summary>通过实际管理接口和 SQLite 验证共同分页行为。</summary>
public sealed class PaginationTests
{
    /// <summary>管理 OpenAPI 保留分页查询参数和令牌列表的既有 schema 名称。</summary>
    /// <returns>文档验证任务。</returns>
    [Fact]
    public async Task OpenApiPreservesPaginationParametersAndTokenSchema()
    {
        using var data = new TestDataDirectory();
        await using var app = new ServerFactory(data);
        using var client = app.NewClient();
        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/admin.json", TestContext.Current.CancellationToken);
        var paths = document.GetProperty("paths");
        foreach (var path in new[]
        {
            "/api/admin/projects", "/api/admin/projects/{projectId}/environments",
            "/api/admin/projects/{projectId}/environments/{environmentId}/files",
            "/api/admin/projects/{projectId}/environments/{environmentId}/files/{fileId}/releases",
            "/api/admin/application-tokens"
        })
        {
            var operation = paths.EnumerateObject().Single(x => x.Name.TrimEnd('/') == path).Value.GetProperty("get");
            var parameters = operation.GetProperty("parameters").EnumerateArray().ToArray();
            foreach (var (name, defaultValue, maximum) in new[] { ("page", 1, 1_000_000), ("pageSize", 50, 100) })
            {
                var parameter = Assert.Single(parameters, x => x.GetProperty("name").GetString() == name);
                Assert.Equal("query", parameter.GetProperty("in").GetString());
                var schema = parameter.GetProperty("schema");
                Assert.Equal(defaultValue, schema.GetProperty("default").GetInt32());
                Assert.Equal(1, schema.GetProperty("minimum").GetInt32());
                Assert.Equal(maximum, schema.GetProperty("maximum").GetInt32());
            }
        }
        var tokenPage = document.GetProperty("components").GetProperty("schemas").GetProperty("ApplicationTokenListResponse");
        Assert.Equal("按创建时间倒序返回的有界令牌列表。", tokenPage.GetProperty("description").GetString());
        var properties = tokenPage.GetProperty("properties");
        Assert.Equal("当前页的令牌元数据。", properties.GetProperty("items").GetProperty("description").GetString());
        Assert.Equal("从 1 开始的页码。", properties.GetProperty("page").GetProperty("description").GetString());
        Assert.Equal("每页返回的最大条目数。", properties.GetProperty("pageSize").GetProperty("description").GetString());
        var pages = document.GetProperty("components").GetProperty("schemas").EnumerateObject()
            .Where(schema => schema.Name.StartsWith("PageResponse", StringComparison.Ordinal)).ToArray();
        Assert.Equal(4, pages.Length);
        foreach (var page in pages)
        {
            Assert.Equal("有界列表响应；条目数量等于页大小时可请求下一页。", page.Value.GetProperty("description").GetString());
            var fields = page.Value.GetProperty("properties");
            Assert.Equal("当前页条目。", fields.GetProperty("items").GetProperty("description").GetString());
            Assert.Equal("从 1 开始的页码。", fields.GetProperty("page").GetProperty("description").GetString());
            Assert.Equal("每页最大条目数。", fields.GetProperty("pageSize").GetProperty("description").GetString());
        }
    }

    /// <summary>五类列表保持查询参数、分页、排序、数据隔离与数据库侧限制。</summary>
    /// <returns>测试任务。</returns>
    [Fact]
    public async Task ListsKeepPagingContractsAndLimitQueriesInSqlite()
    {
        await using var fixture = await CreateAsync();
        var projectPath = $"/api/admin/projects/{fixture.Project.Id}";
        await SendAsync<ProjectResponse>(fixture.Admin, HttpMethod.Post, "/api/admin/projects", new { key = "zproject", displayName = "Second" }, HttpStatusCode.Created);
        await SendAsync<EnvironmentResponse>(fixture.Admin, HttpMethod.Post, projectPath + "/environments", new { key = "zenv", displayName = "Second" }, HttpStatusCode.Created);
        await SendAsync<ConfigFileResponse>(fixture.Admin, HttpMethod.Post, fixture.EnvironmentPath + "/files", new { name = "z.json" }, HttpStatusCode.Created);
        await fixture.SaveAsync("{\"secret\":\"draft\"}");
        await fixture.PublishAsync();
        await fixture.SaveAsync("{\"secret\":\"published\"}");
        await fixture.PublishAsync();
        var token1 = await fixture.IssueAsync();
        var token2 = await fixture.IssueAsync();

        var lists = new (string Path, string Table, string Field, string First, string Second)[]
        {
            ("/api/admin/projects", "Projects", "key", "project", "zproject"),
            (projectPath + "/environments", "Environments", "key", "env", "zenv"),
            (fixture.EnvironmentPath + "/files", "ConfigFiles", "name", "app.json", "z.json"),
            (fixture.FilePath + "/releases", "Releases", "version", "2", "1"),
            ("/api/admin/application-tokens", "ApplicationTokens", "id", token1.Token.Id.ToString(), token2.Token.Id.ToString())
        };

        foreach (var (path, table, field, first, second) in lists)
        {
            fixture.App.Logs.Entries.Clear();
            var defaultPage = await ReadPageAsync(fixture.Admin, path);
            Assert.Equal(1, defaultPage.GetProperty("page").GetInt32());
            Assert.Equal(50, defaultPage.GetProperty("pageSize").GetInt32());
            Assert.Equal(2, defaultPage.GetProperty("items").GetArrayLength());
            Assert.Equal(new[] { "items", "page", "pageSize" }, defaultPage.EnumerateObject().Select(x => x.Name).Order(StringComparer.Ordinal).ToArray());
            var query = Assert.Single(fixture.App.Logs.Entries.Where(x => x.Contains("LIMIT", StringComparison.OrdinalIgnoreCase)
                && x.Contains($"\"{table}\"", StringComparison.Ordinal)));
            Assert.Contains("OFFSET", query, StringComparison.OrdinalIgnoreCase);
            if (table is "ConfigFiles" or "Releases")
                Assert.DoesNotContain("Json", query, StringComparison.OrdinalIgnoreCase);
            var one = await ReadPageAsync(fixture.Admin, path + "?page=1&pageSize=1");
            var two = await ReadPageAsync(fixture.Admin, path + "?page=2&pageSize=1");
            var values = new[] { one.GetProperty("items")[0].GetProperty(field).ToString(), two.GetProperty("items")[0].GetProperty(field).ToString() };
            var expected = table == "ApplicationTokens" ? new[] { first, second }.Order(StringComparer.Ordinal).ToArray() : new[] { first, second };
            Assert.Equal(expected, values);
            Assert.Empty((await ReadPageAsync(fixture.Admin, path + "?page=1000000&pageSize=100")).GetProperty("items").EnumerateArray());
            foreach (var invalid in new[] { "page=0", "page=1000001", "pageSize=0", "pageSize=101", "page=invalid" })
            {
                using var response = await fixture.Admin.GetAsync(path + "?" + invalid, TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }
            if (table == "ApplicationTokens")
            {
                Assert.DoesNotContain(token1.Secret, defaultPage.ToString());
                Assert.DoesNotContain(token2.Secret, defaultPage.ToString());
                Assert.All(defaultPage.GetProperty("items").EnumerateArray(), item => Assert.Single(item.GetProperty("scopes").EnumerateArray()));
            }
        }

        foreach (var missing in new[]
        {
            $"/api/admin/projects/{Guid.NewGuid()}/environments",
            $"/api/admin/projects/{fixture.Project.Id}/environments/{Guid.NewGuid()}/files",
            fixture.EnvironmentPath + $"/files/{Guid.NewGuid()}/releases"
        })
        {
            using var response = await fixture.Admin.GetAsync(missing, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    /// <summary>读取分页接口的 JSON 对象并验证成功状态。</summary>
    /// <param name="client">已登录管理员客户端。</param>
    /// <param name="path">分页列表路径。</param>
    /// <returns>已解析的分页响应。</returns>
    private static async Task<JsonElement> ReadPageAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);
    }
}
