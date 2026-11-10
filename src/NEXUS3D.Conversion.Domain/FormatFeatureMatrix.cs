using NEXUS3D.Conversion.Abstractions;
using NEXUS3D.Conversion.Abstractions.Interfaces;
using NEXUS3D.Domain.Enums;

namespace NEXUS3D.Conversion.Domain;

/// <summary>
/// Authoritative catalog of format capabilities and supported features across NEXUS-3D.
/// </summary>
public static class FormatFeatureMatrix
{
    private static readonly Dictionary<ModelFormatType, FormatCapabilityDescriptor> Capabilities = new()
    {
        [ModelFormatType.Schematic] = new FormatCapabilityDescriptor(
            FormatType: ModelFormatType.Schematic,
            DisplayName: "Minecraft Schematic (Sponge v2/v3, Legacy)",
            SupportedFeatures: FormatFeature.VoxelGrid |
                               FormatFeature.CustomNbtMetadata |
                               FormatFeature.MultiMaterials,
            NativeGeometryKind: GeometryKind.VoxelGrid,
            SupportsLosslessRoundTripWithSelf: true,
            FileExtensions: [".schem", ".schematic"]
        ),

        [ModelFormatType.Obj] = new FormatCapabilityDescriptor(
            FormatType: ModelFormatType.Obj,
            DisplayName: "Wavefront OBJ & MTL",
            SupportedFeatures: FormatFeature.PolygonMesh |
                               FormatFeature.DiffuseTextures |
                               FormatFeature.PerFaceUv |
                               FormatFeature.MultiMaterials |
                               FormatFeature.SmoothingGroups |
                               FormatFeature.VertexColors,
            NativeGeometryKind: GeometryKind.PolygonMesh,
            SupportsLosslessRoundTripWithSelf: true,
            FileExtensions: [".obj", ".mtl"]
        ),

        [ModelFormatType.VoxelJson] = new FormatCapabilityDescriptor(
            FormatType: ModelFormatType.VoxelJson,
            DisplayName: "Voxel JSON (Nexus v1, Sparse, Block Cuboids)",
            SupportedFeatures: FormatFeature.VoxelGrid |
                               FormatFeature.MultiMaterials |
                               FormatFeature.VertexColors |
                               FormatFeature.CustomProperties,
            NativeGeometryKind: GeometryKind.VoxelGrid,
            SupportsLosslessRoundTripWithSelf: true,
            FileExtensions: [".voxel.json", ".json"]
        ),

        [ModelFormatType.BedrockGeometry] = new FormatCapabilityDescriptor(
            FormatType: ModelFormatType.BedrockGeometry,
            DisplayName: "Minecraft Bedrock Geometry JSON",
            SupportedFeatures: FormatFeature.SkeletalHierarchy |
                               FormatFeature.BoneLocators |
                               FormatFeature.BoxUv |
                               FormatFeature.PerFaceUv |
                               FormatFeature.DiffuseTextures |
                               FormatFeature.CustomProperties,
            NativeGeometryKind: GeometryKind.CuboidHierarchy,
            SupportsLosslessRoundTripWithSelf: true,
            FileExtensions: [".geo.json", ".json"]
        )
    };

    public static FormatCapabilityDescriptor Get(ModelFormatType format)
    {
        if (Capabilities.TryGetValue(format, out var descriptor))
        {
            return descriptor;
        }

        return new FormatCapabilityDescriptor(
            FormatType: format,
            DisplayName: format.ToString(),
            SupportedFeatures: FormatFeature.None,
            NativeGeometryKind: GeometryKind.Unknown,
            SupportsLosslessRoundTripWithSelf: false,
            FileExtensions: []
        );
    }

    public static IReadOnlyList<FormatCapabilityDescriptor> GetAll() => Capabilities.Values.ToList();

    public static bool Supports(ModelFormatType format, FormatFeature feature)
    {
        var cap = Get(format);
        return (cap.SupportedFeatures & feature) == feature;
    }
}
