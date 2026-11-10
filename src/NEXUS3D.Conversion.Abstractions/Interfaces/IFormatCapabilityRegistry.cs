using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Scene;

namespace NEXUS3D.Conversion.Abstractions.Interfaces;

/// <summary>
/// Detailed capabilities profile for a single format.
/// </summary>
public sealed record FormatCapabilityDescriptor(
    ModelFormatType FormatType,
    string DisplayName,
    FormatFeature SupportedFeatures,
    GeometryKind NativeGeometryKind,
    bool SupportsLosslessRoundTripWithSelf,
    IReadOnlyList<string> FileExtensions);

/// <summary>
/// Registry managing metadata, feature support, and loss matrices for all supported formats.
/// </summary>
public interface IFormatCapabilityRegistry
{
    FormatCapabilityDescriptor GetCapability(ModelFormatType format);
    bool SupportsFeature(ModelFormatType format, FormatFeature feature);
    ConversionLossReport EvaluateLoss(ModelFormatType source, ModelFormatType target, Scene3D? sampleScene = null);
    IReadOnlyList<FormatCapabilityDescriptor> GetAllCapabilities();
}
