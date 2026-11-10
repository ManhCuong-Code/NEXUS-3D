using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NEXUS3D.Conversion.Abstractions;
using NEXUS3D.Conversion.Abstractions.Interfaces;
using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Domain.Coordinates;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Shared.Guards;
using ICoordinateConverter = NEXUS3D.Conversion.Abstractions.Interfaces.ICoordinateConverter;

namespace NEXUS3D.Conversion.Engine.Services;

/// <summary>
/// Universal multi-format 3D conversion orchestrator.
/// Strictly enforces conversion through the Universal Scene Model (USM) without direct pairwise converters.
/// </summary>
public sealed class UniversalConversionPipeline : IUniversalConversionPipeline
{
    private readonly IEnumerable<IFormatParser> _parsers;
    private readonly IEnumerable<IFormatExporter> _exporters;
    private readonly IConversionPlanner _planner;
    private readonly ICoordinateConverter _coordinateConverter;
    private readonly IVoxelToMeshConverter _voxelToMeshConverter;
    private readonly IMeshToVoxelConverter _meshToVoxelConverter;
    private readonly IBedrockCubeToMeshConverter _bedrockCubeConverter;
    private readonly IPolygonTriangulator _triangulator;
    private readonly IMaterialMappingService _materialMapper;
    private readonly IDataLossAnalyzer _lossAnalyzer;
    private readonly ILogger<UniversalConversionPipeline> _logger;

    public UniversalConversionPipeline(
        IEnumerable<IFormatParser> parsers,
        IEnumerable<IFormatExporter> exporters,
        IConversionPlanner planner,
        ICoordinateConverter coordinateConverter,
        IVoxelToMeshConverter voxelToMeshConverter,
        IMeshToVoxelConverter meshToVoxelConverter,
        IBedrockCubeToMeshConverter bedrockCubeConverter,
        IPolygonTriangulator triangulator,
        IMaterialMappingService materialMapper,
        IDataLossAnalyzer lossAnalyzer,
        ILogger<UniversalConversionPipeline> logger)
    {
        _parsers = Guard.NotNull(parsers);
        _exporters = Guard.NotNull(exporters);
        _planner = Guard.NotNull(planner);
        _coordinateConverter = Guard.NotNull(coordinateConverter);
        _voxelToMeshConverter = Guard.NotNull(voxelToMeshConverter);
        _meshToVoxelConverter = Guard.NotNull(meshToVoxelConverter);
        _bedrockCubeConverter = Guard.NotNull(bedrockCubeConverter);
        _triangulator = Guard.NotNull(triangulator);
        _materialMapper = Guard.NotNull(materialMapper);
        _lossAnalyzer = Guard.NotNull(lossAnalyzer);
        _logger = Guard.NotNull(logger);
    }

    public async Task<ConversionResult> ExecuteAsync(
        ConversionRequest request,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);
        var context = new ConversionContext(request, cancellationToken);
        context.Stopwatch.Start();

        try
        {
            progress?.Report(0.05);
            context.Log($"Initiating conversion: '{Path.GetFileName(request.SourceFilePath)}' ({request.SourceFormat}) -> '{Path.GetFileName(request.TargetFilePath)}' ({request.TargetFormat}).");

            if (!File.Exists(request.SourceFilePath))
            {
                return Fail(context, "CONV_ERR_SRC_MISSING", $"Source file '{request.SourceFilePath}' does not exist.");
            }

            // Step 1: Create Execution Plan
            progress?.Report(0.10);
            var planResult = await _planner.CreatePlanAsync(request, null, cancellationToken).ConfigureAwait(false);
            if (planResult.IsFailure || planResult.Value is null)
            {
                return Fail(context, planResult.ErrorCode ?? "CONV_PLAN_ERR", planResult.ErrorMessage ?? "Planning failed.");
            }

            context.Plan = planResult.Value;
            context.Log($"Plan generated: {context.Plan.StrategySummary}");

            // Loss policy guard
            if (request.Options?.LossPolicy == LossActionPolicy.AbortOnAnyLoss && context.Plan.LossReport.HasLoss)
            {
                return Fail(
                    context,
                    "CONV_ABORT_LOSS_POLICY",
                    $"Conversion aborted due to LossActionPolicy.AbortOnAnyLoss: detected {context.Plan.LossReport.Items.Count} loss item(s).");
            }

            // Step 2: Parse source format into USM (Scene3D)
            progress?.Report(0.20);
            IFormatParser parser = ResolveParser(request.SourceFormat, request.SourceFilePath);
            context.Log($"Decoding source stream with {parser.GetType().Name}...");

            await using var sourceStream = new FileStream(
                request.SourceFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 65536,
                useAsync: true);

            var parseResult = await parser.ParseToSceneAsync(
                sourceStream,
                new ParseOptions { StrictValidation = false, PreserveUnknownData = true },
                cancellationToken).ConfigureAwait(false);

            if (parseResult.IsFailure || parseResult.Value is null)
            {
                return Fail(context, parseResult.Error.Code ?? "PARSE_FAILED", parseResult.Error.Message ?? "Parsing to USM failed.");
            }

            Scene3D scene = parseResult.Value;
            context.Scene = scene;
            context.Log($"Successfully parsed into USM: Scene '{scene.Name}' ({scene.CalculateStatistics().NodeCount} nodes).");

            // Step 3: Coordinate Adaptation
            if (context.Options.AdaptCoordinates)
            {
                progress?.Report(0.35);
                CoordinateSystem targetCoords = ResolveTargetCoordinates(request.TargetFormat);
                _coordinateConverter.AdaptCoordinates(
                    scene,
                    scene.Coordinates.CurrentSystem,
                    targetCoords,
                    new CoordinateTransformOptions(
                        Scale: context.Options.ScaleFactor,
                        InvertNormals: context.Options.InvertNormals,
                        InvertWindingOrder: context.Options.AutoCorrectWindingOrder));
                context.Log($"Adapted coordinates to {targetCoords.Handedness} {targetCoords.UpAxis} (Scale: {context.Options.ScaleFactor:F2}x).");
            }

            // Step 4: Geometry Adaptation
            progress?.Report(0.50);
            await ExecuteGeometryAdaptationAsync(scene, request, context, cancellationToken).ConfigureAwait(false);

            // Step 5: Triangulation (if enabled)
            if (context.Options.TriangulatePolygons)
            {
                progress?.Report(0.60);
                TriangulateSceneMeshes(scene);
                context.Log("Triangulated scene polygons into planar triangles.");
            }

            // Step 6: Material and Texture Mapping
            if (context.Options.PreserveMaterials)
            {
                progress?.Report(0.70);
                var matResult = await _materialMapper.MapMaterialsAsync(scene, request, cancellationToken).ConfigureAwait(false);
                if (matResult.IsFailure)
                {
                    context.Warn($"Material mapping returned warning: {matResult.ErrorMessage}");
                }
                else
                {
                    context.Log("Completed material and texture mapping.");
                }
            }

            // Step 7: Data Loss Audit
            progress?.Report(0.80);
            ConversionLossReport liveLossReport = _lossAnalyzer.AnalyzeLoss(scene, request.TargetFormat, context.Options);
            context.Log($"Data loss audit complete: Severity {liveLossReport.OverallSeverity} ({liveLossReport.Items.Count} items).");

            // Step 8: Export to Target Format
            progress?.Report(0.90);
            IFormatExporter exporter = ResolveExporter(request.TargetFormat, request.TargetFilePath);
            context.Log($"Serializing target format using {exporter.GetType().Name}...");

            string targetDir = Path.GetDirectoryName(Path.GetFullPath(request.TargetFilePath)) ?? Environment.CurrentDirectory;
            Directory.CreateDirectory(targetDir);

            long targetBytesWritten;
            await using (var targetStream = new FileStream(
                request.TargetFilePath,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 65536,
                useAsync: true))
            {
                FormatExportReport exportReport = await exporter.ExportSceneAsync(
                    scene,
                    targetStream,
                    new ExportOptions { StrictMapping = false, PreserveUnknownData = true },
                    cancellationToken).ConfigureAwait(false);

                if (!exportReport.IsSuccess)
                {
                    return Fail(context, exportReport.Error?.Code ?? "EXPORT_FAILED", exportReport.Error?.Message ?? "Export failed.");
                }

                targetBytesWritten = exportReport.BytesWritten > 0 ? exportReport.BytesWritten : targetStream.Length;
            }

            progress?.Report(1.0);
            context.Stopwatch.Stop();
            long sourceBytes = new FileInfo(request.SourceFilePath).Length;

            context.Log($"Conversion succeeded in {context.Stopwatch.ElapsedMilliseconds}ms. Output size: {targetBytesWritten} bytes.");

            return new ConversionResult(
                JobId: context.JobId,
                IsSuccess: true,
                ErrorMessage: null,
                ErrorCode: null,
                SourceFilePath: request.SourceFilePath,
                TargetFilePath: request.TargetFilePath,
                SourceFormat: request.SourceFormat,
                TargetFormat: request.TargetFormat,
                SourceSizeBytes: sourceBytes,
                TargetSizeBytes: targetBytesWritten,
                DurationMs: context.Stopwatch.ElapsedMilliseconds,
                LossReport: liveLossReport,
                Logs: context.Logs,
                Warnings: context.Warnings);
        }
        catch (OperationCanceledException)
        {
            context.Stopwatch.Stop();
            context.Log("Conversion was cancelled by caller.");
            return Fail(context, "CONV_CANCELLED", "Conversion was cancelled.", isCancelled: true);
        }
        catch (Exception ex)
        {
            context.Stopwatch.Stop();
            _logger.LogError(ex, "Unexpected error in conversion pipeline for '{SourcePath}'.", request.SourceFilePath);
            return Fail(context, "CONV_UNHANDLED_EXCEPTION", ex.Message);
        }
    }

    private async Task ExecuteGeometryAdaptationAsync(
        Scene3D scene,
        ConversionRequest request,
        ConversionContext context,
        CancellationToken cancellationToken)
    {
        // 1. Bedrock bones/cubes -> Static Mesh
        if (request.SourceFormat == ModelFormatType.BedrockGeometry && request.TargetFormat == ModelFormatType.Obj)
        {
            var bakeResult = _bedrockCubeConverter.ConvertBedrockCubesToMesh(scene, new BedrockCubeToMeshOptions
            {
                BakeTransforms = context.Options.BakeBedrockBonesToMesh
            });

            if (bakeResult.IsSuccess && bakeResult.Value is not null)
            {
                var sceneObj = new SceneObject("StaticMesh");
                sceneObj.Meshes.Add(bakeResult.Value);
                var staticNode = new SceneNode("BakedBedrockGeometry", sceneObj);
                scene.Graph.AddNode(scene.Graph.Root, staticNode);
                context.Log($"Baked Bedrock bones into static mesh '{bakeResult.Value.Name}' ({bakeResult.Value.Faces.Count} faces).");
            }
        }

        // 2. Voxel -> Mesh
        if (request.TargetFormat == ModelFormatType.Obj)
        {
            foreach (var node in scene.Graph.Traverse().ToList())
            {
                if (node.Object is not { } obj || obj.VoxelGrids.Count == 0) continue;

                foreach (var grid in obj.VoxelGrids.ToList())
                {
                    var meshResult = _voxelToMeshConverter.ConvertVoxelGridToMesh(grid, new VoxelToMeshOptions(
                        Algorithm: context.Options.MeshingMode,
                        BlockSize: context.Options.TargetVoxelSize));

                    if (meshResult.IsSuccess && meshResult.Value is not null)
                    {
                        obj.Meshes.Add(meshResult.Value);
                        context.Log($"Meshed VoxelGrid '{grid.Name}' into MeshData ({meshResult.Value.Faces.Count} faces) via {context.Options.MeshingMode}.");
                    }
                }

                obj.VoxelGrids.Clear();
            }
        }

        // 3. Mesh -> Voxel
        if (request.TargetFormat is ModelFormatType.Schematic or ModelFormatType.VoxelJson)
        {
            foreach (var node in scene.Graph.Traverse().ToList())
            {
                if (node.Object is not { } obj || obj.Meshes.Count == 0) continue;

                foreach (var mesh in obj.Meshes.ToList())
                {
                    var voxelResult = _meshToVoxelConverter.ConvertMeshToVoxelGrid(mesh, scene, new MeshToVoxelOptions(
                        Mode: context.Options.VoxelizationMode,
                        VoxelSize: context.Options.TargetVoxelSize,
                        MaxGridDimension: context.Options.TargetGridMaxResolution,
                        FillInterior: context.Options.SolidFillVoxels));

                    if (voxelResult.IsSuccess && voxelResult.Value is not null)
                    {
                        obj.VoxelGrids.Add(voxelResult.Value);
                        context.Log($"Voxelized mesh '{mesh.Name}' into VoxelGrid ({voxelResult.Value.GetVoxelCount()} voxels).");
                    }
                }

                obj.Meshes.Clear();
            }
        }

        await Task.CompletedTask;
    }

    private void TriangulateSceneMeshes(Scene3D scene)
    {
        foreach (var node in scene.Graph.Traverse())
        {
            if (node.Object is not { } obj) continue;
            for (int i = 0; i < obj.Meshes.Count; i++)
            {
                obj.Meshes[i] = _triangulator.TriangulateMesh(obj.Meshes[i]);
            }
        }
    }

    private IFormatParser ResolveParser(ModelFormatType formatType, string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLowerInvariant();

        foreach (var parser in _parsers)
        {
            if (formatType == ModelFormatType.Schematic &&
                (parser.FormatIdentifier.Contains("sponge", StringComparison.OrdinalIgnoreCase) ||
                 parser.FormatIdentifier.Contains("schematic", StringComparison.OrdinalIgnoreCase)))
            {
                return parser;
            }

            if (formatType == ModelFormatType.Obj &&
                parser.FormatIdentifier.Contains("obj", StringComparison.OrdinalIgnoreCase))
            {
                return parser;
            }

            if (formatType == ModelFormatType.VoxelJson &&
                parser.FormatIdentifier.Contains("voxel", StringComparison.OrdinalIgnoreCase))
            {
                return parser;
            }

            if (formatType == ModelFormatType.BedrockGeometry &&
                parser.FormatIdentifier.Contains("bedrock", StringComparison.OrdinalIgnoreCase))
            {
                return parser;
            }
        }

        // Fallback: match by identifier substring
        var fallback = _parsers.FirstOrDefault(p =>
            p.FormatIdentifier.Contains(formatType.ToString(), StringComparison.OrdinalIgnoreCase));

        return fallback ?? throw new InvalidOperationException($"No registered IFormatParser found for format '{formatType}'.");
    }

    private IFormatExporter ResolveExporter(ModelFormatType formatType, string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLowerInvariant();

        foreach (var exporter in _exporters)
        {
            if (formatType == ModelFormatType.Schematic &&
                (exporter.FormatIdentifier.Contains("sponge", StringComparison.OrdinalIgnoreCase) ||
                 exporter.FormatIdentifier.Contains("schematic", StringComparison.OrdinalIgnoreCase)))
            {
                // Prefer Sponge v3 for .schem, or Legacy for .schematic
                if (ext == ".schematic" && exporter.FormatIdentifier.Contains("legacy", StringComparison.OrdinalIgnoreCase))
                {
                    return exporter;
                }

                if (ext == ".schem" && exporter.FormatIdentifier.Contains("sponge", StringComparison.OrdinalIgnoreCase))
                {
                    return exporter;
                }

                return exporter;
            }

            if (formatType == ModelFormatType.Obj &&
                exporter.FormatIdentifier.Contains("obj", StringComparison.OrdinalIgnoreCase))
            {
                return exporter;
            }

            if (formatType == ModelFormatType.VoxelJson &&
                exporter.FormatIdentifier.Contains("voxel", StringComparison.OrdinalIgnoreCase))
            {
                return exporter;
            }

            if (formatType == ModelFormatType.BedrockGeometry &&
                exporter.FormatIdentifier.Contains("bedrock", StringComparison.OrdinalIgnoreCase))
            {
                return exporter;
            }
        }

        var fallback = _exporters.FirstOrDefault(e =>
            e.FormatIdentifier.Contains(formatType.ToString(), StringComparison.OrdinalIgnoreCase));

        return fallback ?? throw new InvalidOperationException($"No registered IFormatExporter found for format '{formatType}'.");
    }

    private static CoordinateSystem ResolveTargetCoordinates(ModelFormatType format) => format switch
    {
        ModelFormatType.Obj => CoordinateSystem.WavefrontObjDefault,
        ModelFormatType.BedrockGeometry => CoordinateSystem.BedrockDefault,
        _ => CoordinateSystem.DefaultUsm
    };

    private static ConversionResult Fail(
        ConversionContext context,
        string code,
        string message,
        bool isCancelled = false)
    {
        context.Log($"[ERROR] {code}: {message}");
        return ConversionResult.Failure(
            jobId: context.JobId,
            sourcePath: context.Request.SourceFilePath,
            targetPath: context.Request.TargetFilePath,
            sourceFormat: context.Request.SourceFormat,
            targetFormat: context.Request.TargetFormat,
            errorCode: code,
            errorMessage: message,
            durationMs: context.Stopwatch.ElapsedMilliseconds,
            lossReport: context.Plan?.LossReport,
            logs: context.Logs,
            warnings: context.Warnings);
    }
}
