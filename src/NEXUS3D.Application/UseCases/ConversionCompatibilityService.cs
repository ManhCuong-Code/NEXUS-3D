using Microsoft.Extensions.Logging;
using NEXUS3D.Application.Interfaces;
using NEXUS3D.Application.Models;
using NEXUS3D.Conversion.Abstractions;
using NEXUS3D.Conversion.Abstractions.Interfaces;
using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.Application.UseCases;

/// <summary>
/// Evaluates cross-format compatibility and loss reports to guide users during model conversion.
/// </summary>
public sealed class ConversionCompatibilityService : IConversionCompatibilityService
{
    private readonly IFormatRegistry _formatRegistry;
    private readonly IDataLossAnalyzer _lossAnalyzer;
    private readonly ILogger<ConversionCompatibilityService> _logger;

    public ConversionCompatibilityService(
        IFormatRegistry formatRegistry,
        IDataLossAnalyzer lossAnalyzer,
        ILogger<ConversionCompatibilityService> logger)
    {
        _formatRegistry = Guard.NotNull(formatRegistry);
        _lossAnalyzer = Guard.NotNull(lossAnalyzer);
        _logger = Guard.NotNull(logger);
    }

    public IReadOnlyList<TargetFormatCompatibility> GetCompatibleTargets(Scene3D scene, ModelFormatType sourceFormat)
    {
        Guard.NotNull(scene);

        var exportable = _formatRegistry.GetExportableFormats();
        var results = new List<TargetFormatCompatibility>();

        foreach (var desc in exportable)
        {
            var target = desc.FormatType;
            var lossReport = GenerateLossReport(scene, sourceFormat, target);

            var (rating, description, isRecommended) = EvaluateRating(sourceFormat, target, scene, lossReport);

            var preserved = new List<string>();
            var lossItems = new List<string>();

            foreach (var item in lossReport.Items)
            {
                if (item.Severity is LossSeverity.Supported or LossSeverity.PreservedRaw)
                {
                    preserved.Add($"{item.FeatureName}: {item.Description}");
                }
                else
                {
                    lossItems.Add($"[{item.Severity}] {item.FeatureName}: {item.Description}");
                }
            }

            results.Add(new TargetFormatCompatibility
            {
                FormatType = target,
                DisplayName = desc.DisplayName,
                ShortCode = desc.ShortCode,
                PrimaryExtension = desc.PrimaryExtension,
                Descriptor = desc,
                Rating = rating,
                RatingDescription = description,
                LossReport = lossReport,
                PreservedFeatures = preserved,
                ApproximateOrLostFeatures = lossItems,
                IsRecommended = isRecommended
            });
        }

        return results.OrderByDescending(r => r.IsRecommended)
                      .ThenBy(r => r.Rating)
                      .ToList();
    }

    public ConversionLossReport GenerateLossReport(Scene3D scene, ModelFormatType sourceFormat, ModelFormatType targetFormat)
    {
        Guard.NotNull(scene);

        try
        {
            return _lossAnalyzer.AnalyzeLoss(scene, targetFormat);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed computing dynamic loss report for {Source} -> {Target}", sourceFormat, targetFormat);
            return ConversionLossReport.Empty(sourceFormat, targetFormat);
        }
    }

    private static (CompatibilityRating Rating, string Description, bool IsRecommended) EvaluateRating(
        ModelFormatType source,
        ModelFormatType target,
        Scene3D scene,
        ConversionLossReport lossReport)
    {
        if (source == target && source != ModelFormatType.Unknown)
        {
            return (CompatibilityRating.NativeLossless, "Exact format match. Lossless round-trip guaranteed.", false);
        }

        bool hasVoxels = false;
        bool hasMeshes = false;
        bool hasBones = scene.Skeletons.Count > 0;

        foreach (var node in scene.Graph.Nodes)
        {
            if (node.Object is { } obj)
            {
                if (obj.VoxelGrids.Count > 0) hasVoxels = true;
                if (obj.Meshes.Count > 0) hasMeshes = true;
            }
        }

        // Voxel <-> Schematic
        if (source is ModelFormatType.Schematic or ModelFormatType.VoxelJson &&
            target is ModelFormatType.Schematic or ModelFormatType.VoxelJson)
        {
            return (CompatibilityRating.HighFidelity, "Direct 3D block-to-voxel state mapping. High structural fidelity.", true);
        }

        // Voxel / Schematic -> Polygon Mesh (Voxel to Mesh)
        if (hasVoxels && target == ModelFormatType.Obj)
        {
            return (CompatibilityRating.HighFidelity, "Voxel cubes triangulate into optimized polygon mesh.", true);
        }

        // Bedrock -> OBJ
        if (source == ModelFormatType.BedrockGeometry && target == ModelFormatType.Obj)
        {
            return (CompatibilityRating.HighFidelity, "Cuboid elements convert directly to triangulated/quad meshes with box UVs.", true);
        }

        // OBJ -> Bedrock
        if (source == ModelFormatType.Obj && target == ModelFormatType.BedrockGeometry)
        {
            return (CompatibilityRating.ModerateApproximation, "Polygon meshes convert into Bedrock bone/cube hierarchy.", false);
        }

        // Polygon Mesh -> Schematic/Voxel (Requires Voxelization)
        if (hasMeshes && (target is ModelFormatType.Schematic or ModelFormatType.VoxelJson))
        {
            return (CompatibilityRating.ModerateApproximation, "Continuous surface polygon mesh requires 3D surface voxelization into discrete blocks.", false);
        }

        // Skeletal Bedrock -> Schematic/Voxel (Bones lost)
        if (hasBones && (target is ModelFormatType.Schematic or ModelFormatType.VoxelJson))
        {
            return (CompatibilityRating.LossyStructure, "Kinematic bone hierarchy and locators will be baked and permanently discarded.", false);
        }

        if (lossReport.LostCount > 0)
        {
            return (CompatibilityRating.LossyStructure, $"{lossReport.LostCount} data construct(s) unsupported by destination format.", false);
        }

        if (lossReport.ApproximatedCount > 0)
        {
            return (CompatibilityRating.ModerateApproximation, $"{lossReport.ApproximatedCount} construct(s) will be algorithmically approximated.", false);
        }

        return (CompatibilityRating.HighFidelity, "High compatibility with standard geometric and material preservation.", false);
    }
}
