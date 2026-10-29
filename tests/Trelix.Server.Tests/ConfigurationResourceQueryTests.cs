using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Trelix.Server.Common.Errors;
using Trelix.Server.Features.ConfigFiles;
using Trelix.Server.Infrastructure.Persistence;

namespace Trelix.Server.Tests;

/// <summary>验证文件列表的父资源检查和 SQLite 查询边界。</summary>
public sealed class ConfigurationResourceQueryTests
{
    /// <summary>收集实际执行的 SQLite 读取语句。</summary>
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

    /// <summary>有效父资源执行两次读取，缺失或错配的父资源保留既有错误。</summary>
    /// <returns>表示 SQLite 查询验证完成的任务。</returns>
    [Fact]
    public async Task FileListChecksParentOnceBeforeQueryingPage()
    {
        var capture = new ReadCommandCapture();
        await using var fixture = await ConfigurationApiFixture.CreateAsync(services =>
            services.AddDbContext<TrelixDbContext>((_, options) => options.AddInterceptors(capture)));
        await using var scope = fixture.App.Services.CreateAsyncScope();
        var files = scope.ServiceProvider.GetRequiredService<ConfigFileService>();

        capture.Commands.Clear();
        var page = await files.ListAsync(fixture.Project.Id, fixture.Environment.Id, 1, 50, TestContext.Current.CancellationToken);
        Assert.Single(page.Items);
        var commands = capture.Commands.ToArray();
        Assert.Equal(2, commands.Length);
        Assert.Contains("EXISTS", commands[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"Environments\"", commands[0], StringComparison.Ordinal);
        Assert.DoesNotContain("\"DisplayName\"", commands[0], StringComparison.Ordinal);
        Assert.Contains("\"ConfigFiles\"", commands[1], StringComparison.Ordinal);
        Assert.Contains("LIMIT", commands[1], StringComparison.OrdinalIgnoreCase);

        foreach (var (projectId, environmentId) in new[]
        {
            (Guid.NewGuid(), fixture.Environment.Id),
            (fixture.Project.Id, Guid.NewGuid())
        })
        {
            capture.Commands.Clear();
            var error = await Assert.ThrowsAsync<ApiOperationException>(() =>
                files.ListAsync(projectId, environmentId, 1, 50, TestContext.Current.CancellationToken));
            Assert.Equal(404, error.Status);
            Assert.Equal("resource_not_found", error.Code);
            Assert.Equal("资源不存在或不属于指定父资源。", error.Message);
            Assert.Single(capture.Commands);

            capture.Commands.Clear();
            var createError = await Assert.ThrowsAsync<ApiOperationException>(() => files.CreateAsync(projectId, environmentId,
                new CreateConfigFileRequest { Name = "new.json", Json = "invalid" }, TestContext.Current.CancellationToken));
            Assert.Equal(404, createError.Status);
            Assert.Equal("resource_not_found", createError.Code);
            Assert.Single(capture.Commands);
        }
    }
}
