using Microsoft.Data.Sqlite;

namespace Trelix.Server.Persistence;

/// <summary>解析持久化目录并提供启用外键约束的 SQLite 连接配置。</summary>
public sealed class StorageSettings
{
    public string DataDirectory { get; }
    public string ConnectionString { get; }

    /// <summary>解析并创建数据目录；非开发环境必须显式指定目录。</summary>
    /// <param name="configuration">外部注入的应用配置。</param>
    /// <param name="environment">当前宿主环境及内容根目录。</param>
    /// <exception cref="InvalidOperationException">非开发环境未配置持久化目录。</exception>
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
