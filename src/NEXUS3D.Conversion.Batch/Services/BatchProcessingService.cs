using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using NEXUS3D.Conversion.Abstractions.Interfaces;
using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Domain.Entities;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Repositories;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.Conversion.Batch.Services;

/// <summary>
/// Service coordinating the creation, priority scheduling, execution, cancellation, and persistence of multi-model batch conversions.
/// </summary>
public sealed class BatchProcessingService : IBatchProcessingService
{
    private readonly IJobPriorityQueue _priorityQueue;
    private readonly IBatchWorker _worker;
    private readonly IBatchProgressReporter _progressReporter;
    private readonly IBatchJobRepository? _repository;
    private readonly ILogger<BatchProcessingService> _logger;

    private readonly ConcurrentDictionary<Guid, BatchJob> _jobs = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _activeCts = new();

    public BatchProcessingService(
        IJobPriorityQueue priorityQueue,
        IBatchWorker worker,
        IBatchProgressReporter progressReporter,
        ILogger<BatchProcessingService> logger,
        IBatchJobRepository? repository = null)
    {
        _priorityQueue = Guard.NotNull(priorityQueue);
        _worker = Guard.NotNull(worker);
        _progressReporter = Guard.NotNull(progressReporter);
        _logger = Guard.NotNull(logger);
        _repository = repository;
    }

    public async Task<BatchJob> CreateJobAsync(
        BatchJobRequest request,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);
        Guard.NotNullOrWhiteSpace(request.JobName);
        Guard.NotNullOrWhiteSpace(request.OutputDirectory);

        var filePaths = new List<string>();

        // Add explicit files
        if (request.InputFiles != null && request.InputFiles.Count > 0)
        {
            filePaths.AddRange(request.InputFiles.Where(File.Exists));
        }

        // Add files from directory if specified
        if (!string.IsNullOrWhiteSpace(request.InputDirectory) && Directory.Exists(request.InputDirectory))
        {
            var filters = request.ExtensionFilter ?? [".obj", ".schem", ".schematic", ".json", ".geo.json", ".voxel.json"];
            var dirFiles = Directory.EnumerateFiles(request.InputDirectory, "*.*", SearchOption.AllDirectories)
                .Where(f => filters.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));
            filePaths.AddRange(dirFiles);
        }

        filePaths = filePaths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var job = new BatchJob(
            jobName: request.JobName,
            outputDirectory: request.OutputDirectory,
            targetFormat: request.TargetFormat,
            priority: request.Priority,
            sourceFormat: request.SourceFormat,
            inputDirectory: request.InputDirectory,
            options: request.Options,
            overwriteExisting: request.OverwriteExisting,
            continueOnError: request.ContinueOnError,
            retryLimit: request.RetryLimit,
            maxConcurrency: request.MaxConcurrency);

        string targetExt = GetTargetExtension(request.TargetFormat);

        foreach (var sourceFile in filePaths)
        {
            string baseName = Path.GetFileNameWithoutExtension(sourceFile);
            if (baseName.EndsWith(".geo", StringComparison.OrdinalIgnoreCase) ||
                baseName.EndsWith(".voxel", StringComparison.OrdinalIgnoreCase))
            {
                baseName = Path.GetFileNameWithoutExtension(baseName);
            }

            string targetFile = Path.Combine(request.OutputDirectory, $"{baseName}{targetExt}");
            var sourceFmt = request.SourceFormat ?? DetectFormat(sourceFile);
            long size = File.Exists(sourceFile) ? new FileInfo(sourceFile).Length : 0;

            var item = new BatchJobItem(job.JobId, sourceFile, targetFile, sourceFmt, request.TargetFormat, size);
            job.Items.Add(item);
        }

        _jobs[job.JobId] = job;

        if (_repository is not null)
        {
            try
            {
                var record = MapToRecord(job);
                await _repository.AddJobAsync(record, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist new batch job '{JobId}' to database.", job.JobId);
            }
        }

        _logger.LogInformation("Created BatchJob '{JobName}' ({JobId}) with {Count} files.", job.JobName, job.JobId, job.TotalFiles);
        return job;
    }

    public async Task<BatchJobResult> StartJobAsync(
        Guid jobId,
        IProgress<BatchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var job = await GetJobAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            throw new KeyNotFoundException($"BatchJob with ID '{jobId}' was not found.");
        }

        try
        {
            await _priorityQueue.EnqueueAsync(job, cancellationToken).ConfigureAwait(false);
            var dequeuedJob = await _priorityQueue.DequeueAsync(cancellationToken).ConfigureAwait(false);
            var activeJob = dequeuedJob ?? job;

            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _activeCts[activeJob.JobId] = cts;

            try
            {
                var result = await _worker.ProcessJobAsync(activeJob, progress, cts.Token).ConfigureAwait(false);

                if (_repository is not null)
                {
                    try
                    {
                        var record = MapToRecord(activeJob);
                        await _repository.UpdateJobAsync(record, CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to update batch job '{JobId}' in database.", activeJob.JobId);
                    }
                }

                return result;
            }
            finally
            {
                _activeCts.TryRemove(activeJob.JobId, out _);
            }
        }
        catch (OperationCanceledException)
        {
            job.Status = BatchJobStatus.Cancelled;
            job.ErrorMessage = "Batch job was cancelled.";
            job.CompletedAtUtc = DateTimeOffset.UtcNow;

            if (_repository is not null)
            {
                try
                {
                    var record = MapToRecord(job);
                    await _repository.UpdateJobAsync(record, CancellationToken.None).ConfigureAwait(false);
                }
                catch { }
            }

            return new BatchJobResult(
                job.JobId,
                job.JobName,
                BatchJobStatus.Cancelled,
                job.TotalFiles,
                job.SucceededFiles,
                job.FailedFiles,
                job.SkippedFiles,
                job.TotalDurationMs,
                job.Items.ToList(),
                "Batch job was cancelled.");
        }
    }

    public async Task CancelJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (_activeCts.TryGetValue(jobId, out var cts))
        {
            cts.Cancel();
        }

        await _priorityQueue.RemoveAsync(jobId, cancellationToken).ConfigureAwait(false);

        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.Status = BatchJobStatus.Cancelled;
            job.ErrorMessage = "Job was cancelled by user request.";
            job.CompletedAtUtc = DateTimeOffset.UtcNow;

            if (_repository is not null)
            {
                try
                {
                    var record = MapToRecord(job);
                    await _repository.UpdateJobAsync(record, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update cancelled batch job '{JobId}' in database.", jobId);
                }
            }
        }

        _logger.LogInformation("Cancelled BatchJob '{JobId}'.", jobId);
    }

    public Task PauseJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (_jobs.TryGetValue(jobId, out var job) && job.Status == BatchJobStatus.Running)
        {
            job.Status = BatchJobStatus.Paused;
            _logger.LogInformation("BatchJob '{JobId}' paused.", jobId);
        }

        return Task.CompletedTask;
    }

    public Task ResumeJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (_jobs.TryGetValue(jobId, out var job) && job.Status == BatchJobStatus.Paused)
        {
            job.Status = BatchJobStatus.Running;
            _logger.LogInformation("BatchJob '{JobId}' resumed.", jobId);
        }

        return Task.CompletedTask;
    }

    public async Task<BatchJob?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (_jobs.TryGetValue(jobId, out var cachedJob))
        {
            return cachedJob;
        }

        if (_repository is not null)
        {
            var record = await _repository.GetJobByIdAsync(jobId, cancellationToken).ConfigureAwait(false);
            if (record is not null)
            {
                var loadedJob = MapFromRecord(record);
                _jobs[loadedJob.JobId] = loadedJob;
                return loadedJob;
            }
        }

        return null;
    }

    public async Task<IReadOnlyList<BatchJob>> GetJobsAsync(CancellationToken cancellationToken = default)
    {
        if (_repository is not null)
        {
            try
            {
                var records = await _repository.GetRecentJobsAsync(100, cancellationToken).ConfigureAwait(false);
                foreach (var rec in records)
                {
                    if (!_jobs.ContainsKey(rec.Id))
                    {
                        _jobs[rec.Id] = MapFromRecord(rec);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query recent batch jobs from database.");
            }
        }

        return _jobs.Values.OrderByDescending(j => j.CreatedAtUtc).ToList();
    }

    public async Task<BatchJobResult> RetryFailedItemsAsync(
        Guid jobId,
        IProgress<BatchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var job = await GetJobAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            throw new KeyNotFoundException($"BatchJob '{jobId}' not found for retry.");
        }

        var failedItems = job.Items.Where(i => i.Status is BatchItemStatus.Failed).ToList();
        if (failedItems.Count == 0)
        {
            return new BatchJobResult(job.JobId, job.JobName, job.Status, job.TotalFiles, job.SucceededFiles, 0, job.SkippedFiles, 0, job.Items, null);
        }

        foreach (var item in failedItems)
        {
            item.Status = BatchItemStatus.Pending;
            item.ErrorMessage = null;
            item.ErrorCode = null;
        }

        job.Status = BatchJobStatus.Queued;
        return await StartJobAsync(jobId, progress, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        await CancelJobAsync(jobId, cancellationToken).ConfigureAwait(false);
        _jobs.TryRemove(jobId, out _);

        if (_repository is not null)
        {
            try
            {
                await _repository.DeleteJobAsync(jobId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete batch job '{JobId}' from database.", jobId);
            }
        }

        _logger.LogInformation("Deleted BatchJob '{JobId}'.", jobId);
    }

    private static string GetTargetExtension(ModelFormatType format) => format switch
    {
        ModelFormatType.Obj => ".obj",
        ModelFormatType.Schematic => ".schem",
        ModelFormatType.VoxelJson => ".voxel.json",
        ModelFormatType.BedrockGeometry => ".geo.json",
        _ => ".obj"
    };

    private static ModelFormatType DetectFormat(string path)
    {
        string name = Path.GetFileName(path).ToLowerInvariant();
        if (name.EndsWith(".geo.json", StringComparison.OrdinalIgnoreCase)) return ModelFormatType.BedrockGeometry;
        if (name.EndsWith(".voxel.json", StringComparison.OrdinalIgnoreCase)) return ModelFormatType.VoxelJson;

        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".obj" => ModelFormatType.Obj,
            ".schem" or ".schematic" => ModelFormatType.Schematic,
            ".json" => ModelFormatType.VoxelJson,
            _ => ModelFormatType.Unknown
        };
    }

    public static BatchJobRecord MapToRecord(BatchJob job)
    {
        var record = new BatchJobRecord(
            id: job.JobId,
            jobName: job.JobName,
            outputDirectory: job.OutputDirectory,
            targetFormat: job.TargetFormat,
            priority: job.Priority,
            sourceFormat: job.SourceFormat,
            inputDirectory: job.InputDirectory,
            overwriteExisting: job.OverwriteExisting,
            continueOnError: job.ContinueOnError,
            retryLimit: job.RetryLimit,
            maxConcurrency: job.MaxConcurrency)
        {
            Status = job.Status,
            TotalFiles = job.TotalFiles,
            SucceededFiles = job.SucceededFiles,
            FailedFiles = job.FailedFiles,
            SkippedFiles = job.SkippedFiles,
            DurationMs = job.TotalDurationMs,
            ErrorMessage = job.ErrorMessage,
            StartedAtUtc = job.StartedAtUtc,
            CompletedAtUtc = job.CompletedAtUtc
        };

        foreach (var item in job.Items)
        {
            var itemRec = new BatchJobItemRecord(
                id: item.ItemId,
                jobId: item.JobId,
                sourceFilePath: item.SourceFilePath,
                targetFilePath: item.TargetFilePath,
                sourceFormat: item.SourceFormat,
                targetFormat: item.TargetFormat,
                sourceSizeBytes: item.SourceSizeBytes)
            {
                Status = item.Status,
                ErrorMessage = item.ErrorMessage,
                ErrorCode = item.ErrorCode,
                RetryCount = item.RetryCount,
                DurationMs = item.DurationMs,
                TargetSizeBytes = item.TargetSizeBytes,
                CompletedAtUtc = item.CompletedAtUtc
            };
            record.Items.Add(itemRec);
        }

        return record;
    }

    public static BatchJob MapFromRecord(BatchJobRecord record)
    {
        var job = new BatchJob(
            jobName: record.JobName,
            outputDirectory: record.OutputDirectory,
            targetFormat: record.TargetFormat,
            priority: record.Priority,
            sourceFormat: record.SourceFormat,
            inputDirectory: record.InputDirectory,
            overwriteExisting: record.OverwriteExisting,
            continueOnError: record.ContinueOnError,
            retryLimit: record.RetryLimit,
            maxConcurrency: record.MaxConcurrency)
        {
            JobId = record.Id,
            Status = record.Status,
            StartedAtUtc = record.StartedAtUtc,
            CompletedAtUtc = record.CompletedAtUtc,
            ErrorMessage = record.ErrorMessage,
            CreatedAtUtc = record.CreatedAtUtc
        };

        foreach (var itemRec in record.Items)
        {
            var item = new BatchJobItem(
                jobId: itemRec.JobId,
                sourceFilePath: itemRec.SourceFilePath,
                targetFilePath: itemRec.TargetFilePath,
                sourceFormat: itemRec.SourceFormat,
                targetFormat: itemRec.TargetFormat,
                sourceSizeBytes: itemRec.SourceSizeBytes)
            {
                ItemId = itemRec.Id,
                Status = itemRec.Status,
                ErrorMessage = itemRec.ErrorMessage,
                ErrorCode = itemRec.ErrorCode,
                RetryCount = itemRec.RetryCount,
                DurationMs = itemRec.DurationMs,
                TargetSizeBytes = itemRec.TargetSizeBytes,
                CreatedAtUtc = itemRec.CreatedAtUtc,
                CompletedAtUtc = itemRec.CompletedAtUtc
            };
            job.Items.Add(item);
        }

        return job;
    }
}
