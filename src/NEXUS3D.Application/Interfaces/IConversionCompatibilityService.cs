using NEXUS3D.Application.Models;
using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Scene;

namespace NEXUS3D.Application.Interfaces;

/// <summary>
/// Service assessing cross-format conversion capabilities, generating fidelity loss diagnostics,
/// and filtering target formats based on active scene geometry.
/// </summary>
public interface IConversionCompatibilityService
{
    /// <summary>
    /// Analyzes the active scene and returns all exportable target formats rated by compatibility.
    /// </summary>
    IReadOnlyList<TargetFormatCompatibility> GetCompatibleTargets(Scene3D scene, ModelFormatType sourceFormat);

    /// <summary>
    /// Generates a comprehensive data loss report for a specific source and target pair.
    /// </summary>
    ConversionLossReport GenerateLossReport(Scene3D scene, ModelFormatType sourceFormat, ModelFormatType targetFormat);
}
