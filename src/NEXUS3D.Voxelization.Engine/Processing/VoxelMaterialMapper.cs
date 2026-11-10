using NEXUS3D.Domain.Materials;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Voxels;

namespace NEXUS3D.Voxelization.Engine.Processing;

/// <summary>
/// Maps USM Materials to VoxelPalette entries and resolves dominant material elections.
/// </summary>
public sealed class VoxelMaterialMapper
{
    private readonly Dictionary<Guid, int> _materialToPaletteMap = [];

    public void InitializePalette(VoxelGrid grid, Scene3D? scene, string defaultBlockNamespace)
    {
        _materialToPaletteMap.Clear();

        // Add default block state
        var defaultState = ParseBlockState(defaultBlockNamespace);
        grid.Palette.Add(new VoxelPaletteEntry(defaultState, displayName: "Default Voxel"));

        if (scene is null) return;

        foreach (var mat in scene.Materials.Materials)
        {
            string blockName = CleanBlockIdentifier(mat.Name, defaultBlockNamespace);
            var state = ParseBlockState(blockName);
            int idx = grid.Palette.Add(new VoxelPaletteEntry(state, mat.Id, mat.Name));
            _materialToPaletteMap[mat.Id] = idx;
        }
    }

    public int ResolvePaletteIndex(Guid? materialId, int defaultIndex = 0)
    {
        if (materialId.HasValue && _materialToPaletteMap.TryGetValue(materialId.Value, out int idx))
        {
            return idx;
        }
        return defaultIndex;
    }

    /// <summary>
    /// Chooses the dominant material among multiple intersecting triangles based on cumulative surface area.
    /// </summary>
    public static Guid? ElectDominantMaterial(IReadOnlyList<(WorkingTriangle Tri, float IntersectArea)> candidates)
    {
        if (candidates.Count == 0) return null;
        if (candidates.Count == 1) return candidates[0].Tri.MaterialId;

        var areaByMat = new Dictionary<Guid, float>();
        Guid? maxMat = null;
        float maxArea = -1f;

        foreach (var (tri, area) in candidates)
        {
            if (!tri.MaterialId.HasValue) continue;
            var id = tri.MaterialId.Value;
            float total = areaByMat.GetValueOrDefault(id) + area;
            areaByMat[id] = total;

            if (total > maxArea)
            {
                maxArea = total;
                maxMat = id;
            }
        }

        return maxMat ?? candidates[0].Tri.MaterialId;
    }

    public static BlockState ParseBlockState(string blockName)
    {
        if (string.IsNullOrWhiteSpace(blockName))
        {
            return new BlockState("minecraft", "stone");
        }

        string ns = "minecraft";
        string name = blockName.Trim();

        int colon = name.IndexOf(':');
        if (colon >= 0)
        {
            ns = name[..colon];
            name = name[(colon + 1)..];
        }

        return new BlockState(ns, name);
    }

    private static string CleanBlockIdentifier(string name, string fallback)
    {
        if (string.IsNullOrWhiteSpace(name)) return fallback;

        string clean = name.ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
        if (!clean.Contains(':'))
        {
            clean = $"minecraft:{clean}";
        }
        return clean;
    }
}
