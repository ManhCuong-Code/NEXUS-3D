using Microsoft.Extensions.Logging;
using NEXUS3D.Conversion.Abstractions;
using NEXUS3D.Conversion.Abstractions.Interfaces;
using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Shared.Guards;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Conversion.Engine.Services;

/// <summary>
/// Converts discrete volumetric <see cref="VoxelGrid"/> structures into surface polygon meshes
/// supporting simple quads, hidden internal face culling, and full 3D greedy meshing.
/// </summary>
public sealed class VoxelToMeshConverter : IVoxelToMeshConverter
{
    private readonly ILogger<VoxelToMeshConverter> _logger;

    public VoxelToMeshConverter(ILogger<VoxelToMeshConverter> logger)
    {
        _logger = Guard.NotNull(logger);
    }

    public OperationResult<MeshData> ConvertVoxelGridToMesh(
        VoxelGrid grid,
        VoxelToMeshOptions? options = null)
    {
        Guard.NotNull(grid);
        options ??= new VoxelToMeshOptions();

        string meshName = $"{grid.Name}_Mesh";
        var mesh = new MeshData(meshName, MeshTopology.Polygons);

        if (grid.GetVoxelCount() == 0)
        {
            return OperationResult<MeshData>.Success(mesh);
        }

        double sx = grid.VoxelSize.X > 0 ? grid.VoxelSize.X : options.BlockSize;
        double sy = grid.VoxelSize.Y > 0 ? grid.VoxelSize.Y : options.BlockSize;
        double sz = grid.VoxelSize.Z > 0 ? grid.VoxelSize.Z : options.BlockSize;

        switch (options.Algorithm)
        {
            case MeshingAlgorithm.GreedyMeshing:
                RunGreedyMeshing(grid, mesh, sx, sy, sz, options);
                break;

            case MeshingAlgorithm.ExposedFaceCulling:
                RunExposedFaceCulling(grid, mesh, sx, sy, sz, options);
                break;

            default:
                RunSimpleBlockQuads(grid, mesh, sx, sy, sz, options);
                break;
        }

        _logger.LogInformation(
            "Meshed VoxelGrid '{GridName}' using {Algorithm}: {Vertices} vertices, {Faces} faces.",
            grid.Name,
            options.Algorithm,
            mesh.Vertices.Count,
            mesh.Faces.Count);

        return OperationResult<MeshData>.Success(mesh);
    }

    private static void RunSimpleBlockQuads(
        VoxelGrid grid,
        MeshData mesh,
        double sx,
        double sy,
        double sz,
        VoxelToMeshOptions options)
    {
        foreach (var voxel in grid.Voxels)
        {
            if (voxel.IsAir) continue;

            Guid? matId = voxel.MaterialId;
            if (!matId.HasValue && grid.Palette.TryGet(voxel.PaletteIndex, out var pal))
            {
                matId = pal?.MaterialId;
            }

            int x = voxel.Position.X;
            int y = voxel.Position.Y;
            int z = voxel.Position.Z;

            // Generate all 6 faces
            AddQuad(mesh, new Vector3D(x, y, z + 1), new Vector3D(x + 1, y, z + 1), new Vector3D(x + 1, y + 1, z + 1), new Vector3D(x, y + 1, z + 1), new Vector3D(0, 0, 1), sx, sy, sz, matId, options); // North / +Z
            AddQuad(mesh, new Vector3D(x + 1, y, z), new Vector3D(x, y, z), new Vector3D(x, y + 1, z), new Vector3D(x + 1, y + 1, z), new Vector3D(0, 0, -1), sx, sy, sz, matId, options); // South / -Z
            AddQuad(mesh, new Vector3D(x + 1, y, z + 1), new Vector3D(x + 1, y, z), new Vector3D(x + 1, y + 1, z), new Vector3D(x + 1, y + 1, z + 1), new Vector3D(1, 0, 0), sx, sy, sz, matId, options); // East / +X
            AddQuad(mesh, new Vector3D(x, y, z), new Vector3D(x, y, z + 1), new Vector3D(x, y + 1, z + 1), new Vector3D(x, y + 1, z), new Vector3D(-1, 0, 0), sx, sy, sz, matId, options); // West / -X
            AddQuad(mesh, new Vector3D(x, y + 1, z + 1), new Vector3D(x + 1, y + 1, z + 1), new Vector3D(x + 1, y + 1, z), new Vector3D(x, y + 1, z), new Vector3D(0, 1, 0), sx, sy, sz, matId, options); // Up / +Y
            AddQuad(mesh, new Vector3D(x, y, z), new Vector3D(x + 1, y, z), new Vector3D(x + 1, y, z + 1), new Vector3D(x, y, z + 1), new Vector3D(0, -1, 0), sx, sy, sz, matId, options); // Down / -Y
        }
    }

    private static void RunExposedFaceCulling(
        VoxelGrid grid,
        MeshData mesh,
        double sx,
        double sy,
        double sz,
        VoxelToMeshOptions options)
    {
        foreach (var voxel in grid.Voxels)
        {
            if (voxel.IsAir) continue;

            Guid? matId = voxel.MaterialId;
            if (!matId.HasValue && grid.Palette.TryGet(voxel.PaletteIndex, out var pal))
            {
                matId = pal?.MaterialId;
            }

            int x = voxel.Position.X;
            int y = voxel.Position.Y;
            int z = voxel.Position.Z;

            // +Z face
            if (IsExposed(grid, x, y, z + 1))
                AddQuad(mesh, new Vector3D(x, y, z + 1), new Vector3D(x + 1, y, z + 1), new Vector3D(x + 1, y + 1, z + 1), new Vector3D(x, y + 1, z + 1), new Vector3D(0, 0, 1), sx, sy, sz, matId, options);

            // -Z face
            if (IsExposed(grid, x, y, z - 1))
                AddQuad(mesh, new Vector3D(x + 1, y, z), new Vector3D(x, y, z), new Vector3D(x, y + 1, z), new Vector3D(x + 1, y + 1, z), new Vector3D(0, 0, -1), sx, sy, sz, matId, options);

            // +X face
            if (IsExposed(grid, x + 1, y, z))
                AddQuad(mesh, new Vector3D(x + 1, y, z + 1), new Vector3D(x + 1, y, z), new Vector3D(x + 1, y + 1, z), new Vector3D(x + 1, y + 1, z + 1), new Vector3D(1, 0, 0), sx, sy, sz, matId, options);

            // -X face
            if (IsExposed(grid, x - 1, y, z))
                AddQuad(mesh, new Vector3D(x, y, z), new Vector3D(x, y, z + 1), new Vector3D(x, y + 1, z + 1), new Vector3D(x, y + 1, z), new Vector3D(-1, 0, 0), sx, sy, sz, matId, options);

            // +Y face
            if (IsExposed(grid, x, y + 1, z))
                AddQuad(mesh, new Vector3D(x, y + 1, z + 1), new Vector3D(x + 1, y + 1, z + 1), new Vector3D(x + 1, y + 1, z), new Vector3D(x, y + 1, z), new Vector3D(0, 1, 0), sx, sy, sz, matId, options);

            // -Y face
            if (IsExposed(grid, x, y - 1, z))
                AddQuad(mesh, new Vector3D(x, y, z), new Vector3D(x + 1, y, z), new Vector3D(x + 1, y, z + 1), new Vector3D(x, y, z + 1), new Vector3D(0, -1, 0), sx, sy, sz, matId, options);
        }
    }

    private static void RunGreedyMeshing(
        VoxelGrid grid,
        MeshData mesh,
        double sx,
        double sy,
        double sz,
        VoxelToMeshOptions options)
    {
        // 3D Greedy Meshing across all 3 primary axes (d = 0: X, 1: Y, 2: Z)
        grid.RecalculateBounds();
        var bounds = grid.Bounds;
        if (!bounds.IsValid)
        {
            RunExposedFaceCulling(grid, mesh, sx, sy, sz, options);
            return;
        }

        int minX = bounds.Min.X;
        int minY = bounds.Min.Y;
        int minZ = bounds.Min.Z;
        int sizeX = (int)bounds.Width;
        int sizeY = (int)bounds.Height;
        int sizeZ = (int)bounds.Depth;

        // If bounds are excessively large for dense slices (> 512 in any axis), fall back to exposed face culling
        if (sizeX > 512 || sizeY > 512 || sizeZ > 512)
        {
            RunExposedFaceCulling(grid, mesh, sx, sy, sz, options);
            return;
        }

        // Iterate over axes: 0=X, 1=Y, 2=Z
        for (int d = 0; d < 3; d++)
        {
            int u = (d + 1) % 3;
            int v = (d + 2) % 3;

            int[] x = [0, 0, 0];
            int[] q = [0, 0, 0];
            q[d] = 1;

            int[] dims = [sizeX, sizeY, sizeZ];
            int[] mins = [minX, minY, minZ];

            int dimD = dims[d];
            int dimU = dims[u];
            int dimV = dims[v];

            // Mask: positive sign for front-facing, negative sign for back-facing
            // Holds (paletteIndex + 1) * directionSign
            int[] mask = new int[dimU * dimV];

            // Sweep slice along dimension d
            for (x[d] = -1; x[d] < dimD;)
            {
                int n = 0;
                for (x[v] = 0; x[v] < dimV; x[v]++)
                {
                    for (x[u] = 0; x[u] < dimU; x[u]++)
                    {
                        var posA = new VoxelPosition(x[0] + mins[0], x[1] + mins[1], x[2] + mins[2]);
                        var posB = new VoxelPosition(x[0] + mins[0] + q[0], x[1] + mins[1] + q[1], x[2] + mins[2] + q[2]);

                        bool hasA = grid.TryGetVoxel(posA, out var voxA) && !voxA!.IsAir;
                        bool hasB = grid.TryGetVoxel(posB, out var voxB) && !voxB!.IsAir;

                        if (hasA == hasB)
                        {
                            mask[n++] = 0;
                        }
                        else if (hasA)
                        {
                            mask[n++] = voxA!.PaletteIndex + 1; // +normal
                        }
                        else
                        {
                            mask[n++] = -(voxB!.PaletteIndex + 1); // -normal
                        }
                    }
                }

                x[d]++;

                // Generate quads from 2D mask
                n = 0;
                for (int j = 0; j < dimV; j++)
                {
                    for (int i = 0; i < dimU;)
                    {
                        int val = mask[n];
                        if (val != 0)
                        {
                            // Compute width w
                            int w;
                            for (w = 1; i + w < dimU && mask[n + w] == val; w++)
                            {
                            }

                            // Compute height h
                            int h;
                            bool done = false;
                            for (h = 1; j + h < dimV; h++)
                            {
                                for (int k = 0; k < w; k++)
                                {
                                    if (mask[n + k + h * dimU] != val)
                                    {
                                        done = true;
                                        break;
                                    }
                                }

                                if (done) break;
                            }

                            // Material resolution
                            int palIdx = Math.Abs(val) - 1;
                            Guid? matId = null;
                            if (grid.Palette.TryGet(palIdx, out var palEntry))
                            {
                                matId = palEntry?.MaterialId;
                            }

                            // Vertices for the quad
                            x[u] = i;
                            x[v] = j;

                            int[] du = [0, 0, 0];
                            int[] dv = [0, 0, 0];
                            du[u] = w;
                            dv[v] = h;

                            Vector3D p0 = ToWorld(x[0] + mins[0], x[1] + mins[1], x[2] + mins[2]);
                            Vector3D p1 = ToWorld(x[0] + mins[0] + du[0], x[1] + mins[1] + du[1], x[2] + mins[2] + du[2]);
                            Vector3D p2 = ToWorld(x[0] + mins[0] + du[0] + dv[0], x[1] + mins[1] + du[1] + dv[1], x[2] + mins[2] + du[2] + dv[2]);
                            Vector3D p3 = ToWorld(x[0] + mins[0] + dv[0], x[1] + mins[1] + dv[1], x[2] + mins[2] + dv[2]);

                            Vector3D norm = new Vector3D(q[0], q[1], q[2]) * Math.Sign(val);

                            if (val > 0)
                            {
                                AddRawQuad(mesh, p0, p1, p2, p3, norm, sx, sy, sz, w, h, matId, options);
                            }
                            else
                            {
                                AddRawQuad(mesh, p0, p3, p2, p1, norm, sx, sy, sz, w, h, matId, options);
                            }

                            // Clear used cells in mask
                            for (int l = 0; l < h; l++)
                            {
                                for (int k = 0; k < w; k++)
                                {
                                    mask[n + k + l * dimU] = 0;
                                }
                            }

                            i += w;
                            n += w;
                        }
                        else
                        {
                            i++;
                            n++;
                        }
                    }
                }
            }
        }

        static Vector3D ToWorld(int gx, int gy, int gz) => new(gx, gy, gz);
    }

    private static bool IsExposed(VoxelGrid grid, int x, int y, int z)
    {
        if (!grid.TryGetVoxel(new VoxelPosition(x, y, z), out var neighbor))
        {
            return true;
        }

        return neighbor!.IsAir;
    }

    private static void AddQuad(
        MeshData mesh,
        Vector3D p0,
        Vector3D p1,
        Vector3D p2,
        Vector3D p3,
        Vector3D norm,
        double sx,
        double sy,
        double sz,
        Guid? materialId,
        VoxelToMeshOptions options)
    {
        AddRawQuad(mesh, p0, p1, p2, p3, norm, sx, sy, sz, 1, 1, materialId, options);
    }

    private static void AddRawQuad(
        MeshData mesh,
        Vector3D p0,
        Vector3D p1,
        Vector3D p2,
        Vector3D p3,
        Vector3D norm,
        double sx,
        double sy,
        double sz,
        double uMax,
        double vMax,
        Guid? materialId,
        VoxelToMeshOptions options)
    {
        int baseIdx = mesh.Vertices.Count;

        Vector3D v0 = new(p0.X * sx, p0.Y * sy, p0.Z * sz);
        Vector3D v1 = new(p1.X * sx, p1.Y * sy, p1.Z * sz);
        Vector3D v2 = new(p2.X * sx, p2.Y * sy, p2.Z * sz);
        Vector3D v3 = new(p3.X * sx, p3.Y * sy, p3.Z * sz);

        Vector3D? normal = options.GenerateNormals ? norm : null;

        Vector2D? uv0 = options.GenerateUvs ? new Vector2D(0, 0) : null;
        Vector2D? uv1 = options.GenerateUvs ? new Vector2D(uMax, 0) : null;
        Vector2D? uv2 = options.GenerateUvs ? new Vector2D(uMax, vMax) : null;
        Vector2D? uv3 = options.GenerateUvs ? new Vector2D(0, vMax) : null;

        mesh.Vertices.Add(new Vertex(v0, normal, uv0));
        mesh.Vertices.Add(new Vertex(v1, normal, uv1));
        mesh.Vertices.Add(new Vertex(v2, normal, uv2));
        mesh.Vertices.Add(new Vertex(v3, normal, uv3));

        mesh.Faces.Add(new Face([baseIdx, baseIdx + 1, baseIdx + 2, baseIdx + 3], materialId));
    }
}
