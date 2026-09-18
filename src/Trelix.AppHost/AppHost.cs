var builder = DistributedApplication.CreateBuilder(args);

var server = builder.AddProject<Projects.Trelix_Server>("trelix-server", launchProfileName: "https");

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

builder.Build().Run();
