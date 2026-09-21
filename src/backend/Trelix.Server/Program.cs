using Scalar.AspNetCore;
using Trelix.Server.Infrastructure;
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
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

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

app.MapControllers();
// Unknown API routes must never resolve to the SPA document.
app.MapFallback("/api/{**path}", () => Results.NotFound()).AllowAnonymous();
app.MapFallbackToFile("/index.html").AllowAnonymous();
app.Run();

/// <summary>Server 宿主入口，同时供集成测试定位应用程序集。</summary>
public partial class Program;
