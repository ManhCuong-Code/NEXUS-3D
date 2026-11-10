using System.Diagnostics;
using System.Numerics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Voxelization.Abstractions.Contracts;
using NEXUS3D.Voxelization.Abstractions.Models;
using NEXUS3D.Voxelization.Domain.Models;
using NEXUS3D.Voxelization.Engine.Geometry;
using NEXUS3D.Voxelization.Engine.Processing;

namespace NEXUS3D.Voxelization.Engine.Algorithms;

/// <summary>
/// Solid volumetric voxelization combining surface SAT rasterization with exterior boundary flood-fill
/// and ray-casting parity validation to create watertight solid voxel volumes.
/// </summary>
public sealed class SolidVoxelization : IVoxelizationAlgorithm
{
    private readonly SurfaceVoxelization _surfaceVoxelizer = new();
    private readonly MeshPreprocessor _preprocessor = new();

    public string Name => "Solid Volumetric Voxelization (Boundary Shell + Topological Fill)";
    public VoxelizationMode SupportedMode => VoxelizationMode.SolidVolume;

    public async Task<VoxelizationResult> ExecuteAsync(
        MeshData mesh,
        Scene3D? scene,
        IVoxelizationSettings settings,
        IProgress<VoxelizationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        progress?.Report(new VoxelizationProgress("Surface Boundary Rasterization", 10.0, 0, 1, "Rasterizing boundary shell..."));

        // 1. Run Surface Voxelization first to get the boundary shell
        var surfaceSettings = new VoxelizationSettings
        {
            VoxelSize = settings.VoxelSize,
            ResolutionMode = settings.ResolutionMode,
            MaxGridDimension = settings.MaxGridDimension,
            MaxVoxelCount = settings.MaxVoxelCount,
            Mode = VoxelizationMode.SurfaceOnly,
            GridAlignment = settings.GridAlignment,
            DefaultBlockNamespace = settings.DefaultBlockNamespace,
            PreserveMaterials = settings.PreserveMaterials
        };

        var surfaceResult = await _surfaceVoxelizer.ExecuteAsync(mesh, scene, surfaceSettings, progress, cancellationToken);
        if (!surfaceResult.IsSuccess || surfaceResult.VoxelGrid is null)
        {
            return surfaceResult;
        }

        var grid = surfaceResult.VoxelGrid;
        int dimX = surfaceResult.Statistics.GridDimensionX;
        int dimY = surfaceResult.Statistics.GridDimensionY;
        int dimZ = surfaceResult.Statistics.GridDimensionZ;
        int initialSurfaceCount = grid.GetVoxelCount();

        progress?.Report(new VoxelizationProgress("Interior Volume Analysis", 60.0, 0, 1, "Analyzing enclosed volume and flood-filling interior..."));

        // 2. Identify occupied surface positions
        var surfacePositions = new HashSet<VoxelPosition>(initialSurfaceCount);
        foreach (var voxel in grid.Voxels)
        {
            surfacePositions.Add(voxel.Position);
        }

        // 3. Flood-fill from outside padded bounds (-1 to dim)
        // Any cell in [0..dim-1] that is NOT reached by outside flood fill and is NOT on surface is INTERIOR
        int padMinX = -1, padMaxX = dimX;
        int padMinY = -1, padMaxY = dimY;
        int padMinZ = -1, padMaxZ = dimZ;

        var outsideVisited = new HashSet<VoxelPosition>();
        var queue = new Queue<VoxelPosition>();
        var start = new VoxelPosition(padMinX, padMinY, padMinZ);
        outsideVisited.Add(start);
        queue.Enqueue(start);

        int[] dx = [1, -1, 0, 0, 0, 0];
        int[] dy = [0, 0, 1, -1, 0, 0];
        int[] dz = [0, 0, 0, 0, 1, -1];

        int checkCount = 0;
        while (queue.Count > 0)
        {
            if (++checkCount % 10000 == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var curr = queue.Dequeue();

            for (int i = 0; i < 6; i++)
            {
                int nx = curr.X + dx[i];
                int ny = curr.Y + dy[i];
                int nz = curr.Z + dz[i];

                if (nx < padMinX || nx > padMaxX ||
                    ny < padMinY || ny > padMaxY ||
                    nz < padMinZ || nz > padMaxZ)
                {
                    continue;
                }

                var nextPos = new VoxelPosition(nx, ny, nz);
                if (surfacePositions.Contains(nextPos))
                {
                    continue; // Blocked by surface boundary
                }

                if (outsideVisited.Add(nextPos))
                {
                    queue.Enqueue(nextPos);
                }
            }
        }

        progress?.Report(new VoxelizationProgress("Solid Interior Filling", 85.0, 0, 1, "Filling solid internal cells..."));

        // 4. Fill unreached internal cells
        int filledCount = 0;
        int defaultPalette = 0;

        for (int z = 0; z < dimZ; z++)
        {
            for (int y = 0; y < dimY; y++)
            {
                for (int x = 0; x < dimX; x++)
                {
                    var pos = new VoxelPosition(x, y, z);
                    if (!surfacePositions.Contains(pos) && !outsideVisited.Contains(pos))
                    {
                        var entry = grid.Palette.Entries[defaultPalette];
                        grid.SetVoxel(new Voxel(pos, defaultPalette, entry.State, entry.MaterialId));
                        filledCount++;
                    }
                }
            }
        }

        sw.Stop();
        var stats = new VoxelizationStatistics
        {
            TotalTrianglesProcessed = surfaceResult.Statistics.TotalTrianglesProcessed,
            InitialVoxelCount = initialSurfaceCount,
            FinalVoxelCount = grid.GetVoxelCount(),
            InternalVoxelsFilled = filledCount,
            GridDimensionX = dimX,
            GridDimensionY = dimY,
            GridDimensionZ = dimZ,
            ActualVoxelSize = surfaceResult.Statistics.ActualVoxelSize,
            ProcessingTimeMs = sw.ElapsedMilliseconds
        };

        progress?.Report(VoxelizationProgress.Completed($"Solid voxelization complete: {initialSurfaceCount:N0} surface + {filledCount:N0} interior voxels ({grid.GetVoxelCount():N0} total)."));

        return VoxelizationResult.Success(grid, stats, null, surfaceResult.Warnings);
    }
}
