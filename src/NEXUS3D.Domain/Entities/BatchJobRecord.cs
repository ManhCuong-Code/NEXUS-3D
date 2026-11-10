using NEXUS3D.Domain.Enums;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.Domain.Entities;

/// <summary>
/// Audited record of an individual item inside a persistent batch job in SQLite.
/// </summary>
public sealed class BatchJobItemRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid JobId { get; init; }
    public string SourceFilePath { get; init; } = string.Empty;
    public string TargetFilePath { get; set; } = string.Empty;
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

    public BatchJobItemRecord()
    {
    }

    public BatchJobItemRecord(
        Guid id,
        Guid jobId,
        string sourceFilePath,
        string targetFilePath,
        ModelFormatType sourceFormat,
        ModelFormatType targetFormat,
        long sourceSizeBytes = 0)
    {
        Id = id;
        JobId = jobId;
        SourceFilePath = Guard.NotNullOrWhiteSpace(sourceFilePath);
        TargetFilePath = Guard.NotNullOrWhiteSpace(targetFilePath);
        SourceFormat = sourceFormat;
        TargetFormat = targetFormat;
        SourceSizeBytes = sourceSizeBytes;
        Status = BatchItemStatus.Pending;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// Audited database record of a batch processing job stored in SQLite.
/// </summary>
public sealed class BatchJobRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string JobName { get; init; } = string.Empty;
    public string? InputDirectory { get; init; }
    public string OutputDirectory { get; init; } = string.Empty;
    public ModelFormatType? SourceFormat { get; init; }
    public ModelFormatType TargetFormat { get; init; }
    public BatchJobPriority Priority { get; set; } = BatchJobPriority.Normal;
    public BatchJobStatus Status { get; set; } = BatchJobStatus.Pending;
    public int TotalFiles { get; set; }
    public int SucceededFiles { get; set; }
    public int FailedFiles { get; set; }
    public int SkippedFiles { get; set; }
    public bool OverwriteExisting { get; init; }
    public bool ContinueOnError { get; init; } = true;
    public int RetryLimit { get; init; } = 1;
    public int MaxConcurrency { get; init; } = 4;
    public long DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }

    public List<BatchJobItemRecord> Items { get; init; } = [];

    public BatchJobRecord()
    {
    }

    public BatchJobRecord(
        Guid id,
        string jobName,
        string outputDirectory,
        ModelFormatType targetFormat,
        BatchJobPriority priority = BatchJobPriority.Normal,
        ModelFormatType? sourceFormat = null,
        string? inputDirectory = null,
        bool overwriteExisting = false,
        bool continueOnError = true,
        int retryLimit = 1,
        int maxConcurrency = 4)
    {
        Id = id;
        JobName = Guard.NotNullOrWhiteSpace(jobName);
        OutputDirectory = Guard.NotNullOrWhiteSpace(outputDirectory);
        TargetFormat = targetFormat;
        Priority = priority;
        SourceFormat = sourceFormat;
        InputDirectory = inputDirectory;
        OverwriteExisting = overwriteExisting;
        ContinueOnError = continueOnError;
        RetryLimit = Math.Max(0, retryLimit);
        MaxConcurrency = Math.Max(1, maxConcurrency);
        Status = BatchJobStatus.Pending;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }
}
