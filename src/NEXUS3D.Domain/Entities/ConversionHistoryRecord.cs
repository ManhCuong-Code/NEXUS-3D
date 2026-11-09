using NEXUS3D.Domain.Enums;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.Domain.Entities;

/// <summary>
/// Represents an audited record of a model conversion task stored in SQLite.
/// </summary>
public sealed class ConversionHistoryRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string JobName { get; init; } = string.Empty;
    public string SourceFilePath { get; init; } = string.Empty;
    public string TargetFilePath { get; init; } = string.Empty;
    public ModelFormatType SourceFormat { get; init; }
    public ModelFormatType TargetFormat { get; init; }
    public Guid? ProfileId { get; init; }
    public ConversionStatus Status { get; private set; }
    public long SourceSizeBytes { get; init; }
    public long OutputSizeBytes { get; private set; }
    public long DurationMs { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public ConversionHistoryRecord()
    {
    }

    public ConversionHistoryRecord(
        string jobName,
        string sourceFilePath,
        string targetFilePath,
        ModelFormatType sourceFormat,
        ModelFormatType targetFormat,
        long sourceSizeBytes,
        Guid? profileId = null)
    {
        JobName = Guard.NotNullOrWhiteSpace(jobName);
        SourceFilePath = Guard.NotNullOrWhiteSpace(sourceFilePath);
        TargetFilePath = Guard.NotNullOrWhiteSpace(targetFilePath);
        SourceFormat = sourceFormat;
        TargetFormat = targetFormat;
        SourceSizeBytes = Guard.NonNegative(sourceSizeBytes);
        ProfileId = profileId;
        Status = ConversionStatus.Pending;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkCompleted(long outputSizeBytes, long durationMs)
    {
        OutputSizeBytes = Guard.NonNegative(outputSizeBytes);
        DurationMs = Guard.NonNegative(durationMs);
        Status = ConversionStatus.Completed;
        ErrorMessage = null;
        CompletedAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkFailed(string errorMessage, long durationMs)
    {
        ErrorMessage = Guard.NotNullOrWhiteSpace(errorMessage);
        DurationMs = Guard.NonNegative(durationMs);
        Status = ConversionStatus.Failed;
        CompletedAtUtc = DateTimeOffset.UtcNow;
    }
}

