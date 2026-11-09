using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Metadata;
using NEXUS3D.Domain.Scene;

namespace NEXUS3D.Formats.Abstractions;

/// <summary>
/// Exporting result containing the loss/preservation report and output verification status.
/// </summary>
public sealed record FormatExportReport(
    bool IsSuccess,
    long BytesWritten,
    LossReport LossReport,
    IReadOnlyList<WarningRecord> Warnings,
    Error? Error = null);

/// <summary>
/// Exports a Universal Scene Model (<see cref="Scene3D"/>) to a target format stream or file.
/// </summary>
public interface IFormatExporter
{
    string FormatIdentifier { get; }

    Task<FormatExportReport> ExportSceneAsync(
        Scene3D scene,
        Stream destination,
        ExportOptions? options = null,
        CancellationToken cancellationToken = default);
}

