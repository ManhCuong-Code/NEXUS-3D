using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.Infrastructure.Persistence;

/// <summary>
/// Versioned SQLite schema migrator ensuring forward-compatible database upgrades.
/// </summary>
public sealed class DatabaseSchemaMigrator
{
    private readonly ILogger<DatabaseSchemaMigrator> _logger;

    private static readonly IReadOnlyList<(int Version, string Name, string Sql)> Migrations =
    [
        (
            Version: 1,
            Name: "001_InitialFoundationSchema",
            Sql: """
            CREATE TABLE IF NOT EXISTS ConversionHistory (
                Id TEXT PRIMARY KEY NOT NULL,
                JobName TEXT NOT NULL,
                SourceFilePath TEXT NOT NULL,
                TargetFilePath TEXT NOT NULL,
                SourceFormat INTEGER NOT NULL,
                TargetFormat INTEGER NOT NULL,
                ProfileId TEXT NULL,
                Status INTEGER NOT NULL,
                SourceSizeBytes INTEGER NOT NULL DEFAULT 0,
                OutputSizeBytes INTEGER NOT NULL DEFAULT 0,
                DurationMs INTEGER NOT NULL DEFAULT 0,
                ErrorMessage TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                CompletedAtUtc TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_ConversionHistory_CreatedAtUtc
                ON ConversionHistory (CreatedAtUtc DESC);

            CREATE TABLE IF NOT EXISTS RecentFiles (
                Id TEXT PRIMARY KEY NOT NULL,
                FilePath TEXT NOT NULL UNIQUE,
                FileName TEXT NOT NULL,
                Format INTEGER NOT NULL,
                FileSizeBytes INTEGER NOT NULL DEFAULT 0,
                IsPinned INTEGER NOT NULL DEFAULT 0,
                AccessCount INTEGER NOT NULL DEFAULT 1,
                LastOpenedAtUtc TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_RecentFiles_LastOpenedAtUtc
                ON RecentFiles (IsPinned DESC, LastOpenedAtUtc DESC);

            CREATE TABLE IF NOT EXISTS AppSettings (
                Key TEXT PRIMARY KEY NOT NULL,
                Value TEXT NOT NULL,
                Category TEXT NOT NULL,
                Description TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ExportConfigurations (
                Id TEXT PRIMARY KEY NOT NULL,
                ProfileName TEXT NOT NULL UNIQUE,
                TargetFormat INTEGER NOT NULL,
                OutputDirectory TEXT NOT NULL,
                OverwriteExisting INTEGER NOT NULL DEFAULT 0,
                GenerateMtlFile INTEGER NOT NULL DEFAULT 1,
                CompressOutput INTEGER NOT NULL DEFAULT 0,
                CoordinateSystem INTEGER NOT NULL DEFAULT 0,
                DecimalPrecision INTEGER NOT NULL DEFAULT 4,
                UpdatedAtUtc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ConversionProfiles (
                Id TEXT PRIMARY KEY NOT NULL,
                Name TEXT NOT NULL UNIQUE,
                Description TEXT NOT NULL,
                SourceFormat INTEGER NOT NULL,
                TargetFormat INTEGER NOT NULL,
                ScaleFactor REAL NOT NULL DEFAULT 1.0,
                OptimizeMesh INTEGER NOT NULL DEFAULT 1,
                GreedyMeshing INTEGER NOT NULL DEFAULT 1,
                PreserveMaterials INTEGER NOT NULL DEFAULT 1,
                FlipUVs INTEGER NOT NULL DEFAULT 0,
                AxisUp INTEGER NOT NULL DEFAULT 0,
                IsBuiltIn INTEGER NOT NULL DEFAULT 0,
                UpdatedAtUtc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS PluginRegistrations (
                Id TEXT PRIMARY KEY NOT NULL,
                PluginId TEXT NOT NULL UNIQUE,
                Name TEXT NOT NULL,
                Version TEXT NOT NULL,
                Author TEXT NOT NULL,
                Description TEXT NOT NULL,
                AssemblyPath TEXT NOT NULL,
                IsEnabled INTEGER NOT NULL DEFAULT 1,
                SupportedFormats TEXT NOT NULL,
                InstalledAtUtc TEXT NOT NULL
            );
            """
        )
    ];

    public DatabaseSchemaMigrator(ILogger<DatabaseSchemaMigrator> logger)
    {
        _logger = Guard.NotNull(logger);
    }

    public async Task<int> MigrateAsync(SqliteConnection connection, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(connection);

        await EnsureMigrationTableExistsAsync(connection, cancellationToken).ConfigureAwait(false);
        int currentVersion = await GetCurrentVersionAsync(connection, cancellationToken).ConfigureAwait(false);

        foreach (var migration in Migrations.Where(m => m.Version > currentVersion).OrderBy(m => m.Version))
        {
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = migration.Sql;
                    await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                await using (var recordCmd = connection.CreateCommand())
                {
                    recordCmd.Transaction = transaction;
                    recordCmd.CommandText = """
                        INSERT INTO SchemaMigrations (Version, MigrationName, AppliedAtUtc)
                        VALUES ($version, $name, $appliedAt);
                        """;
                    recordCmd.Parameters.AddWithValue("$version", migration.Version);
                    recordCmd.Parameters.AddWithValue("$name", migration.Name);
                    recordCmd.Parameters.AddWithValue("$appliedAt", DateTimeOffset.UtcNow.ToString("O"));
                    await recordCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                currentVersion = migration.Version;
                _logger.LogInformation("Applied database migration v{Version}: {MigrationName}", migration.Version, migration.Name);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogError(ex, "Database migration v{Version} ({MigrationName}) failed.", migration.Version, migration.Name);
                throw;
            }
        }

        return currentVersion;
    }

    private static async Task EnsureMigrationTableExistsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS SchemaMigrations (
                Version INTEGER PRIMARY KEY NOT NULL,
                MigrationName TEXT NOT NULL,
                AppliedAtUtc TEXT NOT NULL
            );
            """;
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<int> GetCurrentVersionAsync(SqliteConnection connection, CancellationToken cancellationToken = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(Version), 0) FROM SchemaMigrations;";
        object? result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(result);
    }
}

