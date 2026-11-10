using NEXUS3D.Domain.Enums;

namespace NEXUS3D.Conversion.Abstractions.Models;

/// <summary>
/// Detailed record of a specific data construct, attribute, or property affected during conversion.
/// </summary>
public sealed record ConversionLossItem(
    string FeatureName,
    LossSeverity Severity,
    string Category,
    string Description,
    string SourceRepresentation,
    string TargetRepresentation,
    string RemediationAdvice);

/// <summary>
/// Comprehensive report aggregating all data fidelity evaluations and loss items for a conversion job.
/// </summary>
public sealed class ConversionLossReport
{
    public ModelFormatType SourceFormat { get; init; }
    public ModelFormatType TargetFormat { get; init; }
    public List<ConversionLossItem> Items { get; init; } = [];

    public LossSeverity OverallSeverity
    {
        get
        {
            if (Items.Count == 0) return LossSeverity.Supported;
            return Items.Max(x => x.Severity);
        }
    }

    public bool HasLoss => Items.Any(x => x.Severity is LossSeverity.Approximated or LossSeverity.Unmapped or LossSeverity.Lost);
    public bool RequiresConfirmation => Items.Any(x => x.Severity is LossSeverity.Unmapped or LossSeverity.Lost);

    public int PreservedCount => Items.Count(x => x.Severity is LossSeverity.Supported or LossSeverity.PreservedRaw);
    public int ApproximatedCount => Items.Count(x => x.Severity is LossSeverity.Approximated);
    public int UnmappedCount => Items.Count(x => x.Severity is LossSeverity.Unmapped);
    public int LostCount => Items.Count(x => x.Severity is LossSeverity.Lost);

    public static ConversionLossReport Empty(ModelFormatType source, ModelFormatType target) =>
        new()
        {
            SourceFormat = source,
            TargetFormat = target
        };
}
