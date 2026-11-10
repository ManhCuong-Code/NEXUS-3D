using System.Diagnostics;
using System.Numerics;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Voxelization.Abstractions.Contracts;
using NEXUS3D.Voxelization.Abstractions.Models;
using NEXUS3D.Voxelization.Engine.Geometry;
using NEXUS3D.Voxelization.Engine.Processing;

namespace NEXUS3D.Voxelization.Engine.Algorithms;

/// <summary>
/// Precision surface rasterization utilizing Tomas Akenine-Möller Separating Axis Theorem (SAT).
/// </summary>
public sealed class SurfaceVoxelization : IVoxelizationAlgorithm
{
    private readonly MeshPreprocessor _preprocessor = new();
    private readonly VoxelMaterialMapper _materialMapper = new();
    private readonly VoxelColorSampler _colorSampler = new();

    public string Name => "Surface Voxelization (Akenine-Möller SAT)";
    public VoxelizationMode SupportedMode => VoxelizationMode.SurfaceOnly;

    public Task<VoxelizationResult> ExecuteAsync(
        MeshData mesh,
        Scene3D? scene,
        IVoxelizationSettings settings,
        IProgress<VoxelizationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        progress?.Report(new VoxelizationProgress("Mesh Preprocessing", 10.0, 0, 1, "Analyzing mesh geometry and manifold topology..."));

        var prep = _preprocessor.Preprocess(mesh);
        if (prep.Triangles.Count == 0)
        {
            return Task.FromResult(VoxelizationResult.Failure("Mesh contains no valid triangles.", prep.Warnings));
        }

        // 1. Calculate Grid Dimensions and Voxel Size
        var (dimX, dimY, dimZ, voxelSize, origin) = CalculateGridBounds(prep.BoundsMin, prep.BoundsMax, settings);

        long totalPossibleCells = (long)dimX * dimY * dimZ;
        if (totalPossibleCells > settings.MaxVoxelCount * 4)
        {
            return Task.FromResult(VoxelizationResult.Failure(
                $"Requested grid resolution ({dimX}x{dimY}x{dimZ} = {totalPossibleCells:N0} cells) exceeds safe allocation limits (cap: {settings.MaxVoxelCount:N0}).",
                prep.Warnings));
        }

        // 2. Prepare VoxelGrid
        var grid = new VoxelGrid($"{mesh.Name}_VoxelGrid")
        {
            VoxelSize = new VoxelSize(voxelSize, voxelSize, voxelSize)
        };
        _materialMapper.InitializePalette(grid, scene, settings.DefaultBlockNamespace);

        progress?.Report(new VoxelizationProgress("Surface Rasterization", 30.0, 0, prep.Triangles.Count, $"Rasterizing {prep.Triangles.Count:N0} triangles across {dimX}x{dimY}x{dimZ} grid..."));

        var halfVoxel = new Vector3(voxelSize * 0.5f);
        var occupiedVoxels = new Dictionary<VoxelPosition, List<(WorkingTriangle Tri, float Area)>>();

        int processedTriangles = 0;
        int totalTriangles = prep.Triangles.Count;

        for (int t = 0; t < totalTriangles; t++)
        {
            if (t % 500 == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                double pct = 30.0 + (45.0 * t / totalTriangles);
                progress?.Report(new VoxelizationProgress("Surface Rasterization", pct, t, totalTriangles, $"Processing triangle {t:N0} of {totalTriangles:N0}..."));
            }

            var tri = prep.Triangles[t];
            var (minVoxel, maxVoxel) = SpatialIndex.GetVoxelRange(tri, origin, voxelSize, dimX, dimY, dimZ);

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

                        if (TriangleBoxIntersection.Intersects(tri.V0, tri.V1, tri.V2, cellCenter, halfVoxel))
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

            processedTriangles++;
        }

        progress?.Report(new VoxelizationProgress("Material & Color Mapping", 80.0, occupiedVoxels.Count, occupiedVoxels.Count, $"Resolving materials for {occupiedVoxels.Count:N0} voxels..."));

        // 3. Populate VoxelGrid
        foreach (var (pos, candidates) in occupiedVoxels)
        {
            var dominantMat = VoxelMaterialMapper.ElectDominantMaterial(candidates);
            int palIdx = _materialMapper.ResolvePaletteIndex(dominantMat);
            var entry = grid.Palette.Entries[palIdx];

            var voxel = new Voxel(pos, palIdx, entry.State, dominantMat);
            grid.SetVoxel(voxel);
        }

        sw.Stop();
        var stats = new VoxelizationStatistics
        {
            TotalTrianglesProcessed = processedTriangles,
            InitialVoxelCount = grid.GetVoxelCount(),
            FinalVoxelCount = grid.GetVoxelCount(),
            GridDimensionX = dimX,
            GridDimensionY = dimY,
            GridDimensionZ = dimZ,
            ActualVoxelSize = voxelSize,
            ProcessingTimeMs = sw.ElapsedMilliseconds
        };

        progress?.Report(VoxelizationProgress.Completed($"Surface voxelization generated {grid.GetVoxelCount():N0} voxels in {sw.ElapsedMilliseconds}ms."));

        return Task.FromResult(VoxelizationResult.Success(grid, stats, null, prep.Warnings));
    }

    internal static (int DimX, int DimY, int DimZ, float VoxelSize, Vector3 Origin) CalculateGridBounds(
        Vector3 bMin,
        Vector3 bMax,
        IVoxelizationSettings settings)
    {
        var size = Vector3.Max(bMax - bMin, new Vector3(0.001f));
        float voxelSize;

        if (settings.ResolutionMode == VoxelResolutionMode.ByMaxGridDimension)
        {
            float maxDim = MathF.Max(size.X, MathF.Max(size.Y, size.Z));
            int targetDim = Math.Clamp(settings.MaxGridDimension, 4, 1024);
            voxelSize = MathF.Max(0.001f, maxDim / targetDim);
        }
        else
        {
            voxelSize = (float)Math.Max(0.001, settings.VoxelSize);
        }

        int dimX = Math.Max(1, (int)MathF.Ceiling(size.X / voxelSize));
        int dimY = Math.Max(1, (int)MathF.Ceiling(size.Y / voxelSize));
        int dimZ = Math.Max(1, (int)MathF.Ceiling(size.Z / voxelSize));

        Vector3 origin;
        switch (settings.GridAlignment)
        {
            case VoxelGridAlignment.MinAtOrigin:
                origin = Vector3.Zero;
                break;
            case VoxelGridAlignment.PreserveWorldCoordinates:
                origin = bMin;
                break;
            case VoxelGridAlignment.CenterOnOrigin:
            default:
                var center = (bMin + bMax) * 0.5f;
                var totalGridSize = new Vector3(dimX * voxelSize, dimY * voxelSize, dimZ * voxelSize);
                origin = center - totalGridSize * 0.5f;
                break;
        }

        return (dimX, dimY, dimZ, voxelSize, origin);
    }
}
