using Microsoft.Data.Sqlite;

namespace Trelix.Server.Persistence;

public sealed class StorageSettings
{
    public string DataDirectory { get; }
    public string ConnectionString { get; }

    public StorageSettings(IConfiguration configuration, IHostEnvironment environment)
    {
        var directory = configuration["Trelix:Storage:DataDirectory"];
        if (string.IsNullOrWhiteSpace(directory))
        {
            if (!environment.IsDevelopment())
                throw new InvalidOperationException("Trelix:Storage:DataDirectory must be configured.");
            directory = Path.Combine(environment.ContentRootPath, ".trelix-data");
        }

        DataDirectory = Path.GetFullPath(directory, environment.ContentRootPath);
        Directory.CreateDirectory(DataDirectory);
        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(DataDirectory, "trelix.db"),
            ForeignKeys = true,
            DefaultTimeout = 30
        }.ToString();
    }
}
