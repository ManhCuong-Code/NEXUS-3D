using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Metadata;
using NEXUS3D.Domain.Scene;

namespace NEXUS3D.Formats.Schematic.Common;

/// <summary>
/// Result of reading a schematic stream into both <see cref="SchematicDocument"/> and USM <see cref="Scene3D"/>.
/// </summary>
public sealed record SchematicReadResult
{
    public bool IsSuccess { get; init; }
    public SchematicFormatId DetectedFormat { get; init; }
    public SchematicDocument? Document { get; init; }
    public Scene3D? Scene { get; init; }
    public IReadOnlyList<WarningRecord> Warnings { get; init; } = [];
    public LossReport LossReport { get; init; } = new();
    public Error? Error { get; init; }

    public static SchematicReadResult Success(
        SchematicFormatId format,
        SchematicDocument document,
        Scene3D scene,
        IReadOnlyList<WarningRecord>? warnings = null,
        LossReport? lossReport = null) =>
        new()
        {
            IsSuccess = true,
            DetectedFormat = format,
            Document = document,
            Scene = scene,
            Warnings = warnings ?? [],
            LossReport = lossReport ?? new LossReport()
        };

    public static SchematicReadResult Failure(
        Error error,
        SchematicFormatId detectedFormat = SchematicFormatId.Unknown,
        IReadOnlyList<WarningRecord>? warnings = null) =>
        new()
        {
            IsSuccess = false,
            DetectedFormat = detectedFormat,
            Error = error,
            Warnings = warnings ?? []
        };
}

