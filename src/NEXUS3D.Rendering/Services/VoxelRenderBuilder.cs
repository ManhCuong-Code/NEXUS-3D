using System.Numerics;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Rendering.Domain;

namespace NEXUS3D.Rendering.Services;

/// <summary>
/// Builds high-performance, culled, multi-material representation of VoxelGrids for GPU rendering,
/// preserving accurate Minecraft block colors and materials.
/// </summary>
public sealed class VoxelRenderBuilder
{
    private static readonly (int dx, int dy, int dz, Vector3 normal)[] Faces =
    [
        (0, 1, 0, Vector3.UnitY),   // Top (+Y)
        (0, -1, 0, -Vector3.UnitY), // Bottom (-Y)
        (0, 0, 1, Vector3.UnitZ),   // North (+Z)
        (0, 0, -1, -Vector3.UnitZ), // South (-Z)
        (1, 0, 0, Vector3.UnitX),   // East (+X)
        (-1, 0, 0, -Vector3.UnitX)  // West (-X)
    ];

    public RenderVoxelChunk Build(VoxelGrid grid, bool enableCulling = true)
    {
        if (grid is null || grid.GetVoxelCount() == 0)
        {
            return new RenderVoxelChunk
            {
                VoxelCount = 0
            };
        }

        var voxels = grid.Voxels;
        var voxelSet = new HashSet<(int X, int Y, int Z)>(voxels.Count);
        foreach (var v in voxels)
        {
            voxelSet.Add((v.Position.X, v.Position.Y, v.Position.Z));
        }

        float vx = (float)grid.VoxelSize.X;
        float vy = (float)grid.VoxelSize.Y;
        float vz = (float)grid.VoxelSize.Z;
        if (vx <= 0) vx = 1f;
        if (vy <= 0) vy = 1f;
        if (vz <= 0) vz = 1f;

        var globalMin = new Vector3(float.MaxValue);
        var globalMax = new Vector3(float.MinValue);

        // Group voxels by distinct block identifier for multi-material rendering
        var voxelsByBlock = new Dictionary<string, List<Voxel>>(StringComparer.OrdinalIgnoreCase);
        foreach (var voxel in voxels)
        {
            string blockKey = voxel.State?.GetFullIdentifier() ?? "minecraft:stone";
            if (!voxelsByBlock.TryGetValue(blockKey, out var list))
            {
                list = [];
                voxelsByBlock[blockKey] = list;
            }
            list.Add(voxel);
        }

        var subMeshes = new List<RenderMesh>(voxelsByBlock.Count);
        var combinedPositions = new List<Vector3>(voxels.Count * 12);
        var combinedNormals = new List<Vector3>(voxels.Count * 12);
        var combinedUVs = new List<Vector2>(voxels.Count * 12);
        var combinedColors = new List<Vector4>(voxels.Count * 12);
        var combinedIndices = new List<int>(voxels.Count * 18);
        var combinedWireframe = new List<int>(voxels.Count * 24);

        foreach (var (blockKey, blockVoxels) in voxelsByBlock)
        {
            var blockInfo = BlockMaterialRegistry.GetBlockInfo(blockKey);
            var color = blockInfo.Color;

            var bPositions = new List<Vector3>(blockVoxels.Count * 12);
            var bNormals = new List<Vector3>(blockVoxels.Count * 12);
            var bUvs = new List<Vector2>(blockVoxels.Count * 12);
            var bColors = new List<Vector4>(blockVoxels.Count * 12);
            var bIndices = new List<int>(blockVoxels.Count * 18);
            var bWireframe = new List<int>(blockVoxels.Count * 24);

            var bMin = new Vector3(float.MaxValue);
            var bMax = new Vector3(float.MinValue);

            foreach (var voxel in blockVoxels)
            {
                int x = voxel.Position.X;
                int y = voxel.Position.Y;
                int z = voxel.Position.Z;

                float x0 = x * vx;
                float y0 = y * vy;
                float z0 = z * vz;
                float x1 = x0 + vx;
                float y1 = y0 + vy;
                float z1 = z0 + vz;

                bMin = Vector3.Min(bMin, new Vector3(x0, y0, z0));
                bMax = Vector3.Max(bMax, new Vector3(x1, y1, z1));
                globalMin = Vector3.Min(globalMin, new Vector3(x0, y0, z0));
                globalMax = Vector3.Max(globalMax, new Vector3(x1, y1, z1));

                for (int f = 0; f < 6; f++)
                {
                    var (dx, dy, dz, normal) = Faces[f];

                    // Hidden face culling against all solid neighbors
                    if (enableCulling && voxelSet.Contains((x + dx, y + dy, z + dz)))
                    {
                        continue;
                    }

                    int bBaseIdx = bPositions.Count;
                    int combBaseIdx = combinedPositions.Count;

                    Vector3 p0, p1, p2, p3;
                    switch (f)
                    {
                        case 0: // Top (+Y)
                            p0 = new Vector3(x0, y1, z1);
                            p1 = new Vector3(x1, y1, z1);
                            p2 = new Vector3(x1, y1, z0);
                            p3 = new Vector3(x0, y1, z0);
                            break;
                        case 1: // Bottom (-Y)
                            p0 = new Vector3(x0, y0, z0);
                            p1 = new Vector3(x1, y0, z0);
                            p2 = new Vector3(x1, y0, z1);
                            p3 = new Vector3(x0, y0, z1);
                            break;
                        case 2: // North (+Z)
                            p0 = new Vector3(x0, y0, z1);
                            p1 = new Vector3(x1, y0, z1);
                            p2 = new Vector3(x1, y1, z1);
                            p3 = new Vector3(x0, y1, z1);
                            break;
                        case 3: // South (-Z)
                            p0 = new Vector3(x1, y0, z0);
                            p1 = new Vector3(x0, y0, z0);
                            p2 = new Vector3(x0, y1, z0);
                            p3 = new Vector3(x1, y1, z0);
                            break;
                        case 4: // East (+X)
                            p0 = new Vector3(x1, y0, z1);
                            p1 = new Vector3(x1, y0, z0);
                            p2 = new Vector3(x1, y1, z0);
                            p3 = new Vector3(x1, y1, z1);
                            break;
                        default: // West (-X)
                            p0 = new Vector3(x0, y0, z0);
                            p1 = new Vector3(x0, y0, z1);
                            p2 = new Vector3(x0, y1, z1);
                            p3 = new Vector3(x0, y1, z0);
                            break;
                    }

                    // Add to submesh
                    bPositions.Add(p0); bPositions.Add(p1); bPositions.Add(p2); bPositions.Add(p3);
                    combinedPositions.Add(p0); combinedPositions.Add(p1); combinedPositions.Add(p2); combinedPositions.Add(p3);

                    for (int v = 0; v < 4; v++)
                    {
                        bNormals.Add(normal);
                        bColors.Add(color);
                        combinedNormals.Add(normal);
                        combinedColors.Add(color);
                    }

                    var uv0 = new Vector2(0, 0);
                    var uv1 = new Vector2(1, 0);
                    var uv2 = new Vector2(1, 1);
                    var uv3 = new Vector2(0, 1);

                    bUvs.Add(uv0); bUvs.Add(uv1); bUvs.Add(uv2); bUvs.Add(uv3);
                    combinedUVs.Add(uv0); combinedUVs.Add(uv1); combinedUVs.Add(uv2); combinedUVs.Add(uv3);

                    // Submesh Triangles
                    bIndices.Add(bBaseIdx); bIndices.Add(bBaseIdx + 1); bIndices.Add(bBaseIdx + 2);
                    bIndices.Add(bBaseIdx); bIndices.Add(bBaseIdx + 2); bIndices.Add(bBaseIdx + 3);

                    // Submesh Wireframe
                    bWireframe.Add(bBaseIdx); bWireframe.Add(bBaseIdx + 1);
                    bWireframe.Add(bBaseIdx + 1); bWireframe.Add(bBaseIdx + 2);
                    bWireframe.Add(bBaseIdx + 2); bWireframe.Add(bBaseIdx + 3);
                    bWireframe.Add(bBaseIdx + 3); bWireframe.Add(bBaseIdx);

                    // Combined Triangles & Wireframe
                    combinedIndices.Add(combBaseIdx); combinedIndices.Add(combBaseIdx + 1); combinedIndices.Add(combBaseIdx + 2);
                    combinedIndices.Add(combBaseIdx); combinedIndices.Add(combBaseIdx + 2); combinedIndices.Add(combBaseIdx + 3);
                    combinedWireframe.Add(combBaseIdx); combinedWireframe.Add(combBaseIdx + 1);
                    combinedWireframe.Add(combBaseIdx + 1); combinedWireframe.Add(combBaseIdx + 2);
                    combinedWireframe.Add(combBaseIdx + 2); combinedWireframe.Add(combBaseIdx + 3);
                    combinedWireframe.Add(combBaseIdx + 3); combinedWireframe.Add(combBaseIdx);
                }
            }

            if (bPositions.Count > 0)
            {
                var blockRenderMesh = new RenderMesh
                {
                    Id = $"{grid.Id}_{blockKey}",
                    Name = blockInfo.DisplayName,
                    Positions = [.. bPositions],
                    Normals = [.. bNormals],
                    UVs = [.. bUvs],
                    Colors = [.. bColors],
                    Indices = [.. bIndices],
                    WireframeIndices = [.. bWireframe],
                    MaterialId = $"block:{blockKey}",
                    BoundsMin = bMin == new Vector3(float.MaxValue) ? Vector3.Zero : bMin,
                    BoundsMax = bMax == new Vector3(float.MinValue) ? Vector3.Zero : bMax
                };
                subMeshes.Add(blockRenderMesh);
            }
        }

        var consolidatedMesh = new RenderMesh
        {
            Id = grid.Id.ToString(),
            Name = grid.Name,
            Positions = [.. combinedPositions],
            Normals = [.. combinedNormals],
            UVs = [.. combinedUVs],
            Colors = [.. combinedColors],
            Indices = [.. combinedIndices],
            WireframeIndices = [.. combinedWireframe],
            BoundsMin = globalMin == new Vector3(float.MaxValue) ? Vector3.Zero : globalMin,
            BoundsMax = globalMax == new Vector3(float.MinValue) ? Vector3.Zero : globalMax
        };

        return new RenderVoxelChunk
        {
            Id = grid.Id.ToString(),
            Position = new Vector3((float)grid.Origin.X * vx, (float)grid.Origin.Y * vy, (float)grid.Origin.Z * vz),
            VoxelCount = voxels.Count,
            Mesh = consolidatedMesh,
            SubMeshes = subMeshes
        };
    }

    /// <summary>
    /// Divides a large voxel grid into 16x16x16 sub-chunks for spatial partitioning and fast frustum rendering.
    /// </summary>
    public List<RenderVoxelChunk> BuildSpatialChunks(VoxelGrid grid, int chunkSize = 16)
    {
        var result = new List<RenderVoxelChunk>();
        if (grid is null || grid.GetVoxelCount() == 0) return result;

        var chunkGroups = new Dictionary<(int CX, int CY, int CZ), List<Voxel>>();
        foreach (var v in grid.Voxels)
        {
            int cx = (int)Math.Floor((float)v.Position.X / chunkSize);
            int cy = (int)Math.Floor((float)v.Position.Y / chunkSize);
            int cz = (int)Math.Floor((float)v.Position.Z / chunkSize);

            var key = (cx, cy, cz);
            if (!chunkGroups.TryGetValue(key, out var list))
            {
                list = [];
                chunkGroups[key] = list;
            }
            list.Add(v);
        }

        foreach (var (key, chunkVoxels) in chunkGroups)
        {
            var subGrid = new VoxelGrid($"Chunk_{key.CX}_{key.CY}_{key.CZ}")
            {
                VoxelSize = grid.VoxelSize
            };
            foreach (var v in chunkVoxels)
            {
                subGrid.SetVoxel(v);
            }
            result.Add(Build(subGrid, enableCulling: true));
        }

        return result;
    }

    public static Vector4 ResolveVoxelColor(Voxel voxel)
    {
        if (voxel.State is not null)
        {
            return BlockMaterialRegistry.GetBlockInfo(voxel.State).Color;
        }

        return new Vector4(0.5f, 0.5f, 0.5f, 1.0f);
    }
}
