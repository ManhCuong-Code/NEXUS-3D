using System.Numerics;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Rendering.Domain;

namespace NEXUS3D.Rendering.Services;

/// <summary>
/// Builds high-performance, culled and chunk-partitioned 3D mesh representation of VoxelGrids for GPU rendering.
/// </summary>
public sealed class VoxelRenderBuilder
{
    private static readonly (int dx, int dy, int dz, Vector3 normal)[] Faces =
    [
        (0, 1, 0, Vector3.UnitY),   // Top
        (0, -1, 0, -Vector3.UnitY), // Bottom
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

        var positions = new List<Vector3>(voxels.Count * 12);
        var normals = new List<Vector3>(voxels.Count * 12);
        var uvs = new List<Vector2>(voxels.Count * 12);
        var colors = new List<Vector4>(voxels.Count * 12);
        var indices = new List<int>(voxels.Count * 18);
        var wireframe = new List<int>(voxels.Count * 24);

        float vx = (float)grid.VoxelSize.X;
        float vy = (float)grid.VoxelSize.Y;
        float vz = (float)grid.VoxelSize.Z;
        if (vx <= 0) vx = 1f;
        if (vy <= 0) vy = 1f;
        if (vz <= 0) vz = 1f;

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);

        foreach (var voxel in voxels)
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

            min = Vector3.Min(min, new Vector3(x0, y0, z0));
            max = Vector3.Max(max, new Vector3(x1, y1, z1));

            var color = ResolveVoxelColor(voxel);

            for (int f = 0; f < 6; f++)
            {
                var (dx, dy, dz, normal) = Faces[f];

                // Hidden face culling
                if (enableCulling && voxelSet.Contains((x + dx, y + dy, z + dz)))
                {
                    continue;
                }

                int baseIdx = positions.Count;

                switch (f)
                {
                    case 0: // Top (+Y)
                        positions.Add(new Vector3(x0, y1, z1));
                        positions.Add(new Vector3(x1, y1, z1));
                        positions.Add(new Vector3(x1, y1, z0));
                        positions.Add(new Vector3(x0, y1, z0));
                        break;
                    case 1: // Bottom (-Y)
                        positions.Add(new Vector3(x0, y0, z0));
                        positions.Add(new Vector3(x1, y0, z0));
                        positions.Add(new Vector3(x1, y0, z1));
                        positions.Add(new Vector3(x0, y0, z1));
                        break;
                    case 2: // North (+Z)
                        positions.Add(new Vector3(x0, y0, z1));
                        positions.Add(new Vector3(x1, y0, z1));
                        positions.Add(new Vector3(x1, y1, z1));
                        positions.Add(new Vector3(x0, y1, z1));
                        break;
                    case 3: // South (-Z)
                        positions.Add(new Vector3(x1, y0, z0));
                        positions.Add(new Vector3(x0, y0, z0));
                        positions.Add(new Vector3(x0, y1, z0));
                        positions.Add(new Vector3(x1, y1, z0));
                        break;
                    case 4: // East (+X)
                        positions.Add(new Vector3(x1, y0, z1));
                        positions.Add(new Vector3(x1, y0, z0));
                        positions.Add(new Vector3(x1, y1, z0));
                        positions.Add(new Vector3(x1, y1, z1));
                        break;
                    case 5: // West (-X)
                        positions.Add(new Vector3(x0, y0, z0));
                        positions.Add(new Vector3(x0, y0, z1));
                        positions.Add(new Vector3(x0, y1, z1));
                        positions.Add(new Vector3(x0, y1, z0));
                        break;
                }

                for (int v = 0; v < 4; v++)
                {
                    normals.Add(normal);
                    colors.Add(color);
                }

                uvs.Add(new Vector2(0, 0));
                uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(1, 1));
                uvs.Add(new Vector2(0, 1));

                // Triangles: (0, 1, 2) and (0, 2, 3)
                indices.Add(baseIdx);
                indices.Add(baseIdx + 1);
                indices.Add(baseIdx + 2);

                indices.Add(baseIdx);
                indices.Add(baseIdx + 2);
                indices.Add(baseIdx + 3);

                // Wireframe edges
                wireframe.Add(baseIdx);
                wireframe.Add(baseIdx + 1);
                wireframe.Add(baseIdx + 1);
                wireframe.Add(baseIdx + 2);
                wireframe.Add(baseIdx + 2);
                wireframe.Add(baseIdx + 3);
                wireframe.Add(baseIdx + 3);
                wireframe.Add(baseIdx);
            }
        }

        var renderMesh = new RenderMesh
        {
            Id = grid.Id.ToString(),
            Name = grid.Name,
            Positions = [.. positions],
            Normals = [.. normals],
            UVs = [.. uvs],
            Colors = [.. colors],
            Indices = [.. indices],
            WireframeIndices = [.. wireframe],
            BoundsMin = min == new Vector3(float.MaxValue) ? Vector3.Zero : min,
            BoundsMax = max == new Vector3(float.MinValue) ? Vector3.Zero : max
        };

        return new RenderVoxelChunk
        {
            Id = grid.Id.ToString(),
            Position = new Vector3((float)grid.Origin.X * vx, (float)grid.Origin.Y * vy, (float)grid.Origin.Z * vz),
            VoxelCount = voxels.Count,
            Mesh = renderMesh
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

    private static Vector4 ResolveVoxelColor(Voxel voxel)
    {
        if (voxel.State is not null)
        {
            // Deterministic hash color based on block name
            int hash = Math.Abs(voxel.State.Name.GetHashCode());
            float r = ((hash & 0xFF) / 255f) * 0.7f + 0.3f;
            float g = (((hash >> 8) & 0xFF) / 255f) * 0.7f + 0.3f;
            float b = (((hash >> 16) & 0xFF) / 255f) * 0.7f + 0.3f;
            return new Vector4(r, g, b, 1.0f);
        }

        return new Vector4(0.7f, 0.7f, 0.75f, 1.0f);
    }
}
