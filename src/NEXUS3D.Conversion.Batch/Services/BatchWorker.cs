using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NEXUS3D.Conversion.Abstractions.Interfaces;
using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.Conversion.Batch.Services;

/// <summary>
/// Worker engine executing individual conversion tasks within a batch job under bounded concurrency.
/// Handles transient error retries, duplicate output policies, cancellation, and progress reporting.
/// </summary>
public sealed class BatchWorker : IBatchWorker
{
    private readonly IUniversalConversionPipeline _pipeline;
    private readonly IBatchProgressReporter _progressReporter;
    private readonly ILogger<BatchWorker> _logger;

    public BatchWorker(
        IUniversalConversionPipeline pipeline,
        IBatchProgressReporter progressReporter,
        ILogger<BatchWorker> logger)
    {
        _pipeline = Guard.NotNull(pipeline);
        _progressReporter = Guard.NotNull(progressReporter);
        _logger = Guard.NotNull(logger);
    }

    public async Task<BatchJobResult> ProcessJobAsync(
        BatchJob job,
        IProgress<BatchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(job);

        job.StartedAtUtc ??= DateTimeOffset.UtcNow;
        job.Status = BatchJobStatus.Running;

        int totalItems = job.Items.Count;
        if (totalItems == 0)
        {
            job.Status = BatchJobStatus.Completed;
            job.CompletedAtUtc = DateTimeOffset.UtcNow;
            return new BatchJobResult(job.JobId, job.JobName, job.Status, 0, 0, 0, 0, 0, [], null);
        }

        int maxConcurrency = Math.Clamp(job.MaxConcurrency, 1, Math.Max(2, Environment.ProcessorCount));
        using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var jobToken = linkedCts.Token;

        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation(
            "BatchWorker executing job '{JobName}' ({JobId}): {TotalItems} files, Concurrency: {Concurrency}, ContinueOnError: {ContinueOnError}.",
            job.JobName, job.JobId, totalItems, maxConcurrency, job.ContinueOnError);

        // Ensure output directory exists
        Directory.CreateDirectory(job.OutputDirectory);

        var tasks = job.Items.Select(async item =>
        {
            await semaphore.WaitAsync(jobToken).ConfigureAwait(false);
            try
            {
                jobToken.ThrowIfCancellationRequested();

                await ProcessItemWithRetryAsync(job, item, stopwatch, progress, jobToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (item.Status == BatchItemStatus.Pending || item.Status == BatchItemStatus.Processing)
                {
                    item.MarkFailed("CANCELLED", "Processing was cancelled by user.", 0);
                }
                throw;
            }
            catch (Exception ex)
            {
                item.MarkFailed("UNEXPECTED_ERROR", ex.Message, 0);
                _logger.LogError(ex, "Unexpected exception processing item '{Path}' in job '{JobName}'.", item.SourceFilePath, job.JobName);

                if (!job.ContinueOnError)
                {
                    _logger.LogWarning("Halting batch job '{JobName}' because ContinueOnError is false.", job.JobName);
                    try { linkedCts.Cancel(); } catch { }
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            job.Status = BatchJobStatus.Cancelled;
            job.ErrorMessage = "Batch job was cancelled.";
            _logger.LogWarning("Batch job '{JobName}' was cancelled.", job.JobName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Batch job '{JobName}' completed with unhandled task errors.", job.JobName);
        }

        stopwatch.Stop();
        job.CompletedAtUtc = DateTimeOffset.UtcNow;

        if (job.Status != BatchJobStatus.Cancelled)
        {
            if (job.FailedFiles == 0)
            {
                job.Status = BatchJobStatus.Completed;
            }
            else if (job.SucceededFiles > 0)
            {
                job.Status = BatchJobStatus.CompletedWithErrors;
            }
            else
            {
                job.Status = BatchJobStatus.Failed;
            }
        }

        var finalProgress = CreateProgress(job, string.Empty, stopwatch.ElapsedMilliseconds);
        progress?.Report(finalProgress);
        _progressReporter.Report(finalProgress);

        _logger.LogInformation(
            "BatchWorker finished job '{JobName}' in {Duration}ms. Status: {Status}, Succeeded: {Succeeded}, Failed: {Failed}, Skipped: {Skipped}.",
            job.JobName, stopwatch.ElapsedMilliseconds, job.Status, job.SucceededFiles, job.FailedFiles, job.SkippedFiles);

        return new BatchJobResult(
            job.JobId,
            job.JobName,
            job.Status,
            job.TotalFiles,
            job.SucceededFiles,
            job.FailedFiles,
            job.SkippedFiles,
            stopwatch.ElapsedMilliseconds,
            job.Items.ToList(),
            job.ErrorMessage);
    }

    private async Task ProcessItemWithRetryAsync(
        BatchJob job,
        BatchJobItem item,
        Stopwatch totalStopwatch,
        IProgress<BatchProgress>? progress,
        CancellationToken cancellationToken)
    {
        string fileName = Path.GetFileName(item.SourceFilePath);
        item.MarkProcessing();

        var initialProgress = CreateProgress(job, fileName, totalStopwatch.ElapsedMilliseconds);
        progress?.Report(initialProgress);
        _progressReporter.Report(initialProgress);

        // Validation 1: Source file exists
        if (!File.Exists(item.SourceFilePath))
        {
            item.MarkFailed("FILE_NOT_FOUND", $"Source file '{item.SourceFilePath}' does not exist.", 0);
            return;
        }

        // Validation 2: Check overwrite
        if (File.Exists(item.TargetFilePath) && !job.OverwriteExisting)
        {
            item.MarkSkipped("Target file already exists and OverwriteExisting is disabled.");
            _logger.LogInformation("Skipped '{FileName}': target already exists.", fileName);
            return;
        }

        // Resolve format if unknown
        if (item.SourceFormat == ModelFormatType.Unknown)
        {
            item.SourceFormat = DetectFormatFromExtension(item.SourceFilePath);
        }

        int attempt = 0;
        int maxAttempts = Math.Max(1, 1 + job.RetryLimit);

        while (attempt < maxAttempts)
        {
            attempt++;
            cancellationToken.ThrowIfCancellationRequested();

            var itemStopwatch = Stopwatch.StartNew();
            try
            {
                var request = new ConversionRequest(
                    SourceFilePath: item.SourceFilePath,
                    TargetFilePath: item.TargetFilePath,
                    SourceFormat: item.SourceFormat,
                    TargetFormat: item.TargetFormat,
                    Options: job.Options);

                ConversionResult result = await _pipeline.ExecuteAsync(request, null, cancellationToken).ConfigureAwait(false);
                itemStopwatch.Stop();

                if (result.IsSuccess)
                {
                    item.MarkSucceeded(result.TargetSizeBytes, itemStopwatch.ElapsedMilliseconds);
                    break;
                }

                // Failed attempt
                item.RetryCount = attempt - 1;
                if (attempt < maxAttempts)
                {
                    _logger.LogWarning("Transient failure converting '{FileName}' (Attempt {Attempt}/{Max}): {Error}. Retrying...",
                        fileName, attempt, maxAttempts, result.ErrorMessage);
                    await Task.Delay(150 * attempt, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    item.MarkFailed(result.ErrorCode ?? "CONVERSION_ERROR", result.ErrorMessage ?? "Conversion failed.", itemStopwatch.ElapsedMilliseconds);
                    if (!job.ContinueOnError)
                    {
                        throw new InvalidOperationException($"Batch halted on failure of '{fileName}': {result.ErrorMessage}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                itemStopwatch.Stop();
                _logger.LogWarning(ex, "Exception converting '{FileName}' (Attempt {Attempt}/{Max}). Retrying...", fileName, attempt, maxAttempts);
                await Task.Delay(150 * attempt, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                itemStopwatch.Stop();
                item.MarkFailed("CONVERSION_EXCEPTION", ex.Message, itemStopwatch.ElapsedMilliseconds);
                if (!job.ContinueOnError)
                {
                    throw;
                }
                break;
            }
        }

        var updatedProgress = CreateProgress(job, fileName, totalStopwatch.ElapsedMilliseconds);
        progress?.Report(updatedProgress);
        _progressReporter.Report(updatedProgress);
    }

    private static BatchProgress CreateProgress(BatchJob job, string currentFile, long elapsedMs)
    {
        return new BatchProgress(
            JobId: job.JobId,
            JobName: job.JobName,
            TotalFiles: job.TotalFiles,
            CompletedFiles: job.CompletedFiles,
            SucceededFiles: job.SucceededFiles,
            FailedFiles: job.FailedFiles,
            SkippedFiles: job.SkippedFiles,
            PercentComplete: job.PercentComplete,
            CurrentFileName: currentFile,
            Status: job.Status,
            ElapsedMs: elapsedMs);
    }

    private static ModelFormatType DetectFormatFromExtension(string path)
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
}

