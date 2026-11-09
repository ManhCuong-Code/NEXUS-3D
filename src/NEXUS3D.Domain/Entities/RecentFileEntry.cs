using NEXUS3D.Domain.Enums;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.Domain.Entities;

/// <summary>
/// Tracks recently opened or inspected 3D model files.
/// </summary>
public sealed class RecentFileEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string FilePath { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public ModelFormatType Format { get; init; }
    public long FileSizeBytes { get; private set; }
    public bool IsPinned { get; private set; }
    public int AccessCount { get; private set; } = 1;
    public DateTimeOffset LastOpenedAtUtc { get; private set; } = DateTimeOffset.UtcNow;

    public RecentFileEntry()
    {
    }

    public RecentFileEntry(
        string filePath,
        string fileName,
        ModelFormatType format,
        long fileSizeBytes,
        bool isPinned = false)
    {
        FilePath = Guard.NotNullOrWhiteSpace(filePath);
        FileName = Guard.NotNullOrWhiteSpace(fileName);
        Format = format;
        FileSizeBytes = Guard.NonNegative(fileSizeBytes);
        IsPinned = isPinned;
        AccessCount = 1;
        LastOpenedAtUtc = DateTimeOffset.UtcNow;
    }

    public void RecordAccess(long currentFileSizeBytes)
    {
        FileSizeBytes = Guard.NonNegative(currentFileSizeBytes);
        AccessCount++;
        LastOpenedAtUtc = DateTimeOffset.UtcNow;
    }

    public void SetPinned(bool isPinned) => IsPinned = isPinned;
}

