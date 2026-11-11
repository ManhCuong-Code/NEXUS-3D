using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Formats.Abstractions;

namespace NEXUS3D.Application.Models;

/// <summary>
/// Fidelity rating assessing the structural compatibility between a source model and candidate target format.
/// </summary>
public enum CompatibilityRating
{
    NativeLossless = 0,
    HighFidelity = 1,
    ModerateApproximation = 2,
    LossyStructure = 3
}

/// <summary>
/// Detailed evaluation of an available conversion destination format for an active 3D model.
/// </summary>
public sealed class TargetFormatCompatibility
{
    public required ModelFormatType FormatType { get; init; }
    public required string DisplayName { get; init; }
    public required string ShortCode { get; init; }
    public required string PrimaryExtension { get; init; }
    public required IFormatDescriptor Descriptor { get; init; }

    public CompatibilityRating Rating { get; init; }
    public string RatingBadge => Rating switch
    {
        CompatibilityRating.NativeLossless => "Lossless (100%)",
        CompatibilityRating.HighFidelity => "High Fidelity (~95%)",
        CompatibilityRating.ModerateApproximation => "Approximated (~80%)",
        CompatibilityRating.LossyStructure => "Lossy Structure (~50%)",
        _ => "Standard"
    };

    public string RatingColor => Rating switch
    {
        CompatibilityRating.NativeLossless => "#00CEC9",
        CompatibilityRating.HighFidelity => "#55EFC4",
        CompatibilityRating.ModerateApproximation => "#FDCB6E",
        CompatibilityRating.LossyStructure => "#FF7675",
        _ => "#A29BFE"
    };

    public required string RatingDescription { get; init; }
    public required ConversionLossReport LossReport { get; init; }

    public List<string> PreservedFeatures { get; init; } = [];
    public List<string> ApproximateOrLostFeatures { get; init; } = [];
    public bool IsRecommended { get; init; }
}
