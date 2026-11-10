using Microsoft.Extensions.Logging;
using NEXUS3D.Conversion.Abstractions;
using NEXUS3D.Conversion.Abstractions.Interfaces;
using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Shared.Guards;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Conversion.Engine.Services;

/// <summary>
/// Converts continuous 3D polygon meshes into discrete volumetric <see cref="VoxelGrid"/> models
/// using Tomas Akenine-Möller Triangle-AABB Separating Axis Theorem (SAT) rasterization and optional interior flood filling.
/// </summary>
public sealed class MeshToVoxelConverter : IMeshToVoxelConverter
{
    private readonly IPolygonTriangulator _triangulator;
    private readonly ILogger<MeshToVoxelConverter> _logger;

    public MeshToVoxelConverter(
        IPolygonTriangulator triangulator,
        ILogger<MeshToVoxelConverter> logger)
    {
        _triangulator = Guard.NotNull(triangulator);
        _logger = Guard.NotNull(logger);
    }

    public OperationResult<VoxelGrid> ConvertMeshToVoxelGrid(
        MeshData mesh,
        Scene3D? parentScene = null,
        MeshToVoxelOptions? options = null)
    {
        Guard.NotNull(mesh);
        options ??= new MeshToVoxelOptions();

        string gridName = $"{mesh.Name}_Voxels";
        var grid = new VoxelGrid(gridName);

        if (mesh.Vertices.Count == 0 || mesh.Faces.Count == 0)
        {
            return OperationResult<VoxelGrid>.Success(grid);
        }

        BoundingBox bounds = mesh.CalculateBounds();
        if (bounds.IsEmpty)
        {
            return OperationResult<VoxelGrid>.Success(grid);
        }

        // Determine voxel cell size
        double voxelSize = options.VoxelSize > 0.001 ? options.VoxelSize : 1.0;

        if (options.ResolutionMode == VoxelResolutionMode.FixedGridDimension)
        {
            double maxSpan = Math.Max(bounds.Size.X, Math.Max(bounds.Size.Y, bounds.Size.Z));
            int maxDim = Math.Clamp(options.MaxGridDimension, 16, 512);
            voxelSize = Math.Max(0.01, maxSpan / maxDim);
        }

        grid.VoxelSize = new VoxelSize(voxelSize, voxelSize, voxelSize);

        Vector3D minPos = bounds.Min;
        Vector3D halfVoxel = new(voxelSize * 0.5, voxelSize * 0.5, voxelSize * 0.5);

        // Pre-populate palette with default block
        var defaultState = CreateBlockState(options.DefaultBlockNamespace);
        int defaultPaletteIndex = grid.Palette.GetOrAdd(defaultState);

        // Map material IDs to palette indices
        var materialToPalette = new Dictionary<Guid, int>();
        if (parentScene is not null)
        {
            foreach (var mat in parentScene.Materials.Materials)
            {
                string blockName = CleanBlockName(mat.Name, options.DefaultBlockNamespace);
                int palIdx = grid.Palette.GetOrAdd(CreateBlockState(blockName), mat.Id);
                materialToPalette[mat.Id] = palIdx;
            }
        }

        // Triangulate and rasterize each face
        int rasterizedVoxels = 0;
        var occupiedCoords = new HashSet<VoxelPosition>();

        for (int f = 0; f < mesh.Faces.Count; f++)
        {
            var face = mesh.Faces[f];
            var triFaces = _triangulator.TriangulateFace(face, mesh.Vertices);

            int targetPalette = defaultPaletteIndex;
            Guid? targetMatId = face.MaterialId;

            if (targetMatId.HasValue && materialToPalette.TryGetValue(targetMatId.Value, out int mappedIdx))
            {
                targetPalette = mappedIdx;
            }

            for (int t = 0; t < triFaces.Count; t++)
            {
                var tri = triFaces[t];
                Vector3D p0 = mesh.Vertices[tri.VertexIndices[0]].Position;
                Vector3D p1 = mesh.Vertices[tri.VertexIndices[1]].Position;
                Vector3D p2 = mesh.Vertices[tri.VertexIndices[2]].Position;

                // Triangle bounds in grid index space
                Vector3D tMin = Vector3D.Min(p0, Vector3D.Min(p1, p2));
                Vector3D tMax = Vector3D.Max(p0, Vector3D.Max(p1, p2));

                int minGx = (int)Math.Floor((tMin.X - minPos.X) / voxelSize);
                int minGy = (int)Math.Floor((tMin.Y - minPos.Y) / voxelSize);
                int minGz = (int)Math.Floor((tMin.Z - minPos.Z) / voxelSize);

                int maxGx = (int)Math.Floor((tMax.X - minPos.X) / voxelSize);
                int maxGy = (int)Math.Floor((tMax.Y - minPos.Y) / voxelSize);
                int maxGz = (int)Math.Floor((tMax.Z - minPos.Z) / voxelSize);

                for (int gx = minGx; gx <= maxGx; gx++)
                {
                    for (int gy = minGy; gy <= maxGy; gy++)
                    {
                        for (int gz = minGz; gz <= maxGz; gz++)
                        {
                            var voxelCoord = new VoxelPosition(gx, gy, gz);
                            if (occupiedCoords.Contains(voxelCoord))
                            {
                                continue;
                            }

                            // Voxel center in world space
                            Vector3D voxelCenter = minPos + new Vector3D(
                                (gx + 0.5) * voxelSize,
                                (gy + 0.5) * voxelSize,
                                (gz + 0.5) * voxelSize);

                            if (TestTriangleAabbIntersection(p0, p1, p2, voxelCenter, halfVoxel))
                            {
                                var voxel = new Voxel(
                                    position: voxelCoord,
                                    paletteIndex: targetPalette,
                                    materialId: targetMatId);

                                grid.SetVoxel(voxel);
                                occupiedCoords.Add(voxelCoord);
                                rasterizedVoxels++;
                            }
                        }
                    }
                }
            }
        }

        // Optional Solid Flood Filling
        if ((options.Mode == VoxelizationMode.SolidFill || options.FillInterior) && occupiedCoords.Count > 0)
        {
            SolidFillInterior(grid, occupiedCoords, defaultPaletteIndex);
        }

        grid.RecalculateBounds();

        _logger.LogInformation(
            "Voxelized mesh '{MeshName}' into VoxelGrid ({VoxelCount} voxels, size {VoxelSize:F3}m, bounds {Bounds}).",
            mesh.Name,
            grid.GetVoxelCount(),
            voxelSize,
            grid.Bounds);

        return OperationResult<VoxelGrid>.Success(grid);
    }

    private static void SolidFillInterior(
        VoxelGrid grid,
        HashSet<VoxelPosition> occupiedCoords,
        int defaultPaletteIndex)
    {
        grid.RecalculateBounds();
        var bounds = grid.Bounds;
        if (!bounds.IsValid || bounds.Width > 256 || bounds.Depth > 256)
        {
            return;
        }

        int minX = bounds.Min.X;
        int maxX = bounds.Max.X;
        int minY = bounds.Min.Y;
        int maxY = bounds.Max.Y;
        int minZ = bounds.Min.Z;
        int maxZ = bounds.Max.Z;

        // Ray casting / parity fill along Z axis for each (X, Y) column
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                int firstZ = int.MaxValue;
                int lastZ = int.MinValue;

                for (int z = minZ; z <= maxZ; z++)
                {
                    if (occupiedCoords.Contains(new VoxelPosition(x, y, z)))
                    {
                        if (z < firstZ) firstZ = z;
                        if (z > lastZ) lastZ = z;
                    }
                }

                // If bounded by at least 2 voxels on this column
                if (firstZ < lastZ)
                {
                    for (int z = firstZ + 1; z < lastZ; z++)
                    {
                        var pos = new VoxelPosition(x, y, z);
                        if (!occupiedCoords.Contains(pos))
                        {
                            var fillVoxel = new Voxel(pos, defaultPaletteIndex);
                            grid.SetVoxel(fillVoxel);
                            occupiedCoords.Add(pos);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Separating Axis Theorem (SAT) test for Triangle vs Axis-Aligned Bounding Box (AABB).
    /// </summary>
    private static bool TestTriangleAabbIntersection(
        Vector3D v0,
        Vector3D v1,
        Vector3D v2,
        Vector3D boxCenter,
        Vector3D boxHalfExtent)
    {
        // 1. Translate triangle so box center is at origin
        Vector3D p0 = v0 - boxCenter;
        Vector3D p1 = v1 - boxCenter;
        Vector3D p2 = v2 - boxCenter;

        // 2. Test 3 AABB coordinate axes (test AABB of triangle vs box extent)
        Vector3D min = Vector3D.Min(p0, Vector3D.Min(p1, p2));
        Vector3D max = Vector3D.Max(p0, Vector3D.Max(p1, p2));

        if (min.X > boxHalfExtent.X || max.X < -boxHalfExtent.X) return false;
        if (min.Y > boxHalfExtent.Y || max.Y < -boxHalfExtent.Y) return false;
        if (min.Z > boxHalfExtent.Z || max.Z < -boxHalfExtent.Z) return false;

        // 3. Test triangle plane normal
        Vector3D f0 = p1 - p0;
        Vector3D f1 = p2 - p1;
        Vector3D f2 = p0 - p2;

        Vector3D normal = Vector3D.Cross(f0, f1);
        double d = Vector3D.Dot(normal, p0);
        double r = boxHalfExtent.X * Math.Abs(normal.X) +
                   boxHalfExtent.Y * Math.Abs(normal.Y) +
                   boxHalfExtent.Z * Math.Abs(normal.Z);

        if (Math.Abs(d) > r) return false;

        // 4. Test 9 cross products of AABB axis e_i and triangle edge f_j
        // a00 = e0 x f0 = (0, -f0.Z, f0.Y)
        if (!AxisTest(0, -f0.Z, f0.Y, p0.Y, p0.Z, p2.Y, p2.Z, boxHalfExtent.Y, boxHalfExtent.Z)) return false;
        // a01 = e0 x f1 = (0, -f1.Z, f1.Y)
        if (!AxisTest(0, -f1.Z, f1.Y, p0.Y, p0.Z, p1.Y, p1.Z, boxHalfExtent.Y, boxHalfExtent.Z)) return false;
        // a02 = e0 x f2 = (0, -f2.Z, f2.Y)
        if (!AxisTest(0, -f2.Z, f2.Y, p0.Y, p0.Z, p1.Y, p1.Z, boxHalfExtent.Y, boxHalfExtent.Z)) return false;

        // a10 = e1 x f0 = (f0.Z, 0, -f0.X)
        if (!AxisTest(f0.Z, 0, -f0.X, p0.X, p0.Z, p2.X, p2.Z, boxHalfExtent.X, boxHalfExtent.Z)) return false;
        // a11 = e1 x f1 = (f1.Z, 0, -f1.X)
        if (!AxisTest(f1.Z, 0, -f1.X, p0.X, p0.Z, p1.X, p1.Z, boxHalfExtent.X, boxHalfExtent.Z)) return false;
        // a12 = e1 x f2 = (f2.Z, 0, -f2.X)
        if (!AxisTest(f2.Z, 0, -f2.X, p0.X, p0.Z, p1.X, p1.Z, boxHalfExtent.X, boxHalfExtent.Z)) return false;

        // a20 = e2 x f0 = (-f0.Y, f0.X, 0)
        if (!AxisTest(-f0.Y, f0.X, 0, p0.X, p0.Y, p2.X, p2.Y, boxHalfExtent.X, boxHalfExtent.Y)) return false;
        // a21 = e2 x f1 = (-f1.Y, f1.X, 0)
        if (!AxisTest(-f1.Y, f1.X, 0, p0.X, p0.Y, p1.X, p1.Y, boxHalfExtent.X, boxHalfExtent.Y)) return false;
        // a22 = e2 x f2 = (-f2.Y, f2.X, 0)
        if (!AxisTest(-f2.Y, f2.X, 0, p0.X, p0.Y, p1.X, p1.Y, boxHalfExtent.X, boxHalfExtent.Y)) return false;

        return true;
    }

    private static bool AxisTest(
        double a1, double a2, double a3,
        double v0A, double v0B,
        double v1A, double v1B,
        double boxHalfA, double boxHalfB)
    {
        double p0 = a1 != 0 ? a1 * v0A + a3 * v0B : a2 * v0A + a3 * v0B;
        double p1 = a1 != 0 ? a1 * v1A + a3 * v1B : a2 * v1A + a3 * v1B;

        double min = Math.Min(p0, p1);
        double max = Math.Max(p0, p1);

        double rad = boxHalfA * Math.Abs(a1 != 0 ? a1 : a2) + boxHalfB * Math.Abs(a3);
        return !(min > rad || max < -rad);
    }

    private static BlockState CreateBlockState(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier)) return new BlockState("minecraft", "stone");
        int colon = identifier.IndexOf(':');
        if (colon >= 0)
        {
            string ns = identifier[..colon];
            string name = identifier[(colon + 1)..];
            return new BlockState(ns, string.IsNullOrWhiteSpace(name) ? "stone" : name);
        }
        return new BlockState("minecraft", identifier);
    }

    private static string CleanBlockName(string name, string fallback)
    {
        if (string.IsNullOrWhiteSpace(name)) return fallback;
        string cleaned = name.ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
        if (!cleaned.Contains(':'))
        {
            cleaned = $"minecraft:{cleaned}";
        }
        return cleaned;
    }
}
