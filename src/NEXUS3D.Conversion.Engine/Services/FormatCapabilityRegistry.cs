using Microsoft.Extensions.Logging;
using NEXUS3D.Conversion.Abstractions;
using NEXUS3D.Conversion.Abstractions.Interfaces;
using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Conversion.Domain;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.Conversion.Engine.Services;

/// <summary>
/// Authoritative implementation of <see cref="IFormatCapabilityRegistry"/> providing
/// feature reflection, cross-format compatibility analysis, and dynamic data loss auditing.
/// </summary>
public sealed class FormatCapabilityRegistry : IFormatCapabilityRegistry
{
    private readonly ILogger<FormatCapabilityRegistry> _logger;

    public FormatCapabilityRegistry(ILogger<FormatCapabilityRegistry> logger)
    {
        _logger = Guard.NotNull(logger);
    }

    public FormatCapabilityDescriptor GetCapability(ModelFormatType format) =>
        FormatFeatureMatrix.Get(format);

    public bool SupportsFeature(ModelFormatType format, FormatFeature feature) =>
        FormatFeatureMatrix.Supports(format, feature);

    public IReadOnlyList<FormatCapabilityDescriptor> GetAllCapabilities() =>
        FormatFeatureMatrix.GetAll();

    public ConversionLossReport EvaluateLoss(
        ModelFormatType source,
        ModelFormatType target,
        Scene3D? sampleScene = null)
    {
        var sourceCap = GetCapability(source);
        var targetCap = GetCapability(target);

        var report = new ConversionLossReport
        {
            SourceFormat = source,
            TargetFormat = target
        };

        if (source == target && source != ModelFormatType.Unknown)
        {
            report.Items.Add(new ConversionLossItem(
                FeatureName: "Identical Format",
                Severity: LossSeverity.Supported,
                Category: "Format",
                Description: "Source and target formats are identical. Full lossless round-trip expected.",
                SourceRepresentation: sourceCap.DisplayName,
                TargetRepresentation: targetCap.DisplayName,
                RemediationAdvice: "None. All features directly supported."));
            return report;
        }

        // 1. Geometry representations
        EvaluateGeometryRepresentation(sourceCap, targetCap, sampleScene, report);

        // 2. Skeletal & Rigging
        EvaluateSkeletalRigging(sourceCap, targetCap, sampleScene, report);

        // 3. Materials, Textures & Shading
        EvaluateMaterialsAndTextures(sourceCap, targetCap, sampleScene, report);

        // 4. Metadata, NBT & Custom Tags
        EvaluateMetadata(sourceCap, targetCap, sampleScene, report);

        // 5. Animations
        EvaluateAnimations(sourceCap, targetCap, sampleScene, report);

        _logger.LogInformation(
            "Loss evaluation for {Source} -> {Target}: {Preserved} preserved, {Approximated} approx, {Unmapped} unmapped, {Lost} lost.",
            source,
            target,
            report.PreservedCount,
            report.ApproximatedCount,
            report.UnmappedCount,
            report.LostCount);

        return report;
    }

    private static void EvaluateGeometryRepresentation(
        FormatCapabilityDescriptor sourceCap,
        FormatCapabilityDescriptor targetCap,
        Scene3D? sampleScene,
        ConversionLossReport report)
    {
        bool hasVoxelGrid = (sampleScene is not null && sampleScene.Graph.Traverse().Any(n => n.Object?.VoxelGrids.Count > 0)) ||
                            sourceCap.NativeGeometryKind == GeometryKind.VoxelGrid;

        bool hasMesh = (sampleScene is not null && sampleScene.Graph.Traverse().Any(n => n.Object?.Meshes.Count > 0)) ||
                       sourceCap.NativeGeometryKind == GeometryKind.PolygonMesh;

        bool hasCuboids = (sampleScene is not null && sampleScene.Skeletons.Count > 0 && sourceCap.FormatType == ModelFormatType.BedrockGeometry) ||
                          sourceCap.NativeGeometryKind == GeometryKind.CuboidHierarchy;

        // Voxel -> Mesh
        if (hasVoxelGrid && targetCap.NativeGeometryKind == GeometryKind.PolygonMesh)
        {
            report.Items.Add(new ConversionLossItem(
                FeatureName: "Volumetric Voxel Grid",
                Severity: LossSeverity.Approximated,
                Category: "Geometry",
                Description: "Voxel grid converted to surface polygon mesh using greedy/exposed meshing.",
                SourceRepresentation: "Discrete 3D VoxelGrid",
                TargetRepresentation: "Boundary Surface Polygons (MeshData)",
                RemediationAdvice: "Internal block states are culled; exterior boundary quads are generated."));
        }

        // Mesh -> Voxel
        if (hasMesh && (targetCap.NativeGeometryKind == GeometryKind.VoxelGrid || targetCap.FormatType is ModelFormatType.Schematic or ModelFormatType.VoxelJson))
        {
            report.Items.Add(new ConversionLossItem(
                FeatureName: "Continuous Surface Mesh",
                Severity: LossSeverity.Approximated,
                Category: "Geometry",
                Description: "Smooth polygon triangles are rasterized into discrete voxel grid cells.",
                SourceRepresentation: "Continuous MeshData (Triangles/Quads)",
                TargetRepresentation: "Discrete Block Grid (VoxelGrid)",
                RemediationAdvice: "Fine geometric details smaller than the voxel resolution will be quantized."));
        }

        // Bedrock Cuboid -> Mesh
        if (hasCuboids && targetCap.FormatType == ModelFormatType.Obj)
        {
            report.Items.Add(new ConversionLossItem(
                FeatureName: "Cuboid Element Hierarchy",
                Severity: LossSeverity.Approximated,
                Category: "Geometry",
                Description: "Bones and cuboid elements will be flattened into unified polygon meshes.",
                SourceRepresentation: "Bedrock Bone-Cuboid Hierarchy",
                TargetRepresentation: "Static Wavefront OBJ Meshes",
                RemediationAdvice: "Cube origins, sizes, and inflates are baked directly into vertex positions."));
        }

        // Bedrock Cuboid -> Voxel
        if (hasCuboids && (targetCap.FormatType is ModelFormatType.Schematic or ModelFormatType.VoxelJson))
        {
            report.Items.Add(new ConversionLossItem(
                FeatureName: "Cuboid Element Hierarchy",
                Severity: LossSeverity.Approximated,
                Category: "Geometry",
                Description: "Bedrock cuboids converted into block/voxel structures.",
                SourceRepresentation: "Bedrock Bone-Cuboid Hierarchy",
                TargetRepresentation: "Discrete Block Grid",
                RemediationAdvice: "Inflate parameters and arbitrary rotations may result in stepped voxelization."));
        }
    }

    private static void EvaluateSkeletalRigging(
        FormatCapabilityDescriptor sourceCap,
        FormatCapabilityDescriptor targetCap,
        Scene3D? sampleScene,
        ConversionLossReport report)
    {
        bool hasBones = (sampleScene?.Skeletons.Count > 0) ||
                        FormatFeatureMatrix.Supports(sourceCap.FormatType, FormatFeature.SkeletalHierarchy);

        bool targetSupportsBones = FormatFeatureMatrix.Supports(targetCap.FormatType, FormatFeature.SkeletalHierarchy);

        if (hasBones && !targetSupportsBones)
        {
            report.Items.Add(new ConversionLossItem(
                FeatureName: "Skeletal Hierarchy & Bone Rigging",
                Severity: LossSeverity.Unmapped,
                Category: "Rigging",
                Description: "Target format does not define skeletal bone hierarchies, pivots, or locators.",
                SourceRepresentation: "USM SkeletonData / BoneData",
                TargetRepresentation: "None (Bones baked or omitted)",
                RemediationAdvice: "Mesh vertices are converted in world space coordinates. Rigging data is discarded."));
        }
    }

    private static void EvaluateMaterialsAndTextures(
        FormatCapabilityDescriptor sourceCap,
        FormatCapabilityDescriptor targetCap,
        Scene3D? sampleScene,
        ConversionLossReport report)
    {
        bool hasPbr = (sampleScene?.Materials.Materials.Any(m => m.IsPbrMetallicRoughnessSpecified) == true) ||
                      FormatFeatureMatrix.Supports(sourceCap.FormatType, FormatFeature.PbrMaterials);

        bool targetSupportsPbr = FormatFeatureMatrix.Supports(targetCap.FormatType, FormatFeature.PbrMaterials);

        if (hasPbr && !targetSupportsPbr)
        {
            report.Items.Add(new ConversionLossItem(
                FeatureName: "PBR Material Workflow",
                Severity: LossSeverity.Approximated,
                Category: "Material",
                Description: "PBR Metallic-Roughness properties converted to legacy Phong Diffuse/Specular model.",
                SourceRepresentation: "PBR MetallicRoughness",
                TargetRepresentation: "Phong Diffuse/Specular (MTL)",
                RemediationAdvice: "BaseColor is mapped to diffuse reflectance; roughness is approximated to specular exponent."));
        }

        // Textures to Schematic / Block Palette
        if (targetCap.FormatType is ModelFormatType.Schematic or ModelFormatType.VoxelJson)
        {
            report.Items.Add(new ConversionLossItem(
                FeatureName: "Arbitrary UV Textures",
                Severity: LossSeverity.Approximated,
                Category: "Material",
                Description: "External image textures mapped to discrete Minecraft block palettes or voxel colors.",
                SourceRepresentation: "Texture Images (.png/.jpg) & UV Coordinates",
                TargetRepresentation: "Block State ID / Palette Index",
                RemediationAdvice: "Color quantization chooses closest matching block state or hex palette color."));
        }
    }

    private static void EvaluateMetadata(
        FormatCapabilityDescriptor sourceCap,
        FormatCapabilityDescriptor targetCap,
        Scene3D? sampleScene,
        ConversionLossReport report)
    {
        bool hasNbt = (sampleScene?.Metadata.Extensions.ContainsKey("Schematic_Nbt") == true) ||
                      sourceCap.FormatType == ModelFormatType.Schematic;

        bool targetSupportsNbt = FormatFeatureMatrix.Supports(targetCap.FormatType, FormatFeature.CustomNbtMetadata);

        if (hasNbt && !targetSupportsNbt)
        {
            report.Items.Add(new ConversionLossItem(
                FeatureName: "Minecraft NBT & TileEntities",
                Severity: LossSeverity.PreservedRaw,
                Category: "Metadata",
                Description: "Custom NBT block entities and tile ticks preserved in USM metadata container.",
                SourceRepresentation: "NBT Compound Tag",
                TargetRepresentation: "USM ExtensionData (Not exported to target file)",
                RemediationAdvice: "Retained in memory and USM scene files; target format does not support custom NBT."));
        }
    }

    private static void EvaluateAnimations(
        FormatCapabilityDescriptor sourceCap,
        FormatCapabilityDescriptor targetCap,
        Scene3D? sampleScene,
        ConversionLossReport report)
    {
        bool hasAnimations = sampleScene?.Animations.Count > 0;
        bool targetSupportsAnimations = FormatFeatureMatrix.Supports(targetCap.FormatType, FormatFeature.AnimationSequences);

        if (hasAnimations && !targetSupportsAnimations)
        {
            report.Items.Add(new ConversionLossItem(
                FeatureName: "Animation Keyframes & Sequences",
                Severity: LossSeverity.Lost,
                Category: "Animation",
                Description: "Target format does not support animation keyframes or timeline clips.",
                SourceRepresentation: "USM AnimationClip",
                TargetRepresentation: "None",
                RemediationAdvice: "Base pose / bind pose geometry is exported. Motion clips are omitted."));
        }
    }
}
