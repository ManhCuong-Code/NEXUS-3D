using Microsoft.Extensions.Logging;
using NEXUS3D.Application.Interfaces;
using NEXUS3D.Domain.Entities;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Repositories;
using NEXUS3D.Domain.Rules;
using NEXUS3D.Shared.Guards;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Application.UseCases;

/// <summary>
/// Coordinates recent file history and conversion log management.
/// </summary>
public sealed class FileManagerService : IFileManagerService
{
    private readonly IRecentFileRepository _recentFileRepository;
    private readonly IConversionHistoryRepository _historyRepository;
    private readonly ILogger<FileManagerService> _logger;

    public FileManagerService(
        IRecentFileRepository recentFileRepository,
        IConversionHistoryRepository historyRepository,
        ILogger<FileManagerService> logger)
    {
        _recentFileRepository = Guard.NotNull(recentFileRepository);
        _historyRepository = Guard.NotNull(historyRepository);
        _logger = Guard.NotNull(logger);
    }

    public async Task<OperationResult<IReadOnlyList<RecentFileEntry>>> GetRecentFilesAsync(
        int limit = 25,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var items = await _recentFileRepository.GetAllAsync(limit, cancellationToken).ConfigureAwait(false);
            return OperationResult<IReadOnlyList<RecentFileEntry>>.Success(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve recent files.");
            return OperationResult<IReadOnlyList<RecentFileEntry>>.Failure("RECENT_FILES_ERR", ex.Message);
        }
    }

    public async Task<OperationResult<IReadOnlyList<ConversionHistoryRecord>>> GetConversionHistoryAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var records = await _historyRepository.GetRecentAsync(limit, cancellationToken).ConfigureAwait(false);
            return OperationResult<IReadOnlyList<ConversionHistoryRecord>>.Success(records);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve conversion history.");
            return OperationResult<IReadOnlyList<ConversionHistoryRecord>>.Failure("HISTORY_LOAD_ERR", ex.Message);
        }
    }

    public async Task<OperationResult<RecentFileEntry>> RegisterFileAccessAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return OperationResult<RecentFileEntry>.Failure("FILE_PATH_EMPTY", "File path cannot be empty.");
        }

        try
        {
            string fileName = Path.GetFileName(filePath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = filePath;
            }

            long size = File.Exists(filePath) ? new FileInfo(filePath).Length : 0;
            ModelFormatType format = ConversionDomainRules.DetectFormatFromPath(filePath);

            var entry = new RecentFileEntry(filePath, fileName, format, size);
            await _recentFileRepository.UpsertAsync(entry, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Registered recent file access: {FilePath}", filePath);
            return OperationResult<RecentFileEntry>.Success(entry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register recent file access for {FilePath}.", filePath);
            return OperationResult<RecentFileEntry>.Failure("RECENT_REGISTER_ERR", ex.Message);
        }
    }

    public async Task<OperationResult> TogglePinRecentFileAsync(
        RecentFileEntry entry,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(entry);
        try
        {
            entry.SetPinned(!entry.IsPinned);
            await _recentFileRepository.UpsertAsync(entry, cancellationToken).ConfigureAwait(false);
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle pin state for recent file {FileId}.", entry.Id);
            return OperationResult.Failure("RECENT_PIN_ERR", ex.Message);
        }
    }

    public async Task<OperationResult> RemoveRecentFileAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            await _recentFileRepository.RemoveAsync(id, cancellationToken).ConfigureAwait(false);
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove recent file {FileId}.", id);
            return OperationResult.Failure("RECENT_REMOVE_ERR", ex.Message);
        }
    }

    public async Task<OperationResult> ClearHistoryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _historyRepository.ClearHistoryAsync(cancellationToken).ConfigureAwait(false);
            await _recentFileRepository.ClearUnpinnedAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Cleared conversion history and unpinned recent files.");
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear file history.");
            return OperationResult.Failure("HISTORY_CLEAR_ERR", ex.Message);
        }
    }
}

