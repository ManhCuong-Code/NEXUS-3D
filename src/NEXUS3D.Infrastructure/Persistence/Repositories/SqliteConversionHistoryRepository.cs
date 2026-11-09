using System.Globalization;
using Microsoft.Data.Sqlite;
using NEXUS3D.Domain.Entities;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Repositories;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.Infrastructure.Persistence.Repositories;

/// <summary>
/// SQLite implementation of <see cref="IConversionHistoryRepository"/>.
/// </summary>
public sealed class SqliteConversionHistoryRepository : IConversionHistoryRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteConversionHistoryRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = Guard.NotNull(connectionFactory);
    }

    public async Task<IReadOnlyList<ConversionHistoryRecord>> GetRecentAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT Id, JobName, SourceFilePath, TargetFilePath, SourceFormat, TargetFormat,
                   ProfileId, Status, SourceSizeBytes, OutputSizeBytes, DurationMs,
                   ErrorMessage, CreatedAtUtc, CompletedAtUtc
            FROM ConversionHistory
            ORDER BY CreatedAtUtc DESC
            LIMIT $limit;
            """;
        cmd.Parameters.AddWithValue("$limit", Math.Max(1, limit));

        var list = new List<ConversionHistoryRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(MapRecord(reader));
        }

        return list;
    }

    public async Task<ConversionHistoryRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT Id, JobName, SourceFilePath, TargetFilePath, SourceFormat, TargetFormat,
                   ProfileId, Status, SourceSizeBytes, OutputSizeBytes, DurationMs,
                   ErrorMessage, CreatedAtUtc, CompletedAtUtc
            FROM ConversionHistory
            WHERE Id = $id;
            """;
        cmd.Parameters.AddWithValue("$id", id.ToString());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? MapRecord(reader)
            : null;
    }

    public async Task AddAsync(ConversionHistoryRecord record, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(record);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ConversionHistory (
                Id, JobName, SourceFilePath, TargetFilePath, SourceFormat, TargetFormat,
                ProfileId, Status, SourceSizeBytes, OutputSizeBytes, DurationMs,
                ErrorMessage, CreatedAtUtc, CompletedAtUtc
            ) VALUES (
                $id, $jobName, $sourceFilePath, $targetFilePath, $sourceFormat, $targetFormat,
                $profileId, $status, $sourceSizeBytes, $outputSizeBytes, $durationMs,
                $errorMessage, $createdAtUtc, $completedAtUtc
            );
            """;

        BindParameters(cmd, record);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(ConversionHistoryRecord record, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(record);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE ConversionHistory
            SET Status = $status,
                OutputSizeBytes = $outputSizeBytes,
                DurationMs = $durationMs,
                ErrorMessage = $errorMessage,
                CompletedAtUtc = $completedAtUtc
            WHERE Id = $id;
            """;

        BindParameters(cmd, record);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM ConversionHistory;";
        object? result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    public async Task ClearHistoryAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM ConversionHistory;";
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void BindParameters(SqliteCommand cmd, ConversionHistoryRecord record)
    {
        cmd.Parameters.AddWithValue("$id", record.Id.ToString());
        cmd.Parameters.AddWithValue("$jobName", record.JobName);
        cmd.Parameters.AddWithValue("$sourceFilePath", record.SourceFilePath);
        cmd.Parameters.AddWithValue("$targetFilePath", record.TargetFilePath);
        cmd.Parameters.AddWithValue("$sourceFormat", (int)record.SourceFormat);
        cmd.Parameters.AddWithValue("$targetFormat", (int)record.TargetFormat);
        cmd.Parameters.AddWithValue("$profileId", record.ProfileId.HasValue ? record.ProfileId.Value.ToString() : DBNull.Value);
        cmd.Parameters.AddWithValue("$status", (int)record.Status);
        cmd.Parameters.AddWithValue("$sourceSizeBytes", record.SourceSizeBytes);
        cmd.Parameters.AddWithValue("$outputSizeBytes", record.OutputSizeBytes);
        cmd.Parameters.AddWithValue("$durationMs", record.DurationMs);
        cmd.Parameters.AddWithValue("$errorMessage", (object?)record.ErrorMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$createdAtUtc", record.CreatedAtUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$completedAtUtc", record.CompletedAtUtc.HasValue ? record.CompletedAtUtc.Value.ToString("O") : DBNull.Value);
    }

    private static ConversionHistoryRecord MapRecord(SqliteDataReader reader)
    {
        Guid id = Guid.Parse(reader.GetString(0));
        string jobName = reader.GetString(1);
        string sourcePath = reader.GetString(2);
        string targetPath = reader.GetString(3);
        var sourceFormat = (ModelFormatType)reader.GetInt32(4);
        var targetFormat = (ModelFormatType)reader.GetInt32(5);
        Guid? profileId = reader.IsDBNull(6) ? null : Guid.Parse(reader.GetString(6));
        var status = (ConversionStatus)reader.GetInt32(7);
        long sourceSize = reader.GetInt64(8);
        long outputSize = reader.GetInt64(9);
        long durationMs = reader.GetInt64(10);
        string? errorMsg = reader.IsDBNull(11) ? null : reader.GetString(11);
        DateTimeOffset createdAt = DateTimeOffset.Parse(reader.GetString(12), CultureInfo.InvariantCulture);

        var record = new ConversionHistoryRecord(
            jobName,
            sourcePath,
            targetPath,
            sourceFormat,
            targetFormat,
            sourceSize,
            profileId)
        {
            Id = id,
            CreatedAtUtc = createdAt
        };

        if (status == ConversionStatus.Completed)
        {
            record.MarkCompleted(outputSize, durationMs);
        }
        else if (status == ConversionStatus.Failed && !string.IsNullOrWhiteSpace(errorMsg))
        {
            record.MarkFailed(errorMsg, durationMs);
        }

        return record;
    }
}

