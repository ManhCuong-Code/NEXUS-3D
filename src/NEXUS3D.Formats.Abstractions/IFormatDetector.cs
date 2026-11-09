using NEXUS3D.Domain.Enums;

namespace NEXUS3D.Formats.Abstractions;

/// <summary>
/// Result of inspecting a stream or file header to detect its concrete format and version.
/// </summary>
public sealed record FormatDetectionResult(
    bool IsRecognized,
    ModelFormatType FormatType,
    string FormatId,
    int Version,
    string? Details = null);

/// <summary>
/// Detects whether a binary or text stream matches a supported 3D/schematic format based on content inspection.
/// </summary>
public interface IFormatDetector
{
    Task<FormatDetectionResult> DetectAsync(
        Stream source,
        string? fileExtensionHint = null,
        CancellationToken cancellationToken = default);
}

