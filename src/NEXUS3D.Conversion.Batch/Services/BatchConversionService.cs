using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NEXUS3D.Conversion.Abstractions.Interfaces;
using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.Conversion.Batch.Services;

/// <summary>
/// Service managing high-throughput, bounded-concurrency batch conversions across multi-format 3D models.
/// </summary>
public sealed class BatchConversionService : IBatchConversionService
{
    private readonly IUniversalConversionPipeline _pipeline;
    private readonly ILogger<BatchConversionService> _logger;

    public BatchConversionService(
        IUniversalConversionPipeline pipeline,
        ILogger<BatchConversionService> logger)
    {
        _pipeline = Guard.NotNull(pipeline);
        _logger = Guard.NotNull(logger);
    }

    public async Task<BatchConversionResult> ConvertBatchAsync(
        BatchConversionRequest request,
        IProgress<BatchProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);

        int totalJobs = request.Jobs.Count;
        if (totalJobs == 0)
        {
            return new BatchConversionResult(0, 0, 0, 0, []);
        }

        int maxConcurrency = Math.Clamp(request.MaxConcurrency, 1, Math.Max(2, Environment.ProcessorCount));
        using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

        var stopwatch = Stopwatch.StartNew();
        var results = new ConcurrentBag<ConversionResult>();
        int completedCount = 0;

        _logger.LogInformation(
            "Starting batch conversion of {TotalJobs} models (MaxConcurrency: {Concurrency}, ContinueOnError: {ContinueOnError}).",
            totalJobs,
            maxConcurrency,
            request.ContinueOnError);

        var tasks = request.Jobs.Select(async jobRequest =>
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                string currentFile = Path.GetFileName(jobRequest.SourceFilePath);
                _logger.LogInformation("Batch worker processing '{FileName}'...", currentFile);

                ConversionResult result = await _pipeline.ExecuteAsync(jobRequest, null, cancellationToken).ConfigureAwait(false);
                results.Add(result);

                int done = Interlocked.Increment(ref completedCount);
                double percent = (double)done / totalJobs * 100.0;

                progress?.Report(new BatchProgressReport(
                    CompletedJobs: done,
                    TotalJobs: totalJobs,
                    PercentComplete: percent,
                    CurrentFileName: currentFile,
                    LastJobSucceeded: result.IsSuccess));

                if (!result.IsSuccess && !request.ContinueOnError)
                {
                    _logger.LogWarning("Batch execution halted on error due to ContinueOnError=false: {Error}", result.ErrorMessage);
                    throw new InvalidOperationException($"Batch halted on file '{currentFile}': {result.ErrorMessage}");
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
        catch (Exception ex) when (request.ContinueOnError && ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "One or more batch tasks completed with exceptions while ContinueOnError is true.");
        }

        stopwatch.Stop();
        var orderedResults = results.OrderBy(r => r.SourceFilePath, StringComparer.OrdinalIgnoreCase).ToList();
        int succeeded = orderedResults.Count(r => r.IsSuccess);
        int failed = orderedResults.Count(r => !r.IsSuccess);

        _logger.LogInformation(
            "Batch conversion finished in {Duration}ms: {Succeeded}/{Total} succeeded, {Failed} failed.",
            stopwatch.ElapsedMilliseconds,
            succeeded,
            totalJobs,
            failed);

        return new BatchConversionResult(
            TotalJobs: totalJobs,
            SucceededJobs: succeeded,
            FailedJobs: failed,
            TotalDurationMs: stopwatch.ElapsedMilliseconds,
            Results: orderedResults);
    }
}
