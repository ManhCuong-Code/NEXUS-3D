using Microsoft.Extensions.Logging;
using NEXUS3D.Conversion.Abstractions;
using NEXUS3D.Conversion.Abstractions.Interfaces;
using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Metadata;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.Conversion.Engine.Services;

/// <summary>
/// Service inspecting an active Universal Scene Model (<see cref="Scene3D"/>) against target format capabilities
/// to generate transparent data fidelity, approximation, and loss audits.
/// </summary>
public sealed class DataLossAnalyzer : IDataLossAnalyzer
{
    private readonly IFormatCapabilityRegistry _capabilityRegistry;
    private readonly ILogger<DataLossAnalyzer> _logger;

    public DataLossAnalyzer(
        IFormatCapabilityRegistry capabilityRegistry,
        ILogger<DataLossAnalyzer> logger)
    {
        _capabilityRegistry = Guard.NotNull(capabilityRegistry);
        _logger = Guard.NotNull(logger);
    }

    public ConversionLossReport AnalyzeLoss(
        Scene3D scene,
        ModelFormatType targetFormat,
        ConversionOptions? options = null)
    {
        Guard.NotNull(scene);
        options ??= ConversionOptions.Default;

        // Detect source format from scene metadata or infer
        ModelFormatType sourceFormat = DetectSourceFormat(scene);
        var report = _capabilityRegistry.EvaluateLoss(sourceFormat, targetFormat, scene);

        // Additional deep scene-level structural inspections
        InspectDetailedSceneData(scene, targetFormat, report, options);

        // Sync report back into USM scene metadata
        SyncToSceneMetadata(scene, report);

        _logger.LogInformation(
            "Completed data loss analysis for '{SceneName}' -> {TargetFormat}: Overall Severity {Severity}, {ItemsCount} diagnostic items.",
            scene.Name,
            targetFormat,
            report.OverallSeverity,
            report.Items.Count);

        return report;
    }

    private static ModelFormatType DetectSourceFormat(Scene3D scene)
    {
        string? formatId = scene.Metadata.Source?.OriginalFormatId;
        if (!string.IsNullOrWhiteSpace(formatId))
        {
            if (formatId.Contains("Schematic", StringComparison.OrdinalIgnoreCase)) return ModelFormatType.Schematic;
            if (formatId.Contains("Obj", StringComparison.OrdinalIgnoreCase)) return ModelFormatType.Obj;
            if (formatId.Contains("Voxel", StringComparison.OrdinalIgnoreCase)) return ModelFormatType.VoxelJson;
            if (formatId.Contains("Bedrock", StringComparison.OrdinalIgnoreCase)) return ModelFormatType.BedrockGeometry;
        }

        // Infer from scene contents
        if (scene.Skeletons.Count > 0) return ModelFormatType.BedrockGeometry;
        if (scene.Graph.Traverse().Any(n => n.Object?.VoxelGrids.Count > 0)) return ModelFormatType.Schematic;
        if (scene.Graph.Traverse().Any(n => n.Object?.Meshes.Count > 0)) return ModelFormatType.Obj;

        return ModelFormatType.Unknown;
    }

    private static void InspectDetailedSceneData(
        Scene3D scene,
        ModelFormatType targetFormat,
        ConversionLossReport report,
        ConversionOptions options)
    {
        int totalMeshes = 0;
        int totalNgons = 0;
        int totalVoxelGrids = 0;

        foreach (var node in scene.Graph.Traverse())
        {
            if (node.Object is not { } obj) continue;
            totalMeshes += obj.Meshes.Count;
            totalVoxelGrids += obj.VoxelGrids.Count;

            for (int m = 0; m < obj.Meshes.Count; m++)
            {
                var mesh = obj.Meshes[m];
                for (int f = 0; f < mesh.Faces.Count; f++)
                {
                    if (mesh.Faces[f].VertexIndices.Count > 4)
                    {
                        totalNgons++;
                    }
                }
            }
        }

        if (totalNgons > 0 && options.TriangulatePolygons)
        {
            report.Items.Add(new ConversionLossItem(
                FeatureName: "Polygon Triangulation",
                Severity: LossSeverity.Approximated,
                Category: "Geometry",
                Description: $"{totalNgons} n-gon polygon face(s) are subdivided into planar triangles.",
                SourceRepresentation: "N-gon Polygons (> 4 vertices)",
                TargetRepresentation: "Decomposed Triangle primitives",
                RemediationAdvice: "Triangulation is enabled; planar surfaces remain geometrically accurate."));
        }

        if (scene.Metadata.PreservedRawData.Count > 0)
        {
            report.Items.Add(new ConversionLossItem(
                FeatureName: "Preserved Raw Binary Containers",
                Severity: LossSeverity.PreservedRaw,
                Category: "Metadata",
                Description: $"{scene.Metadata.PreservedRawData.Count} raw data container(s) preserved in memory.",
                SourceRepresentation: "RawDataContainer",
                TargetRepresentation: "Target format native files (Omitted from export)",
                RemediationAdvice: "Retained safely within USM scene structures."));
        }
    }

    private static void SyncToSceneMetadata(Scene3D scene, ConversionLossReport report)
    {
        foreach (var item in report.Items)
        {
            if (item.Severity is LossSeverity.Unmapped or LossSeverity.Lost)
            {
                scene.Metadata.Warnings.Add(new WarningRecord
                {
                    Code = $"LOSS_{item.Severity.ToString().ToUpperInvariant()}",
                    Message = $"{item.FeatureName}: {item.Description}",
                    SourceComponent = nameof(DataLossAnalyzer)
                });
            }
        }
    }
}
