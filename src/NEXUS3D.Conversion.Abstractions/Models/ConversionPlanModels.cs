using NEXUS3D.Domain.Enums;

namespace NEXUS3D.Conversion.Abstractions.Models;

/// <summary>
/// An atomic execution step in an orchestrated conversion plan.
/// </summary>
public sealed record ConversionStep(
    int Order,
    ConversionStepType StepType,
    string Title,
    string Description,
    bool IsRequired);

/// <summary>
/// Pre-calculated execution plan detailing each stage of transformation between formats through USM.
/// </summary>
public sealed class ConversionPlan
{
    public ModelFormatType SourceFormat { get; init; }
    public ModelFormatType TargetFormat { get; init; }
    public List<ConversionStep> Steps { get; init; } = [];
    public ConversionLossReport LossReport { get; init; } = new();
    public string StrategySummary { get; init; } = string.Empty;
}
