using System.Diagnostics;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Voxelization.Abstractions.Contracts;
using NEXUS3D.Voxelization.Abstractions.Models;

namespace NEXUS3D.Voxelization.Engine.Conversion;

/// <summary>
/// Greedy 3D box merger collapsing contiguous voxels of the same material into maximal Axis-Aligned Bounding Cuboids.
/// </summary>
public sealed class VoxelToCuboidConverter : IVoxelToCuboidConverter
{
    public Task<CuboidOptimizationResult> ConvertToCuboidsAsync(
        VoxelGrid grid,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        int initialCount = grid.GetVoxelCount();
        if (initialCount == 0)
        {
            return Task.FromResult(new CuboidOptimizationResult(0, 0, [], 0.0, 0));
        }

        // 1. Group voxel positions by PaletteIndex
        var voxelsByPalette = new Dictionary<int, HashSet<VoxelPosition>>();
        foreach (var voxel in grid.Voxels)
        {
            if (!voxelsByPalette.TryGetValue(voxel.PaletteIndex, out var set))
            {
                set = [];
                voxelsByPalette[voxel.PaletteIndex] = set;
            }
            set.Add(voxel.Position);
        }

        var cuboids = new List<VoxelCuboid>();

        // 2. Greedy merge per material group
        foreach (var (paletteIdx, positionSet) in voxelsByPalette)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var unvisited = new HashSet<VoxelPosition>(positionSet);

            var entry = grid.Palette.Entries[paletteIdx];
            var state = entry.State;
            var matId = entry.MaterialId;

            while (unvisited.Count > 0)
            {
                // Pick lowest coordinate (Min X, Min Y, Min Z)
                var min = FindMinPosition(unvisited);

                // Expand along X
                int sizeX = 1;
                while (unvisited.Contains(new VoxelPosition(min.X + sizeX, min.Y, min.Z)))
                {
                    sizeX++;
                }

                // Expand along Y (all X must be present)
                int sizeY = 1;
                bool canExpandY = true;
                while (canExpandY)
                {
                    int checkY = min.Y + sizeY;
                    for (int x = 0; x < sizeX; x++)
                    {
                        if (!unvisited.Contains(new VoxelPosition(min.X + x, checkY, min.Z)))
                        {
                            canExpandY = false;
                            break;
                        }
                    }
                    if (canExpandY)
                    {
                        sizeY++;
                    }
                }

                // Expand along Z (entire X x Y slab must be present)
                int sizeZ = 1;
                bool canExpandZ = true;
                while (canExpandZ)
                {
                    int checkZ = min.Z + sizeZ;
                    for (int y = 0; y < sizeY; y++)
                    {
                        for (int x = 0; x < sizeX; x++)
                        {
                            if (!unvisited.Contains(new VoxelPosition(min.X + x, min.Y + y, checkZ)))
                            {
                                canExpandZ = false;
                                break;
                            }
                        }
                        if (!canExpandZ) break;
                    }
                    if (canExpandZ)
                    {
                        sizeZ++;
                    }
                }

                // Remove all merged voxels from unvisited set
                var max = new VoxelPosition(min.X + sizeX - 1, min.Y + sizeY - 1, min.Z + sizeZ - 1);
                for (int z = min.Z; z <= max.Z; z++)
                {
                    for (int y = min.Y; y <= max.Y; y++)
                    {
                        for (int x = min.X; x <= max.X; x++)
                        {
                            unvisited.Remove(new VoxelPosition(x, y, z));
                        }
                    }
                }

                cuboids.Add(new VoxelCuboid(min, max, paletteIdx, state, matId));
            }
        }

        sw.Stop();
        double reduction = initialCount > 0
            ? (1.0 - ((double)cuboids.Count / initialCount)) * 100.0
            : 0.0;

        return Task.FromResult(new CuboidOptimizationResult(
            initialCount,
            cuboids.Count,
            cuboids,
            reduction,
            sw.ElapsedMilliseconds));
    }

    private static VoxelPosition FindMinPosition(HashSet<VoxelPosition> positions)
    {
        var min = positions.First();
        foreach (var p in positions)
        {
            if (p.Z < min.Z ||
                (p.Z == min.Z && p.Y < min.Y) ||
                (p.Z == min.Z && p.Y == min.Y && p.X < min.X))
            {
                min = p;
            }
        }
        return min;
    }
}
