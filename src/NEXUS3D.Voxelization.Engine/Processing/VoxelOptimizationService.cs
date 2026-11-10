using NEXUS3D.Domain.Voxels;

namespace NEXUS3D.Voxelization.Engine.Processing;

/// <summary>
/// Post-processing service for voxel cleanup, isolated particle pruning, and gap filling.
/// </summary>
public sealed class VoxelOptimizationService
{
    private static readonly int[] Dx = [1, -1, 0, 0, 0, 0];
    private static readonly int[] Dy = [0, 0, 1, -1, 0, 0];
    private static readonly int[] Dz = [0, 0, 0, 0, 1, -1];

    /// <summary>
    /// Removes isolated single voxels having zero or only one adjacent neighbor.
    /// </summary>
    public int PruneIsolatedVoxels(VoxelGrid grid)
    {
        var positionsToRemove = new List<VoxelPosition>();
        var allVoxels = grid.Voxels.ToList();

        foreach (var voxel in allVoxels)
        {
            int neighborCount = 0;
            var pos = voxel.Position;

            for (int i = 0; i < 6; i++)
            {
                var nPos = new VoxelPosition(pos.X + Dx[i], pos.Y + Dy[i], pos.Z + Dz[i]);
                if (grid.TryGetVoxel(nPos, out _))
                {
                    neighborCount++;
                }
            }

            if (neighborCount <= 1)
            {
                positionsToRemove.Add(pos);
            }
        }

        foreach (var pos in positionsToRemove)
        {
            grid.RemoveVoxel(pos);
        }

        return positionsToRemove.Count;
    }

    /// <summary>
    /// Fills single 1-cell voids completely enclosed by 5 or 6 solid neighbors.
    /// </summary>
    public int FillInternalGaps(VoxelGrid grid)
    {
        grid.RecalculateBounds();
        var bounds = grid.Bounds;
        if (bounds.IsEmpty) return 0;

        var (min, max) = (bounds.Min, bounds.Max);
        var cellsToFill = new List<(VoxelPosition Pos, int PaletteIndex)>();

        for (int z = min.Z; z <= max.Z; z++)
        {
            for (int y = min.Y; y <= max.Y; y++)
            {
                for (int x = min.X; x <= max.X; x++)
                {
                    var pos = new VoxelPosition(x, y, z);
                    if (grid.TryGetVoxel(pos, out _)) continue;

                    int neighborCount = 0;
                    int dominantPalette = 0;

                    for (int i = 0; i < 6; i++)
                    {
                        var nPos = new VoxelPosition(pos.X + Dx[i], pos.Y + Dy[i], pos.Z + Dz[i]);
                        if (grid.TryGetVoxel(nPos, out var neighbor) && neighbor is not null)
                        {
                            neighborCount++;
                            dominantPalette = neighbor.PaletteIndex;
                        }
                    }

                    if (neighborCount >= 5)
                    {
                        cellsToFill.Add((pos, dominantPalette));
                    }
                }
            }
        }

        foreach (var (pos, palIdx) in cellsToFill)
        {
            var entry = grid.Palette.Entries[palIdx];
            grid.SetVoxel(new Voxel(pos, palIdx, entry.State, entry.MaterialId));
        }

        return cellsToFill.Count;
    }
}
