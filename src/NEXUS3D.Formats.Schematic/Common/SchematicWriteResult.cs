using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Metadata;

namespace NEXUS3D.Formats.Schematic.Common;

/// <summary>
/// Result of exporting a <see cref="NEXUS3D.Domain.Scene.Scene3D"/> or <see cref="SchematicDocument"/> to a schematic stream/file.
/// Includes comprehensive <see cref="LossReport"/> and post-write verification statistics.
/// </summary>
public sealed record SchematicWriteResult
{
    public bool IsSuccess { get; init; }
    public SchematicFormatId TargetFormat { get; init; }
    public long BytesWritten { get; init; }
    public int ExportedBlockCount { get; init; }
    public int UnmappedBlockCount { get; init; }
    public int ExportedBlockEntityCount { get; init; }
    public int UnsupportedBlockEntityCount { get; init; }
    public bool VerifiedAfterWrite { get; init; }
    public LossReport LossReport { get; init; } = new();
    public IReadOnlyList<WarningRecord> Warnings { get; init; } = [];
    public Error? Error { get; init; }

    public static SchematicWriteResult Failure(
        Error error,
        SchematicFormatId targetFormat,
        LossReport? lossReport = null,
        IReadOnlyList<WarningRecord>? warnings = null) =>
        new()
        {
            IsSuccess = false,
            TargetFormat = targetFormat,
            Error = error,
            LossReport = lossReport ?? new LossReport(),
            Warnings = warnings ?? []
        };
}
