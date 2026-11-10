using System.Globalization;
using NEXUS3D.Domain.Entities;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Repositories;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.Infrastructure.Persistence.Repositories;

/// <summary>
/// SQLite implementation of <see cref="IExportConfigurationRepository"/>, <see cref="IAppSettingRepository"/>,
/// and <see cref="IPluginRegistrationRepository"/>.
/// </summary>
public sealed class SqliteExportConfigurationRepository : IExportConfigurationRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteExportConfigurationRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = Guard.NotNull(connectionFactory);
    }

    public async Task<IReadOnlyList<ExportConfiguration>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT Id, ProfileName, TargetFormat, OutputDirectory, OverwriteExisting,
                   GenerateMtlFile, CompressOutput, CoordinateSystem, DecimalPrecision, UpdatedAtUtc
            FROM ExportConfigurations
            ORDER BY ProfileName ASC;
            """;

        var list = new List<ExportConfiguration>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new ExportConfiguration(
                profileName: reader.GetString(1),
                targetFormat: (ModelFormatType)reader.GetInt32(2),
                outputDirectory: reader.GetString(3),
                overwriteExisting: reader.GetInt32(4) == 1,
                generateMtlFile: reader.GetInt32(5) == 1,
                compressOutput: reader.GetInt32(6) == 1,
                coordinateSystem: (CoordinateAxisUp)reader.GetInt32(7),
                decimalPrecision: reader.GetInt32(8))
            {
                Id = Guid.Parse(reader.GetString(0)),
                UpdatedAtUtc = DateTimeOffset.Parse(reader.GetString(9), CultureInfo.InvariantCulture)
            });
        }

        return list;
    }

    public async Task<ExportConfiguration?> GetDefaultAsync(CancellationToken cancellationToken = default)
    {
        var all = await GetAllAsync(cancellationToken).ConfigureAwait(false);
        return all.FirstOrDefault(x => string.Equals(x.ProfileName, "Default Export", StringComparison.OrdinalIgnoreCase))
            ?? all.FirstOrDefault();
    }

    public async Task UpsertAsync(ExportConfiguration configuration, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(configuration);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ExportConfigurations (
                Id, ProfileName, TargetFormat, OutputDirectory, OverwriteExisting,
                GenerateMtlFile, CompressOutput, CoordinateSystem, DecimalPrecision, UpdatedAtUtc
            ) VALUES (
                $id, $profileName, $targetFormat, $outputDirectory, $overwriteExisting,
                $generateMtlFile, $compressOutput, $coordinateSystem, $decimalPrecision, $updatedAtUtc
            )
            ON CONFLICT(ProfileName) DO UPDATE SET
                TargetFormat = excluded.TargetFormat,
                OutputDirectory = excluded.OutputDirectory,
                OverwriteExisting = excluded.OverwriteExisting,
                GenerateMtlFile = excluded.GenerateMtlFile,
                CompressOutput = excluded.CompressOutput,
                CoordinateSystem = excluded.CoordinateSystem,
                DecimalPrecision = excluded.DecimalPrecision,
                UpdatedAtUtc = excluded.UpdatedAtUtc;
            """;

        cmd.Parameters.AddWithValue("$id", configuration.Id.ToString());
        cmd.Parameters.AddWithValue("$profileName", configuration.ProfileName);
        cmd.Parameters.AddWithValue("$targetFormat", (int)configuration.TargetFormat);
        cmd.Parameters.AddWithValue("$outputDirectory", configuration.OutputDirectory);
        cmd.Parameters.AddWithValue("$overwriteExisting", configuration.OverwriteExisting ? 1 : 0);
        cmd.Parameters.AddWithValue("$generateMtlFile", configuration.GenerateMtlFile ? 1 : 0);
        cmd.Parameters.AddWithValue("$compressOutput", configuration.CompressOutput ? 1 : 0);
        cmd.Parameters.AddWithValue("$coordinateSystem", (int)configuration.CoordinateSystem);
        cmd.Parameters.AddWithValue("$decimalPrecision", configuration.DecimalPrecision);
        cmd.Parameters.AddWithValue("$updatedAtUtc", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed class SqliteAppSettingRepository : IAppSettingRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteAppSettingRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = Guard.NotNull(connectionFactory);
    }

    public async Task<IReadOnlyList<AppSettingEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Key, Value, Category, Description, UpdatedAtUtc FROM AppSettings ORDER BY Category, Key;";

        var settings = new List<AppSettingEntry>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            settings.Add(new AppSettingEntry(
                key: reader.GetString(0),
                value: reader.GetString(1),
                category: reader.GetString(2),
                description: reader.GetString(3))
            {
                UpdatedAtUtc = DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture)
            });
        }

        return settings;
    }

    public async Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(key);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Value FROM AppSettings WHERE Key = $key;";
        cmd.Parameters.AddWithValue("$key", key);

        object? result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result as string;
    }

    public async Task SetValueAsync(
        string key,
        string value,
        string category = "General",
        string description = "",
        CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(key);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO AppSettings (Key, Value, Category, Description, UpdatedAtUtc)
            VALUES ($key, $value, $category, $description, $updatedAtUtc)
            ON CONFLICT(Key) DO UPDATE SET
                Value = excluded.Value,
                Category = excluded.Category,
                Description = excluded.Description,
                UpdatedAtUtc = excluded.UpdatedAtUtc;
            """;

        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$value", value ?? string.Empty);
        cmd.Parameters.AddWithValue("$category", string.IsNullOrWhiteSpace(category) ? "General" : category);
        cmd.Parameters.AddWithValue("$description", description ?? string.Empty);
        cmd.Parameters.AddWithValue("$updatedAtUtc", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed class SqlitePluginRegistrationRepository : IPluginRegistrationRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqlitePluginRegistrationRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = Guard.NotNull(connectionFactory);
    }

    public async Task<IReadOnlyList<PluginRegistration>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT Id, PluginId, Name, Version, Author, Description, AssemblyPath,
                   IsEnabled, SupportedFormats, InstalledAtUtc
            FROM PluginRegistrations
            ORDER BY Name ASC;
            """;

        var plugins = new List<PluginRegistration>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            plugins.Add(new PluginRegistration(
                pluginId: reader.GetString(1),
                name: reader.GetString(2),
                version: reader.GetString(3),
                author: reader.GetString(4),
                description: reader.GetString(5),
                assemblyPath: reader.GetString(6),
                supportedFormats: reader.GetString(8),
                isEnabled: reader.GetInt32(7) == 1)
            {
                Id = Guid.Parse(reader.GetString(0)),
                InstalledAtUtc = DateTimeOffset.Parse(reader.GetString(9), CultureInfo.InvariantCulture)
            });
        }

        return plugins;
    }

    public async Task UpsertAsync(PluginRegistration plugin, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(plugin);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO PluginRegistrations (
                Id, PluginId, Name, Version, Author, Description, AssemblyPath,
                IsEnabled, SupportedFormats, InstalledAtUtc
            ) VALUES (
                $id, $pluginId, $name, $version, $author, $description, $assemblyPath,
                $isEnabled, $supportedFormats, $installedAtUtc
            )
            ON CONFLICT(PluginId) DO UPDATE SET
                Name = excluded.Name,
                Version = excluded.Version,
                Author = excluded.Author,
                Description = excluded.Description,
                AssemblyPath = excluded.AssemblyPath,
                IsEnabled = excluded.IsEnabled,
                SupportedFormats = excluded.SupportedFormats;
            """;

        cmd.Parameters.AddWithValue("$id", plugin.Id.ToString());
        cmd.Parameters.AddWithValue("$pluginId", plugin.PluginId);
        cmd.Parameters.AddWithValue("$name", plugin.Name);
        cmd.Parameters.AddWithValue("$version", plugin.Version);
        cmd.Parameters.AddWithValue("$author", plugin.Author);
        cmd.Parameters.AddWithValue("$description", plugin.Description);
        cmd.Parameters.AddWithValue("$assemblyPath", plugin.AssemblyPath);
        cmd.Parameters.AddWithValue("$isEnabled", plugin.IsEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("$supportedFormats", plugin.SupportedFormats);
        cmd.Parameters.AddWithValue("$installedAtUtc", plugin.InstalledAtUtc.ToString("O", CultureInfo.InvariantCulture));

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SetEnabledAsync(string pluginId, bool isEnabled, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(pluginId);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE PluginRegistrations SET IsEnabled = $isEnabled WHERE PluginId = $pluginId;";
        cmd.Parameters.AddWithValue("$isEnabled", isEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("$pluginId", pluginId);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(pluginId);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM PluginRegistrations WHERE PluginId = $pluginId;";
        cmd.Parameters.AddWithValue("$pluginId", pluginId);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

