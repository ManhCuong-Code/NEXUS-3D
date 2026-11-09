using System.Globalization;
using Microsoft.Data.Sqlite;
using NEXUS3D.Domain.Entities;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Repositories;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.Infrastructure.Persistence.Repositories;

/// <summary>
/// SQLite implementation of <see cref="IConversionProfileRepository"/>.
/// </summary>
public sealed class SqliteConversionProfileRepository : IConversionProfileRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteConversionProfileRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = Guard.NotNull(connectionFactory);
    }

    public async Task<IReadOnlyList<ConversionProfile>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT Id, Name, Description, SourceFormat, TargetFormat, ScaleFactor,
                   OptimizeMesh, GreedyMeshing, PreserveMaterials, FlipUVs, AxisUp,
                   IsBuiltIn, UpdatedAtUtc
            FROM ConversionProfiles
            ORDER BY IsBuiltIn DESC, Name ASC;
            """;

        var profiles = new List<ConversionProfile>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            profiles.Add(MapProfile(reader));
        }

        return profiles;
    }

    public async Task<ConversionProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT Id, Name, Description, SourceFormat, TargetFormat, ScaleFactor,
                   OptimizeMesh, GreedyMeshing, PreserveMaterials, FlipUVs, AxisUp,
                   IsBuiltIn, UpdatedAtUtc
            FROM ConversionProfiles
            WHERE Id = $id;
            """;
        cmd.Parameters.AddWithValue("$id", id.ToString());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? MapProfile(reader)
            : null;
    }

    public async Task UpsertAsync(ConversionProfile profile, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(profile);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ConversionProfiles (
                Id, Name, Description, SourceFormat, TargetFormat, ScaleFactor,
                OptimizeMesh, GreedyMeshing, PreserveMaterials, FlipUVs, AxisUp,
                IsBuiltIn, UpdatedAtUtc
            ) VALUES (
                $id, $name, $description, $sourceFormat, $targetFormat, $scaleFactor,
                $optimizeMesh, $greedyMeshing, $preserveMaterials, $flipUVs, $axisUp,
                $isBuiltIn, $updatedAtUtc
            )
            ON CONFLICT(Name) DO UPDATE SET
                Description = excluded.Description,
                SourceFormat = excluded.SourceFormat,
                TargetFormat = excluded.TargetFormat,
                ScaleFactor = excluded.ScaleFactor,
                OptimizeMesh = excluded.OptimizeMesh,
                GreedyMeshing = excluded.GreedyMeshing,
                PreserveMaterials = excluded.PreserveMaterials,
                FlipUVs = excluded.FlipUVs,
                AxisUp = excluded.AxisUp,
                UpdatedAtUtc = excluded.UpdatedAtUtc;
            """;

        cmd.Parameters.AddWithValue("$id", profile.Id.ToString());
        cmd.Parameters.AddWithValue("$name", profile.Name);
        cmd.Parameters.AddWithValue("$description", profile.Description);
        cmd.Parameters.AddWithValue("$sourceFormat", (int)profile.SourceFormat);
        cmd.Parameters.AddWithValue("$targetFormat", (int)profile.TargetFormat);
        cmd.Parameters.AddWithValue("$scaleFactor", profile.ScaleFactor);
        cmd.Parameters.AddWithValue("$optimizeMesh", profile.OptimizeMesh ? 1 : 0);
        cmd.Parameters.AddWithValue("$greedyMeshing", profile.GreedyMeshing ? 1 : 0);
        cmd.Parameters.AddWithValue("$preserveMaterials", profile.PreserveMaterials ? 1 : 0);
        cmd.Parameters.AddWithValue("$flipUVs", profile.FlipUVs ? 1 : 0);
        cmd.Parameters.AddWithValue("$axisUp", (int)profile.AxisUp);
        cmd.Parameters.AddWithValue("$isBuiltIn", profile.IsBuiltIn ? 1 : 0);
        cmd.Parameters.AddWithValue("$updatedAtUtc", profile.UpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture));

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM ConversionProfiles WHERE Id = $id AND IsBuiltIn = 0;";
        cmd.Parameters.AddWithValue("$id", id.ToString());
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static ConversionProfile MapProfile(SqliteDataReader reader) =>
        new(
            name: reader.GetString(1),
            description: reader.GetString(2),
            sourceFormat: (ModelFormatType)reader.GetInt32(3),
            targetFormat: (ModelFormatType)reader.GetInt32(4),
            scaleFactor: reader.GetDouble(5),
            optimizeMesh: reader.GetInt32(6) == 1,
            greedyMeshing: reader.GetInt32(7) == 1,
            preserveMaterials: reader.GetInt32(8) == 1,
            isBuiltIn: reader.GetInt32(11) == 1)
        {
            Id = Guid.Parse(reader.GetString(0)),
            FlipUVs = reader.GetInt32(9) == 1,
            AxisUp = (CoordinateAxisUp)reader.GetInt32(10),
            UpdatedAtUtc = DateTimeOffset.Parse(reader.GetString(12), CultureInfo.InvariantCulture)
        };
}

