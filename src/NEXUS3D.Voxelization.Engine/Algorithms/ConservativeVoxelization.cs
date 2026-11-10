using System.Diagnostics;
using System.Numerics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Voxelization.Abstractions.Contracts;
using NEXUS3D.Voxelization.Abstractions.Models;
using NEXUS3D.Voxelization.Engine.Geometry;
using NEXUS3D.Voxelization.Engine.Processing;

namespace NEXUS3D.Voxelization.Engine.Algorithms;

/// <summary>
/// Conservative surface voxelization with dilated AABB tests guaranteeing zero topological holes for thin or diagonal features.
/// </summary>
public sealed class ConservativeVoxelization : IVoxelizationAlgorithm
{
    private readonly MeshPreprocessor _preprocessor = new();
    private readonly VoxelMaterialMapper _materialMapper = new();

    public string Name => "Conservative Voxelization (Zero-Loss Dilated SAT)";
    public VoxelizationMode SupportedMode => VoxelizationMode.Conservative;

    public Task<VoxelizationResult> ExecuteAsync(
        MeshData mesh,
        Scene3D? scene,
        IVoxelizationSettings settings,
        IProgress<VoxelizationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        progress?.Report(new VoxelizationProgress("Mesh Preprocessing", 10.0, 0, 1, "Analyzing mesh for conservative rasterization..."));

        var prep = _preprocessor.Preprocess(mesh);
        if (prep.Triangles.Count == 0)
        {
            return Task.FromResult(VoxelizationResult.Failure("Mesh contains no valid triangles.", prep.Warnings));
        }

        var (dimX, dimY, dimZ, voxelSize, origin) = SurfaceVoxelization.CalculateGridBounds(prep.BoundsMin, prep.BoundsMax, settings);

        var grid = new VoxelGrid($"{mesh.Name}_ConservativeVoxels")
        {
            VoxelSize = new VoxelSize(voxelSize, voxelSize, voxelSize)
        };
        _materialMapper.InitializePalette(grid, scene, settings.DefaultBlockNamespace);

        // Conservative dilation factor (15% expansion of half-voxel bounding box to capture thin touching boundaries)
        float expansion = voxelSize * 0.15f;
        var halfVoxel = new Vector3(voxelSize * 0.5f);
        var occupiedVoxels = new Dictionary<VoxelPosition, List<(WorkingTriangle Tri, float Area)>>();

        int totalTriangles = prep.Triangles.Count;

        for (int t = 0; t < totalTriangles; t++)
        {
            if (t % 500 == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                double pct = 20.0 + (65.0 * t / totalTriangles);
                progress?.Report(new VoxelizationProgress("Conservative Rasterization", pct, t, totalTriangles, $"Rasterizing triangle {t:N0} of {totalTriangles:N0}..."));
            }

            var tri = prep.Triangles[t];
            var (minVoxel, maxVoxel) = SpatialIndex.GetVoxelRange(tri, origin, voxelSize, dimX, dimY, dimZ, expansion);

            for (int z = minVoxel.Z; z <= maxVoxel.Z; z++)
            {
                float cz = origin.Z + (z + 0.5f) * voxelSize;
                for (int y = minVoxel.Y; y <= maxVoxel.Y; y++)
                {
                    float cy = origin.Y + (y + 0.5f) * voxelSize;
                    for (int x = minVoxel.X; x <= maxVoxel.X; x++)
                    {
                        float cx = origin.X + (x + 0.5f) * voxelSize;
                        var cellCenter = new Vector3(cx, cy, cz);

                        if (TriangleBoxIntersection.Intersects(tri.V0, tri.V1, tri.V2, cellCenter, halfVoxel, expansion))
                        {
                            var pos = new VoxelPosition(x, y, z);
                            if (!occupiedVoxels.TryGetValue(pos, out var list))
                            {
                                list = [];
                                occupiedVoxels[pos] = list;
                            }
                            list.Add((tri, tri.Area));
                        }
                    }
                }
            }
        }

        // Material assignment
        foreach (var (pos, candidates) in occupiedVoxels)
        {
            var dominantMat = VoxelMaterialMapper.ElectDominantMaterial(candidates);
            int palIdx = _materialMapper.ResolvePaletteIndex(dominantMat);
            var entry = grid.Palette.Entries[palIdx];
            grid.SetVoxel(new Voxel(pos, palIdx, entry.State, dominantMat));
        }

        sw.Stop();
        var stats = new VoxelizationStatistics
        {
            TotalTrianglesProcessed = totalTriangles,
            InitialVoxelCount = grid.GetVoxelCount(),
            FinalVoxelCount = grid.GetVoxelCount(),
            GridDimensionX = dimX,
            GridDimensionY = dimY,
            GridDimensionZ = dimZ,
            ActualVoxelSize = voxelSize,
            ProcessingTimeMs = sw.ElapsedMilliseconds
        };

        progress?.Report(VoxelizationProgress.Completed($"Conservative voxelization complete: {grid.GetVoxelCount():N0} voxels generated."));

        return Task.FromResult(VoxelizationResult.Success(grid, stats, null, prep.Warnings));
    }
}
