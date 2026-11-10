using NEXUS3D.Domain.Enums;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.Conversion.Abstractions.Models;

/// <summary>
/// Status and execution details of a single file in a batch conversion job.
/// </summary>
public sealed class BatchJobItem
{
    public Guid ItemId { get; init; } = Guid.NewGuid();
    public Guid JobId { get; init; }
    public string SourceFilePath { get; init; }
    public string TargetFilePath { get; set; }
    public ModelFormatType SourceFormat { get; set; }
    public ModelFormatType TargetFormat { get; set; }
    public BatchItemStatus Status { get; set; } = BatchItemStatus.Pending;
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
    public int RetryCount { get; set; }
    public long DurationMs { get; set; }
    public long SourceSizeBytes { get; set; }
    public long TargetSizeBytes { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAtUtc { get; set; }

    public BatchJobItem(
        Guid jobId,
        string sourceFilePath,
        string targetFilePath,
        ModelFormatType sourceFormat,
        ModelFormatType targetFormat,
        long sourceSizeBytes = 0)
    {
        JobId = jobId;
        SourceFilePath = Guard.NotNullOrWhiteSpace(sourceFilePath);
        TargetFilePath = Guard.NotNullOrWhiteSpace(targetFilePath);
        SourceFormat = sourceFormat;
        TargetFormat = targetFormat;
        SourceSizeBytes = sourceSizeBytes;
    }

    public void MarkProcessing()
    {
        Status = BatchItemStatus.Processing;
    }

    public void MarkSucceeded(long targetSizeBytes, long durationMs)
    {
        Status = BatchItemStatus.Succeeded;
        TargetSizeBytes = Math.Max(0, targetSizeBytes);
        DurationMs = Math.Max(0, durationMs);
        ErrorMessage = null;
        ErrorCode = null;
        CompletedAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkFailed(string errorCode, string errorMessage, long durationMs)
    {
        Status = BatchItemStatus.Failed;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        DurationMs = Math.Max(0, durationMs);
        CompletedAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkSkipped(string reason)
    {
        Status = BatchItemStatus.Skipped;
        ErrorMessage = reason;
        CompletedAtUtc = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// Represents a high-level batch conversion job containing multiple files.
/// </summary>
public sealed class BatchJob
{
    public Guid JobId { get; init; } = Guid.NewGuid();
    public string JobName { get; init; }
    public string? InputDirectory { get; init; }
    public string OutputDirectory { get; init; }
    public ModelFormatType? SourceFormat { get; init; }
    public ModelFormatType TargetFormat { get; init; }
    public BatchJobPriority Priority { get; set; } = BatchJobPriority.Normal;
    public BatchJobStatus Status { get; set; } = BatchJobStatus.Pending;
    public ConversionOptions Options { get; init; } = ConversionOptions.Default;
    public bool OverwriteExisting { get; init; }
    public bool ContinueOnError { get; init; } = true;
    public int RetryLimit { get; init; } = 1;
    public int MaxConcurrency { get; init; } = 4;
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EnqueuedAtUtc { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public string? ErrorMessage { get; set; }

    public List<BatchJobItem> Items { get; init; } = [];

    public int TotalFiles => Items.Count;
    public int CompletedFiles => Items.Count(i => i.Status is BatchItemStatus.Succeeded or BatchItemStatus.Failed or BatchItemStatus.Skipped);
    public int SucceededFiles => Items.Count(i => i.Status == BatchItemStatus.Succeeded);
    public int FailedFiles => Items.Count(i => i.Status == BatchItemStatus.Failed);
    public int SkippedFiles => Items.Count(i => i.Status == BatchItemStatus.Skipped);
    public double PercentComplete => TotalFiles == 0 ? 100.0 : (double)CompletedFiles / TotalFiles * 100.0;
    public long TotalDurationMs => (long)((CompletedAtUtc ?? DateTimeOffset.UtcNow) - (StartedAtUtc ?? CreatedAtUtc)).TotalMilliseconds;

    public BatchJob(
        string jobName,
        string outputDirectory,
        ModelFormatType targetFormat,
        BatchJobPriority priority = BatchJobPriority.Normal,
        ModelFormatType? sourceFormat = null,
        string? inputDirectory = null,
        ConversionOptions? options = null,
        bool overwriteExisting = false,
        bool continueOnError = true,
        int retryLimit = 1,
        int maxConcurrency = 4)
    {
        JobName = Guard.NotNullOrWhiteSpace(jobName);
        OutputDirectory = Guard.NotNullOrWhiteSpace(outputDirectory);
        TargetFormat = targetFormat;
        Priority = priority;
        SourceFormat = sourceFormat;
        InputDirectory = inputDirectory;
        Options = options ?? ConversionOptions.Default;
        OverwriteExisting = overwriteExisting;
        ContinueOnError = continueOnError;
        RetryLimit = Math.Max(0, retryLimit);
        MaxConcurrency = Math.Max(1, maxConcurrency);
    }
}

/// <summary>
/// Request parameter object to initialize a batch job.
/// </summary>
public sealed record BatchJobRequest(
    string JobName,
    IReadOnlyList<string> InputFiles,
    string OutputDirectory,
    ModelFormatType TargetFormat,
    BatchJobPriority Priority = BatchJobPriority.Normal,
    ModelFormatType? SourceFormat = null,
    string? InputDirectory = null,
    ConversionOptions? Options = null,
    bool OverwriteExisting = false,
    bool ContinueOnError = true,
    int RetryLimit = 1,
    int MaxConcurrency = 4,
    IReadOnlyList<string>? ExtensionFilter = null,
    string? TargetExtension = null);

/// <summary>
/// Live progress report for an executing batch job.
/// </summary>
public sealed record BatchProgress(
    Guid JobId,
    string JobName,
    int TotalFiles,
    int CompletedFiles,
    int SucceededFiles,
    int FailedFiles,
    int SkippedFiles,
    double PercentComplete,
    string CurrentFileName,
    BatchJobStatus Status,
    long ElapsedMs);

/// <summary>
/// Aggregated summary result of a completed or cancelled batch job.
/// </summary>
public sealed record BatchJobResult(
    Guid JobId,
    string JobName,
    BatchJobStatus Status,
    int TotalFiles,
    int SucceededFiles,
    int FailedFiles,
    int SkippedFiles,
    long DurationMs,
    IReadOnlyList<BatchJobItem> Items,
    string? ErrorMessage);

