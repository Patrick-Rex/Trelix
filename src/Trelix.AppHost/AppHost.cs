using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

var server = builder.AddProject<Projects.Trelix_Server>("trelix-server", launchProfileName: "https")
    .WithHttpHealthCheck("/health", endpointName: "https");

builder.AddViteApp("trelix-client", "../frontend")
    .WithNpm(installCommand: "ci")
    .WithEndpoint("http", endpoint =>
    {
        // Vite already uses the ASP.NET Core HTTPS development certificate.
        endpoint.Name = "https";
        endpoint.UriScheme = "https";
    })
    .WithReference(server)
    .WaitFor(server);

if (builder.Configuration.GetValue<bool>("Trelix:Sample:Enabled"))
{
    var connection = builder.Configuration.GetSection("Trelix:Sample");
    foreach (var key in new[] { "ProjectKey", "EnvironmentKey", "FileName", "AccessToken" })
        if (string.IsNullOrWhiteSpace(connection[key]))
            throw new InvalidOperationException($"启用 SDK 示例需要外部配置 Trelix:Sample:{key}。");

    var sampleToken = builder.AddParameter("trelix-sample-token", () => connection["AccessToken"]!, secret: true);
    builder.AddProject<Projects.Trelix_SampleApp>("trelix-sample", launchProfileName: null)
        .WithHttpEndpoint(env: "ASPNETCORE_HTTP_PORTS")
        .WithEnvironment("Trelix__ServerUrl", server.GetEndpoint("https"))
        .WithEnvironment("Trelix__ProjectKey", connection["ProjectKey"])
        .WithEnvironment("Trelix__EnvironmentKey", connection["EnvironmentKey"])
        .WithEnvironment("Trelix__FileName", connection["FileName"])
        .WithEnvironment("Trelix__AccessToken", sampleToken)
        .WaitFor(server);
}

builder.Build().Run();
