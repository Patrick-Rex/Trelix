using Scalar.AspNetCore;
using Trelix.Server.Infrastructure;
using Trelix.Server.Infrastructure.Authentication;
using Trelix.Server.Features.Authentication;
using Trelix.Server.Features.ApplicationTokens;
using Trelix.Server.Features.Projects;
using Trelix.Server.Features.ConfigFiles;
using Trelix.Server.Features.Releases;
using Trelix.Server.Features.Distribution;
using Trelix.Server.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddTrelix(builder.Environment);

var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(app.Lifetime.ApplicationStopping);
}

app.UseExceptionHandler(new ExceptionHandlerOptions { SuppressDiagnosticsCallback = _ => true });
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseRouting();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
        context.Response.Headers.CacheControl = "no-store";
    await next(context);
});
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseMiddleware<ManagementAntiforgeryMiddleware>();

app.MapDefaultEndpoints();
app.MapStaticAssets().AllowAnonymous();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference(options => options
        .WithTitle("Trelix API")
        .AddDocument("admin", "管理 API", isDefault: true)
        .AddDocument("application", "应用 API")
        .DisableDefaultFonts()
        .DisableAgent()).AllowAnonymous();
}

var admin = app.MapGroup("/api/admin").WithGroupName("admin")
    .RequireAuthorization(AuthenticationConstants.AdministratorPolicy)
    .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
admin.MapAuthentication();
admin.MapApplicationTokens();
admin.MapProjects();
admin.MapConfigFiles();
admin.MapReleases();
app.MapDistribution();
// Unknown API routes must never resolve to the SPA document.
app.MapFallback("/api/{**path}", () => Results.NotFound()).AllowAnonymous();
app.MapFallbackToFile("/index.html").AllowAnonymous();
app.Run();

/// <summary>Server 宿主入口，同时供集成测试定位应用程序集。</summary>
public partial class Program;
