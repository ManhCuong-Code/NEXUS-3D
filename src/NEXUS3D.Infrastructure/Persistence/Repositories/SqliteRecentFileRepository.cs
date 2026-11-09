using System.Globalization;
using Microsoft.Data.Sqlite;
using NEXUS3D.Domain.Entities;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Repositories;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.Infrastructure.Persistence.Repositories;

/// <summary>
/// SQLite implementation of <see cref="IRecentFileRepository"/>.
/// </summary>
public sealed class SqliteRecentFileRepository : IRecentFileRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteRecentFileRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = Guard.NotNull(connectionFactory);
    }

    public async Task<IReadOnlyList<RecentFileEntry>> GetAllAsync(
        int limit = 25,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT Id, FilePath, FileName, Format, FileSizeBytes, IsPinned, AccessCount, LastOpenedAtUtc
            FROM RecentFiles
            ORDER BY IsPinned DESC, LastOpenedAtUtc DESC
            LIMIT $limit;
            """;
        cmd.Parameters.AddWithValue("$limit", Math.Max(1, limit));

        var list = new List<RecentFileEntry>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var entry = new RecentFileEntry(
                filePath: reader.GetString(1),
                fileName: reader.GetString(2),
                format: (ModelFormatType)reader.GetInt32(3),
                fileSizeBytes: reader.GetInt64(4),
                isPinned: reader.GetInt32(5) == 1)
            {
                Id = Guid.Parse(reader.GetString(0))
            };

            list.Add(entry);
        }

        return list;
    }

    public async Task UpsertAsync(RecentFileEntry entry, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(entry);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO RecentFiles (
                Id, FilePath, FileName, Format, FileSizeBytes, IsPinned, AccessCount, LastOpenedAtUtc
            ) VALUES (
                $id, $filePath, $fileName, $format, $fileSizeBytes, $isPinned, $accessCount, $lastOpenedAtUtc
            )
            ON CONFLICT(FilePath) DO UPDATE SET
                FileName = excluded.FileName,
                Format = excluded.Format,
                FileSizeBytes = excluded.FileSizeBytes,
                IsPinned = excluded.IsPinned,
                AccessCount = RecentFiles.AccessCount + 1,
                LastOpenedAtUtc = excluded.LastOpenedAtUtc;
            """;

        cmd.Parameters.AddWithValue("$id", entry.Id.ToString());
        cmd.Parameters.AddWithValue("$filePath", entry.FilePath);
        cmd.Parameters.AddWithValue("$fileName", entry.FileName);
        cmd.Parameters.AddWithValue("$format", (int)entry.Format);
        cmd.Parameters.AddWithValue("$fileSizeBytes", entry.FileSizeBytes);
        cmd.Parameters.AddWithValue("$isPinned", entry.IsPinned ? 1 : 0);
        cmd.Parameters.AddWithValue("$accessCount", entry.AccessCount);
        cmd.Parameters.AddWithValue("$lastOpenedAtUtc", entry.LastOpenedAtUtc.ToString("O", CultureInfo.InvariantCulture));

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM RecentFiles WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", id.ToString());
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearUnpinnedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM RecentFiles WHERE IsPinned = 0;";
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

