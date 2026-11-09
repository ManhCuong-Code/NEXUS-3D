using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Metadata;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Validation;
using NEXUS3D.Formats.Obj.Common;

namespace NEXUS3D.Formats.Obj.Parsing;

/// <summary>
/// Result of parsing a Wavefront OBJ stream/file (and optional companion MTL libraries) into
/// both <see cref="ObjDocument"/> and USM <see cref="Scene3D"/>.
/// </summary>
public sealed record ObjParseResult
{
    public bool IsSuccess { get; init; }
    public ObjDocument? Document { get; init; }
    public Scene3D? Scene { get; init; }
    public ValidationResult Validation { get; init; } = ValidationResult.Success();
    public IReadOnlyList<WarningRecord> Warnings { get; init; } = [];
    public LossReport LossReport { get; init; } = new();
    public Error? Error { get; init; }

    public static ObjParseResult Success(
        ObjDocument document,
        Scene3D? scene = null,
        ValidationResult? validation = null,
        IReadOnlyList<WarningRecord>? warnings = null,
        LossReport? lossReport = null) =>
        new()
        {
            IsSuccess = true,
            Document = document,
            Scene = scene,
            Validation = validation ?? ValidationResult.Success(),
            Warnings = warnings ?? document.Warnings,
            LossReport = lossReport ?? new LossReport()
        };

    public static ObjParseResult Failure(
        Error error,
        ValidationResult? validation = null,
        IReadOnlyList<WarningRecord>? warnings = null) =>
        new()
        {
            IsSuccess = false,
            Error = error,
            Validation = validation ?? ValidationResult.Success(),
            Warnings = warnings ?? []
        };
}

