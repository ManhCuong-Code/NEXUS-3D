using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Voxelization.Abstractions.Contracts;
using NEXUS3D.Voxelization.Abstractions.Models;
using NEXUS3D.Voxelization.Engine.Algorithms;
using NEXUS3D.Voxelization.Engine.Conversion;
using NEXUS3D.Voxelization.Engine.Processing;

namespace NEXUS3D.Voxelization.Engine.Engine;

/// <summary>
/// Universal high-performance voxelization engine coordinating algorithm execution, post-processing, and cuboid merging.
/// </summary>
public sealed class VoxelizationEngine : IVoxelizationEngine
{
    private readonly IVoxelizationValidator _validator;
    private readonly IVoxelToCuboidConverter _cuboidConverter;
    private readonly VoxelOptimizationService _optimizationService = new();
    private readonly ILogger<VoxelizationEngine>? _logger;

    private readonly Dictionary<VoxelizationMode, IVoxelizationAlgorithm> _algorithms = [];

    public VoxelizationEngine(
        IVoxelizationValidator validator,
        IVoxelToCuboidConverter cuboidConverter,
        ILogger<VoxelizationEngine>? logger = null)
    {
        _validator = validator;
        _cuboidConverter = cuboidConverter;
        _logger = logger;

        RegisterAlgorithm(new SurfaceVoxelization());
        RegisterAlgorithm(new SolidVoxelization());
        RegisterAlgorithm(new ConservativeVoxelization());
        RegisterAlgorithm(new AdaptiveVoxelization());
    }

    public void RegisterAlgorithm(IVoxelizationAlgorithm algorithm)
    {
        _algorithms[algorithm.SupportedMode] = algorithm;
    }

    public async Task<VoxelizationResult> VoxelizeAsync(
        Scene3D scene,
        IVoxelizationSettings settings,
        IProgress<VoxelizationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var (isValid, errorMsg, warnings) = _validator.Validate(scene, settings);
        if (!isValid)
        {
            return VoxelizationResult.Failure(errorMsg ?? "Scene validation failed.", warnings);
        }

        // Aggregate all meshes from scene
        var combinedMesh = new MeshData { Name = $"{scene.Name}_CombinedMesh" };
        int vertexOffset = 0;

        foreach (var node in scene.Graph.Nodes)
        {
            if (node.Object?.Meshes.Count > 0)
            {
                var worldMat = node.GetWorldMatrix();
                foreach (var m in node.Object.Meshes)
                {
                    for (int v = 0; v < m.Vertices.Count; v++)
                    {
                        var vert = m.Vertices[v].DeepClone();
                        vert.Position = worldMat.TransformPoint(vert.Position);
                        combinedMesh.Vertices.Add(vert);
                    }

                    for (int f = 0; f < m.Faces.Count; f++)
                    {
                        var face = m.Faces[f].DeepClone();
                        face.VertexIndices = face.VertexIndices.Select(idx => idx + vertexOffset).ToArray();
                        combinedMesh.Faces.Add(face);
                    }

                    vertexOffset += m.Vertices.Count;
                }
            }
        }

        // If no polygon meshes exist, check if scene contains VoxelGrids and synthesize polygon meshes from voxels
        if (combinedMesh.Vertices.Count == 0)
        {
            foreach (var node in scene.Graph.Nodes)
            {
                if (node.Object?.VoxelGrids.Count > 0)
                {
                    var worldMat = node.GetWorldMatrix();
                    foreach (var grid in node.Object.VoxelGrids)
                    {
                        AppendVoxelGridToMesh(grid, worldMat, combinedMesh, ref vertexOffset);
                    }
                }
            }
        }

        return await VoxelizeMeshAsync(combinedMesh, scene, settings, progress, cancellationToken);
    }

    private static void AppendVoxelGridToMesh(VoxelGrid grid, Matrix4x4D worldMat, MeshData targetMesh, ref int vertexOffset)
    {
        double vx = grid.VoxelSize.X > 0 ? grid.VoxelSize.X : 1.0;
        double vy = grid.VoxelSize.Y > 0 ? grid.VoxelSize.Y : 1.0;
        double vz = grid.VoxelSize.Z > 0 ? grid.VoxelSize.Z : 1.0;

        foreach (var voxel in grid.Voxels)
        {
            double x0 = voxel.Position.X * vx;
            double y0 = voxel.Position.Y * vy;
            double z0 = voxel.Position.Z * vz;
            double x1 = x0 + vx;
            double y1 = y0 + vy;
            double z1 = z0 + vz;

            var c0 = worldMat.TransformPoint(new Vector3D(x0, y0, z0));
            var c1 = worldMat.TransformPoint(new Vector3D(x1, y0, z0));
            var c2 = worldMat.TransformPoint(new Vector3D(x1, y1, z0));
            var c3 = worldMat.TransformPoint(new Vector3D(x0, y1, z0));
            var c4 = worldMat.TransformPoint(new Vector3D(x0, y0, z1));
            var c5 = worldMat.TransformPoint(new Vector3D(x1, y0, z1));
            var c6 = worldMat.TransformPoint(new Vector3D(x1, y1, z1));
            var c7 = worldMat.TransformPoint(new Vector3D(x0, y1, z1));

            int baseIdx = targetMesh.Vertices.Count;
            targetMesh.Vertices.Add(new Vertex(c0));
            targetMesh.Vertices.Add(new Vertex(c1));
            targetMesh.Vertices.Add(new Vertex(c2));
            targetMesh.Vertices.Add(new Vertex(c3));
            targetMesh.Vertices.Add(new Vertex(c4));
            targetMesh.Vertices.Add(new Vertex(c5));
            targetMesh.Vertices.Add(new Vertex(c6));
            targetMesh.Vertices.Add(new Vertex(c7));

            int[][] cubeTriangles =
            [
                // Front (-Z)
                [baseIdx + 0, baseIdx + 2, baseIdx + 1],
                [baseIdx + 0, baseIdx + 3, baseIdx + 2],
                // Back (+Z)
                [baseIdx + 5, baseIdx + 7, baseIdx + 4],
                [baseIdx + 5, baseIdx + 6, baseIdx + 7],
                // Top (+Y)
                [baseIdx + 3, baseIdx + 6, baseIdx + 2],
                [baseIdx + 3, baseIdx + 7, baseIdx + 6],
                // Bottom (-Y)
                [baseIdx + 4, baseIdx + 1, baseIdx + 5],
                [baseIdx + 4, baseIdx + 0, baseIdx + 1],
                // Right (+X)
                [baseIdx + 1, baseIdx + 6, baseIdx + 5],
                [baseIdx + 1, baseIdx + 2, baseIdx + 6],
                // Left (-X)
                [baseIdx + 4, baseIdx + 3, baseIdx + 0],
                [baseIdx + 4, baseIdx + 7, baseIdx + 3]
            ];

            foreach (var tri in cubeTriangles)
            {
                targetMesh.Faces.Add(new Face(tri));
            }
        }

        vertexOffset = targetMesh.Vertices.Count;
    }

    public async Task<VoxelizationResult> VoxelizeMeshAsync(
        MeshData mesh,
        Scene3D? parentScene,
        IVoxelizationSettings settings,
        IProgress<VoxelizationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var (isValid, errorMsg, warnings) = _validator.ValidateMesh(mesh, settings);
        if (!isValid)
        {
            return VoxelizationResult.Failure(errorMsg ?? "Mesh validation failed.", warnings);
        }

        if (!_algorithms.TryGetValue(settings.Mode, out var algorithm))
        {
            algorithm = _algorithms[VoxelizationMode.SurfaceOnly];
        }

        var sw = Stopwatch.StartNew();
        var rawResult = await algorithm.ExecuteAsync(mesh, parentScene, settings, progress, cancellationToken);
        if (!rawResult.IsSuccess || rawResult.VoxelGrid is null)
        {
            return rawResult;
        }

        var grid = rawResult.VoxelGrid;
        int prunedCount = 0;
        int filledGaps = 0;

        // 1. Post-processing optimizations
        if (settings.RemoveIsolatedVoxels)
        {
            progress?.Report(new VoxelizationProgress("Post-Processing", 85.0, 0, 1, "Pruning isolated single voxels..."));
            prunedCount = _optimizationService.PruneIsolatedVoxels(grid);
        }

        if (settings.FillInternalGaps)
        {
            progress?.Report(new VoxelizationProgress("Post-Processing", 90.0, 0, 1, "Filling pinhole boundary gaps..."));
            filledGaps = _optimizationService.FillInternalGaps(grid);
        }

        // 2. Cuboid merging
        IReadOnlyList<VoxelCuboid>? cuboids = null;
        int cuboidCount = 0;
        if (settings.MergeAdjacentVoxels && grid.GetVoxelCount() > 0)
        {
            progress?.Report(new VoxelizationProgress("Cuboid Merging", 95.0, 0, 1, "Greedy merging contiguous voxels into cuboids..."));
            var cuboidResult = await _cuboidConverter.ConvertToCuboidsAsync(grid, cancellationToken);
            cuboids = cuboidResult.Cuboids;
            cuboidCount = cuboids.Count;
        }

        sw.Stop();
        var allWarnings = new List<VoxelizationWarning>(warnings);
        allWarnings.AddRange(rawResult.Warnings);

        var finalStats = rawResult.Statistics with
        {
            InitialVoxelCount = rawResult.Statistics.InitialVoxelCount,
            FinalVoxelCount = grid.GetVoxelCount(),
            OptimizedCuboidCount = cuboidCount,
            IsolatedVoxelsPruned = prunedCount,
            InternalVoxelsFilled = rawResult.Statistics.InternalVoxelsFilled + filledGaps,
            ProcessingTimeMs = sw.ElapsedMilliseconds
        };

        progress?.Report(VoxelizationProgress.Completed($"Voxelization finished: {grid.GetVoxelCount():N0} voxels ({cuboidCount:N0} cuboids) in {sw.ElapsedMilliseconds}ms."));

        return VoxelizationResult.Success(grid, finalStats, cuboids, allWarnings);
    }
}
