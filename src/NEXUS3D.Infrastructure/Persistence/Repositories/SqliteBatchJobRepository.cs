using System.Globalization;
using Microsoft.Data.Sqlite;
using NEXUS3D.Domain.Entities;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Repositories;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.Infrastructure.Persistence.Repositories;

/// <summary>
/// SQLite implementation of <see cref="IBatchJobRepository"/> storing batch jobs and items.
/// </summary>
public sealed class SqliteBatchJobRepository : IBatchJobRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteBatchJobRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = Guard.NotNull(connectionFactory);
    }

    public async Task AddJobAsync(BatchJobRecord job, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(job);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = """
                    INSERT INTO BatchJobs (
                        Id, JobName, InputDirectory, OutputDirectory, SourceFormat, TargetFormat,
                        Priority, Status, TotalFiles, SucceededFiles, FailedFiles, SkippedFiles,
                        OverwriteExisting, ContinueOnError, RetryLimit, MaxConcurrency,
                        DurationMs, ErrorMessage, CreatedAtUtc, StartedAtUtc, CompletedAtUtc
                    ) VALUES (
                        $id, $name, $inDir, $outDir, $srcFmt, $tgtFmt,
                        $pri, $status, $total, $succ, $fail, $skip,
                        $overwrite, $contOnError, $retry, $concurrency,
                        $duration, $error, $createdAt, $startedAt, $completedAt
                    );
                    """;
                cmd.Parameters.AddWithValue("$id", job.Id.ToString());
                cmd.Parameters.AddWithValue("$name", job.JobName);
                cmd.Parameters.AddWithValue("$inDir", (object?)job.InputDirectory ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$outDir", job.OutputDirectory);
                cmd.Parameters.AddWithValue("$srcFmt", job.SourceFormat.HasValue ? (int)job.SourceFormat.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("$tgtFmt", (int)job.TargetFormat);
                cmd.Parameters.AddWithValue("$pri", (int)job.Priority);
                cmd.Parameters.AddWithValue("$status", (int)job.Status);
                cmd.Parameters.AddWithValue("$total", job.TotalFiles);
                cmd.Parameters.AddWithValue("$succ", job.SucceededFiles);
                cmd.Parameters.AddWithValue("$fail", job.FailedFiles);
                cmd.Parameters.AddWithValue("$skip", job.SkippedFiles);
                cmd.Parameters.AddWithValue("$overwrite", job.OverwriteExisting ? 1 : 0);
                cmd.Parameters.AddWithValue("$contOnError", job.ContinueOnError ? 1 : 0);
                cmd.Parameters.AddWithValue("$retry", job.RetryLimit);
                cmd.Parameters.AddWithValue("$concurrency", job.MaxConcurrency);
                cmd.Parameters.AddWithValue("$duration", job.DurationMs);
                cmd.Parameters.AddWithValue("$error", (object?)job.ErrorMessage ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$createdAt", job.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("$startedAt", job.StartedAtUtc.HasValue ? job.StartedAtUtc.Value.ToString("O", CultureInfo.InvariantCulture) : DBNull.Value);
                cmd.Parameters.AddWithValue("$completedAt", job.CompletedAtUtc.HasValue ? job.CompletedAtUtc.Value.ToString("O", CultureInfo.InvariantCulture) : DBNull.Value);

                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            foreach (var item in job.Items)
            {
                await using var itemCmd = connection.CreateCommand();
                itemCmd.Transaction = transaction;
                itemCmd.CommandText = """
                    INSERT INTO BatchJobItems (
                        Id, JobId, SourceFilePath, TargetFilePath, SourceFormat, TargetFormat,
                        Status, ErrorMessage, ErrorCode, RetryCount, DurationMs,
                        SourceSizeBytes, TargetSizeBytes, CreatedAtUtc, CompletedAtUtc
                    ) VALUES (
                        $id, $jobId, $srcPath, $tgtPath, $srcFmt, $tgtFmt,
                        $status, $error, $code, $retry, $duration,
                        $srcSize, $tgtSize, $createdAt, $completedAt
                    );
                    """;
                itemCmd.Parameters.AddWithValue("$id", item.Id.ToString());
                itemCmd.Parameters.AddWithValue("$jobId", job.Id.ToString());
                itemCmd.Parameters.AddWithValue("$srcPath", item.SourceFilePath);
                itemCmd.Parameters.AddWithValue("$tgtPath", item.TargetFilePath);
                itemCmd.Parameters.AddWithValue("$srcFmt", (int)item.SourceFormat);
                itemCmd.Parameters.AddWithValue("$tgtFmt", (int)item.TargetFormat);
                itemCmd.Parameters.AddWithValue("$status", (int)item.Status);
                itemCmd.Parameters.AddWithValue("$error", (object?)item.ErrorMessage ?? DBNull.Value);
                itemCmd.Parameters.AddWithValue("$code", (object?)item.ErrorCode ?? DBNull.Value);
                itemCmd.Parameters.AddWithValue("$retry", item.RetryCount);
                itemCmd.Parameters.AddWithValue("$duration", item.DurationMs);
                itemCmd.Parameters.AddWithValue("$srcSize", item.SourceSizeBytes);
                itemCmd.Parameters.AddWithValue("$tgtSize", item.TargetSizeBytes);
                itemCmd.Parameters.AddWithValue("$createdAt", item.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
                itemCmd.Parameters.AddWithValue("$completedAt", item.CompletedAtUtc.HasValue ? item.CompletedAtUtc.Value.ToString("O", CultureInfo.InvariantCulture) : DBNull.Value);

                await itemCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task UpdateJobAsync(BatchJobRecord job, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(job);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = """
                    UPDATE BatchJobs SET
                        JobName = $name,
                        Status = $status,
                        Priority = $pri,
                        TotalFiles = $total,
                        SucceededFiles = $succ,
                        FailedFiles = $fail,
                        SkippedFiles = $skip,
                        DurationMs = $duration,
                        ErrorMessage = $error,
                        StartedAtUtc = $startedAt,
                        CompletedAtUtc = $completedAt
                    WHERE Id = $id;
                    """;
                cmd.Parameters.AddWithValue("$id", job.Id.ToString());
                cmd.Parameters.AddWithValue("$name", job.JobName);
                cmd.Parameters.AddWithValue("$status", (int)job.Status);
                cmd.Parameters.AddWithValue("$pri", (int)job.Priority);
                cmd.Parameters.AddWithValue("$total", job.TotalFiles);
                cmd.Parameters.AddWithValue("$succ", job.SucceededFiles);
                cmd.Parameters.AddWithValue("$fail", job.FailedFiles);
                cmd.Parameters.AddWithValue("$skip", job.SkippedFiles);
                cmd.Parameters.AddWithValue("$duration", job.DurationMs);
                cmd.Parameters.AddWithValue("$error", (object?)job.ErrorMessage ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$startedAt", job.StartedAtUtc.HasValue ? job.StartedAtUtc.Value.ToString("O", CultureInfo.InvariantCulture) : DBNull.Value);
                cmd.Parameters.AddWithValue("$completedAt", job.CompletedAtUtc.HasValue ? job.CompletedAtUtc.Value.ToString("O", CultureInfo.InvariantCulture) : DBNull.Value);

                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            foreach (var item in job.Items)
            {
                await using var itemCmd = connection.CreateCommand();
                itemCmd.Transaction = transaction;
                itemCmd.CommandText = """
                    INSERT INTO BatchJobItems (
                        Id, JobId, SourceFilePath, TargetFilePath, SourceFormat, TargetFormat,
                        Status, ErrorMessage, ErrorCode, RetryCount, DurationMs,
                        SourceSizeBytes, TargetSizeBytes, CreatedAtUtc, CompletedAtUtc
                    ) VALUES (
                        $id, $jobId, $srcPath, $tgtPath, $srcFmt, $tgtFmt,
                        $status, $error, $code, $retry, $duration,
                        $srcSize, $tgtSize, $createdAt, $completedAt
                    )
                    ON CONFLICT(Id) DO UPDATE SET
                        Status = $status,
                        ErrorMessage = $error,
                        ErrorCode = $code,
                        RetryCount = $retry,
                        DurationMs = $duration,
                        TargetSizeBytes = $tgtSize,
                        CompletedAtUtc = $completedAt;
                    """;
                itemCmd.Parameters.AddWithValue("$id", item.Id.ToString());
                itemCmd.Parameters.AddWithValue("$jobId", job.Id.ToString());
                itemCmd.Parameters.AddWithValue("$srcPath", item.SourceFilePath);
                itemCmd.Parameters.AddWithValue("$tgtPath", item.TargetFilePath);
                itemCmd.Parameters.AddWithValue("$srcFmt", (int)item.SourceFormat);
                itemCmd.Parameters.AddWithValue("$tgtFmt", (int)item.TargetFormat);
                itemCmd.Parameters.AddWithValue("$status", (int)item.Status);
                itemCmd.Parameters.AddWithValue("$error", (object?)item.ErrorMessage ?? DBNull.Value);
                itemCmd.Parameters.AddWithValue("$code", (object?)item.ErrorCode ?? DBNull.Value);
                itemCmd.Parameters.AddWithValue("$retry", item.RetryCount);
                itemCmd.Parameters.AddWithValue("$duration", item.DurationMs);
                itemCmd.Parameters.AddWithValue("$srcSize", item.SourceSizeBytes);
                itemCmd.Parameters.AddWithValue("$tgtSize", item.TargetSizeBytes);
                itemCmd.Parameters.AddWithValue("$createdAt", item.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
                itemCmd.Parameters.AddWithValue("$completedAt", item.CompletedAtUtc.HasValue ? item.CompletedAtUtc.Value.ToString("O", CultureInfo.InvariantCulture) : DBNull.Value);

                await itemCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<BatchJobRecord?> GetJobByIdAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        BatchJobRecord? job = null;
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT Id, JobName, InputDirectory, OutputDirectory, SourceFormat, TargetFormat,
                       Priority, Status, TotalFiles, SucceededFiles, FailedFiles, SkippedFiles,
                       OverwriteExisting, ContinueOnError, RetryLimit, MaxConcurrency,
                       DurationMs, ErrorMessage, CreatedAtUtc, StartedAtUtc, CompletedAtUtc
                FROM BatchJobs
                WHERE Id = $id;
                """;
            cmd.Parameters.AddWithValue("$id", jobId.ToString());

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                job = MapJob(reader);
            }
        }

        if (job is null)
        {
            return null;
        }

        await using (var itemCmd = connection.CreateCommand())
        {
            itemCmd.CommandText = """
                SELECT Id, JobId, SourceFilePath, TargetFilePath, SourceFormat, TargetFormat,
                       Status, ErrorMessage, ErrorCode, RetryCount, DurationMs,
                       SourceSizeBytes, TargetSizeBytes, CreatedAtUtc, CompletedAtUtc
                FROM BatchJobItems
                WHERE JobId = $jobId
                ORDER BY CreatedAtUtc ASC;
                """;
            itemCmd.Parameters.AddWithValue("$jobId", jobId.ToString());

            await using var itemReader = await itemCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await itemReader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                job.Items.Add(MapItem(itemReader));
            }
        }

        return job;
    }

    public async Task<IReadOnlyList<BatchJobRecord>> GetRecentJobsAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        var list = new List<BatchJobRecord>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT Id, JobName, InputDirectory, OutputDirectory, SourceFormat, TargetFormat,
                       Priority, Status, TotalFiles, SucceededFiles, FailedFiles, SkippedFiles,
                       OverwriteExisting, ContinueOnError, RetryLimit, MaxConcurrency,
                       DurationMs, ErrorMessage, CreatedAtUtc, StartedAtUtc, CompletedAtUtc
                FROM BatchJobs
                ORDER BY CreatedAtUtc DESC
                LIMIT $limit;
                """;
            cmd.Parameters.AddWithValue("$limit", Math.Max(1, limit));

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                list.Add(MapJob(reader));
            }
        }

        foreach (var job in list)
        {
            await using var itemCmd = connection.CreateCommand();
            itemCmd.CommandText = """
                SELECT Id, JobId, SourceFilePath, TargetFilePath, SourceFormat, TargetFormat,
                       Status, ErrorMessage, ErrorCode, RetryCount, DurationMs,
                       SourceSizeBytes, TargetSizeBytes, CreatedAtUtc, CompletedAtUtc
                FROM BatchJobItems
                WHERE JobId = $jobId
                ORDER BY CreatedAtUtc ASC;
                """;
            itemCmd.Parameters.AddWithValue("$jobId", job.Id.ToString());

            await using var itemReader = await itemCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await itemReader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                job.Items.Add(MapItem(itemReader));
            }
        }

        return list;
    }

    public async Task DeleteJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using (var itemCmd = connection.CreateCommand())
            {
                itemCmd.Transaction = transaction;
                itemCmd.CommandText = "DELETE FROM BatchJobItems WHERE JobId = $jobId;";
                itemCmd.Parameters.AddWithValue("$jobId", jobId.ToString());
                await itemCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = "DELETE FROM BatchJobs WHERE Id = $id;";
                cmd.Parameters.AddWithValue("$id", jobId.ToString());
                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static BatchJobRecord MapJob(SqliteDataReader reader)
    {
        var id = Guid.Parse(reader.GetString(0));
        var jobName = reader.GetString(1);
        var inputDir = reader.IsDBNull(2) ? null : reader.GetString(2);
        var outputDir = reader.GetString(3);
        ModelFormatType? srcFmt = reader.IsDBNull(4) ? null : (ModelFormatType)reader.GetInt32(4);
        var tgtFmt = (ModelFormatType)reader.GetInt32(5);
        var priority = (BatchJobPriority)reader.GetInt32(6);
        var status = (BatchJobStatus)reader.GetInt32(7);
        var total = reader.GetInt32(8);
        var succ = reader.GetInt32(9);
        var fail = reader.GetInt32(10);
        var skip = reader.GetInt32(11);
        var overwrite = reader.GetInt32(12) == 1;
        var contOnError = reader.GetInt32(13) == 1;
        var retry = reader.GetInt32(14);
        var concurrency = reader.GetInt32(15);
        var duration = reader.GetInt64(16);
        var error = reader.IsDBNull(17) ? null : reader.GetString(17);
        var createdAt = DateTimeOffset.Parse(reader.GetString(18), CultureInfo.InvariantCulture);
        DateTimeOffset? startedAt = reader.IsDBNull(19) ? null : DateTimeOffset.Parse(reader.GetString(19), CultureInfo.InvariantCulture);
        DateTimeOffset? completedAt = reader.IsDBNull(20) ? null : DateTimeOffset.Parse(reader.GetString(20), CultureInfo.InvariantCulture);

        var record = new BatchJobRecord(id, jobName, outputDir, tgtFmt, priority, srcFmt, inputDir, overwrite, contOnError, retry, concurrency)
        {
            Status = status,
            TotalFiles = total,
            SucceededFiles = succ,
            FailedFiles = fail,
            SkippedFiles = skip,
            DurationMs = duration,
            ErrorMessage = error,
            StartedAtUtc = startedAt,
            CompletedAtUtc = completedAt
        };

        return record;
    }

    private static BatchJobItemRecord MapItem(SqliteDataReader reader)
    {
        var id = Guid.Parse(reader.GetString(0));
        var jobId = Guid.Parse(reader.GetString(1));
        var srcPath = reader.GetString(2);
        var tgtPath = reader.GetString(3);
        var srcFmt = (ModelFormatType)reader.GetInt32(4);
        var tgtFmt = (ModelFormatType)reader.GetInt32(5);
        var status = (BatchItemStatus)reader.GetInt32(6);
        var error = reader.IsDBNull(7) ? null : reader.GetString(7);
        var code = reader.IsDBNull(8) ? null : reader.GetString(8);
        var retry = reader.GetInt32(9);
        var duration = reader.GetInt64(10);
        var srcSize = reader.GetInt64(11);
        var tgtSize = reader.GetInt64(12);
        var createdAt = DateTimeOffset.Parse(reader.GetString(13), CultureInfo.InvariantCulture);
        DateTimeOffset? completedAt = reader.IsDBNull(14) ? null : DateTimeOffset.Parse(reader.GetString(14), CultureInfo.InvariantCulture);

        return new BatchJobItemRecord(id, jobId, srcPath, tgtPath, srcFmt, tgtFmt, srcSize)
        {
            Status = status,
            ErrorMessage = error,
            ErrorCode = code,
            RetryCount = retry,
            DurationMs = duration,
            TargetSizeBytes = tgtSize,
            CompletedAtUtc = completedAt
        };
    }
}
