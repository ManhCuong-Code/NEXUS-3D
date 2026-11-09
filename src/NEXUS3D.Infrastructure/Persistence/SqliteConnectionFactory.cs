using Microsoft.Data.Sqlite;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.Infrastructure.Persistence;

/// <summary>
/// Configuration settings for SQLite database connections.
/// </summary>
public sealed class DatabaseConfiguration
{
    public string DatabaseFilePath { get; }
    public int CommandTimeoutSeconds { get; init; } = 30;
    public bool EnableWalMode { get; init; } = true;

    public DatabaseConfiguration(string databaseFilePath)
    {
        DatabaseFilePath = Guard.NotNullOrWhiteSpace(databaseFilePath);
    }

    public string BuildConnectionString()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = DatabaseFilePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = CommandTimeoutSeconds,
            Pooling = true
        };

        return builder.ToString();
    }
}

/// <summary>
/// Manages creation and lifecycle of SQLite connections.
/// </summary>
public sealed class SqliteConnectionFactory
{
    private readonly DatabaseConfiguration _configuration;

    public SqliteConnectionFactory(DatabaseConfiguration configuration)
    {
        _configuration = Guard.NotNull(configuration);
    }

    public DatabaseConfiguration Configuration => _configuration;

    public async Task<SqliteConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        string? directory = Path.GetDirectoryName(_configuration.DatabaseFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connection = new SqliteConnection(_configuration.BuildConnectionString());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var pragmaCommand = connection.CreateCommand();
        pragmaCommand.CommandText = _configuration.EnableWalMode
            ? "PRAGMA foreign_keys = ON; PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;"
            : "PRAGMA foreign_keys = ON;";
        await pragmaCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        return connection;
    }
}

