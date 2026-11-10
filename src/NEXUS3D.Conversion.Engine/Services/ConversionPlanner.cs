using Microsoft.Extensions.Logging;
using NEXUS3D.Conversion.Abstractions;
using NEXUS3D.Conversion.Abstractions.Interfaces;
using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Shared.Guards;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Conversion.Engine.Services;

/// <summary>
/// Plans the required transformation sequence to convert any 3D model through the Universal Scene Model.
/// </summary>
public sealed class ConversionPlanner : IConversionPlanner
{
    private readonly IFormatCapabilityRegistry _capabilityRegistry;
    private readonly ILogger<ConversionPlanner> _logger;

    public ConversionPlanner(
        IFormatCapabilityRegistry capabilityRegistry,
        ILogger<ConversionPlanner> logger)
    {
        _capabilityRegistry = Guard.NotNull(capabilityRegistry);
        _logger = Guard.NotNull(logger);
    }

    public Task<OperationResult<ConversionPlan>> CreatePlanAsync(
        ConversionRequest request,
        Scene3D? preloadedScene = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.SourceFilePath))
        {
            return Task.FromResult(OperationResult<ConversionPlan>.Failure(
                "CONV_PLAN_EMPTY_SRC", "Source file path must not be empty."));
        }

        if (string.IsNullOrWhiteSpace(request.TargetFilePath))
        {
            return Task.FromResult(OperationResult<ConversionPlan>.Failure(
                "CONV_PLAN_EMPTY_TGT", "Target file path must not be empty."));
        }

        var sourceFormat = request.SourceFormat;
        var targetFormat = request.TargetFormat;
        var options = request.Options ?? ConversionOptions.Default;

        var sourceCap = _capabilityRegistry.GetCapability(sourceFormat);
        var targetCap = _capabilityRegistry.GetCapability(targetFormat);

        var lossReport = _capabilityRegistry.EvaluateLoss(sourceFormat, targetFormat, preloadedScene);
        var steps = new List<ConversionStep>();
        int order = 1;

        // Step 1: Detect Format
        steps.Add(new ConversionStep(
            Order: order++,
            StepType: ConversionStepType.SourceFormatDetection,
            Title: "Detect and Validate Source Format",
            Description: $"Verify binary/text signatures for {sourceFormat} from '{Path.GetFileName(request.SourceFilePath)}'.",
            IsRequired: true));

        // Step 2: Parse into USM
        steps.Add(new ConversionStep(
            Order: order++,
            StepType: ConversionStepType.SourceParsingToUsm,
            Title: "Parse Source into Universal Scene Model",
            Description: $"Decode {sourceCap.DisplayName} stream into unified USM (Scene3D) aggregate.",
            IsRequired: true));

        // Step 3: Coordinate system adaptation
        if (options.AdaptCoordinates)
        {
            steps.Add(new ConversionStep(
                Order: order++,
                StepType: ConversionStepType.CoordinateAdaptation,
                Title: "Adapt Coordinate System and Scaling",
                Description: $"Align handedness (RH <-> LH), up-axis (Y-up), and apply scaling factor {options.ScaleFactor:F2}x.",
                IsRequired: true));
        }

        // Step 4: Geometry adaptation
        bool isVoxelToMesh = (sourceCap.NativeGeometryKind == GeometryKind.VoxelGrid || sourceFormat is ModelFormatType.Schematic or ModelFormatType.VoxelJson) &&
                             targetCap.NativeGeometryKind == GeometryKind.PolygonMesh;

        bool isMeshToVoxel = (sourceCap.NativeGeometryKind == GeometryKind.PolygonMesh || sourceFormat == ModelFormatType.Obj) &&
                             (targetCap.NativeGeometryKind == GeometryKind.VoxelGrid || targetFormat is ModelFormatType.Schematic or ModelFormatType.VoxelJson);

        bool isBedrockToMesh = sourceFormat == ModelFormatType.BedrockGeometry && targetFormat == ModelFormatType.Obj;
        bool isBedrockToVoxel = sourceFormat == ModelFormatType.BedrockGeometry && (targetFormat is ModelFormatType.Schematic or ModelFormatType.VoxelJson);

        if (isBedrockToMesh)
        {
            steps.Add(new ConversionStep(
                Order: order++,
                StepType: ConversionStepType.GeometryAdaptation,
                Title: "Bake Bedrock Cuboid Hierarchy to Polygon Mesh",
                Description: "Flatten bone pivots, rotations, inflate values, and locators into static SceneNode meshes.",
                IsRequired: true));
        }
        else if (isBedrockToVoxel)
        {
            steps.Add(new ConversionStep(
                Order: order++,
                StepType: ConversionStepType.GeometryAdaptation,
                Title: "Rasterize Bedrock Cuboids to Voxel Grid",
                Description: "Bake bone cuboid hierarchy and voxelize cuboid bounds into discrete block structures.",
                IsRequired: true));
        }
        else if (isVoxelToMesh)
        {
            string mesherDesc = options.MeshingMode switch
            {
                MeshingAlgorithm.GreedyMeshing => "Greedy Meshing algorithm (coplanar adjacent face merging)",
                MeshingAlgorithm.ExposedFaceCulling => "Exposed face culling algorithm (culls hidden adjacent internal faces)",
                _ => "Simple 6-quad block meshing algorithm"
            };

            steps.Add(new ConversionStep(
                Order: order++,
                StepType: ConversionStepType.GeometryAdaptation,
                Title: "Mesh Voxel Grid into Surface Polygons",
                Description: $"Convert discrete volumetric VoxelGrid into boundary surface mesh using {mesherDesc}.",
                IsRequired: true));
        }
        else if (isMeshToVoxel)
        {
            string voxDesc = options.VoxelizationMode switch
            {
                VoxelizationMode.SolidFill => "Surface triangle-AABB intersection + 3D flood fill for solid interior",
                _ => "Surface triangle-AABB intersection test"
            };

            steps.Add(new ConversionStep(
                Order: order++,
                StepType: ConversionStepType.GeometryAdaptation,
                Title: "Voxelize Surface Mesh into Voxel Grid",
                Description: $"Rasterize continuous mesh geometry using {voxDesc} at voxel size {options.TargetVoxelSize:F2}m.",
                IsRequired: true));
        }

        if (options.TriangulatePolygons && targetCap.NativeGeometryKind == GeometryKind.PolygonMesh)
        {
            steps.Add(new ConversionStep(
                Order: order++,
                StepType: ConversionStepType.GeometryAdaptation,
                Title: "Triangulate Polygon Faces",
                Description: "Decompose quads and n-gons into planar triangle primitives.",
                IsRequired: false));
        }

        // Step 5: Material & Texture Mapping
        if (options.PreserveMaterials)
        {
            steps.Add(new ConversionStep(
                Order: order++,
                StepType: ConversionStepType.MaterialMapping,
                Title: "Map Materials and Resolve Texture Assets",
                Description: $"Bridge shaders/colors between {sourceCap.DisplayName} and {targetCap.DisplayName}; copy textures if enabled.",
                IsRequired: true));
        }

        // Step 6: Loss Analysis
        steps.Add(new ConversionStep(
            Order: order++,
            StepType: ConversionStepType.LossAnalysis,
            Title: "Execute Data Loss Audit",
            Description: $"Verify converted USM scene against {targetFormat} capabilities; record {lossReport.Items.Count} diagnostic notes.",
            IsRequired: true));

        // Step 7: Export Target
        steps.Add(new ConversionStep(
            Order: order++,
            StepType: ConversionStepType.TargetExport,
            Title: $"Serialize and Export {targetCap.DisplayName}",
            Description: $"Encode USM aggregate into final output '{Path.GetFileName(request.TargetFilePath)}'.",
            IsRequired: true));

        string summary = $"{sourceFormat} -> USM -> {targetFormat} ({steps.Count} stages, Severity: {lossReport.OverallSeverity})";

        var plan = new ConversionPlan
        {
            SourceFormat = sourceFormat,
            TargetFormat = targetFormat,
            Steps = steps,
            LossReport = lossReport,
            StrategySummary = summary
        };

        _logger.LogInformation("Generated conversion plan: {Summary}", summary);
        return Task.FromResult(OperationResult<ConversionPlan>.Success(plan));
    }
}
