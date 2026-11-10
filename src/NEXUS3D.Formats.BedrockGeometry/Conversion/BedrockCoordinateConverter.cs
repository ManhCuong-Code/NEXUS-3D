using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Formats.BedrockGeometry.Models;

namespace NEXUS3D.Formats.BedrockGeometry.Conversion;

/// <summary>
/// Converts coordinates, pivots, rotations, UVs, and cuboid primitives between Minecraft Bedrock Geometry space
/// (Left-Handed, Y-Up, 16 pixels = 1 block, UV top-left origin) and the Universal Scene Model (Right-Handed, Y-Up).
/// </summary>
public static class BedrockCoordinateConverter
{
    public const double PixelsPerBlock = 16.0;

    /// <summary>
    /// Converts a Bedrock pixel coordinate <c>(X, Y, Z)</c> to USM space.
    /// Bedrock uses Left-Handed Y-Up (<c>-X</c> right, <c>+Y</c> up, <c>-Z</c> north/forward);
    /// flipping X converts between Left-Handed and Right-Handed while preserving Y-Up.
    /// </summary>
    public static Vector3D BedrockPositionToUsm(Vector3D bedrockPos, bool scaleToBlocks = false)
    {
        double scale = scaleToBlocks ? (1.0 / PixelsPerBlock) : 1.0;
        return new Vector3D(-bedrockPos.X * scale, bedrockPos.Y * scale, bedrockPos.Z * scale);
    }

    /// <summary>
    /// Inverse of <see cref="BedrockPositionToUsm"/>.
    /// </summary>
    public static Vector3D UsmPositionToBedrock(Vector3D usmPos, bool scaledToBlocks = false)
    {
        double scale = scaledToBlocks ? PixelsPerBlock : 1.0;
        double x = -usmPos.X * scale;
        return new Vector3D(Math.Abs(x) < 1e-12 ? 0.0 : x, usmPos.Y * scale, usmPos.Z * scale);
    }

    /// <summary>
    /// Converts Bedrock Euler rotation degrees <c>(pitchX, yawY, rollZ)</c> to USM Euler degrees.
    /// </summary>
    public static Vector3D BedrockRotationToUsmEuler(Vector3D bedrockDegrees) =>
        new(-bedrockDegrees.X, -bedrockDegrees.Y, bedrockDegrees.Z);

    /// <summary>
    /// Inverse of <see cref="BedrockRotationToUsmEuler"/>.
    /// </summary>
    public static Vector3D UsmEulerToBedrockRotation(Vector3D usmDegrees)
    {
        double rx = -usmDegrees.X;
        double ry = -usmDegrees.Y;
        return new Vector3D(
            Math.Abs(rx) < 1e-12 ? 0.0 : rx,
            Math.Abs(ry) < 1e-12 ? 0.0 : ry,
            usmDegrees.Z);
    }

    public static QuaternionD BedrockRotationToUsmQuaternion(Vector3D bedrockDegrees)
    {
        Vector3D usmEuler = BedrockRotationToUsmEuler(bedrockDegrees);
        return QuaternionD.FromEulerAnglesDegrees(usmEuler.X, usmEuler.Y, usmEuler.Z);
    }

    /// <summary>
    /// Converts Bedrock pixel UV <c>(u, v)</c> (origin top-left) to normalized USM UV <c>(u, v)</c> in <c>[0..1]</c>.
    /// </summary>
    public static Vector2D BedrockPixelUvToNormalized(Vector2D pixelUv, int textureWidth, int textureHeight)
    {
        double w = textureWidth > 0 ? textureWidth : 64.0;
        double h = textureHeight > 0 ? textureHeight : 64.0;
        return new Vector2D(pixelUv.X / w, 1.0 - (pixelUv.Y / h));
    }

    /// <summary>
    /// Converts normalized USM UV <c>(u, v)</c> back to Bedrock pixel UV <c>(u, v)</c> (origin top-left).
    /// </summary>
    public static Vector2D NormalizedUvToBedrockPixel(Vector2D normalizedUv, int textureWidth, int textureHeight)
    {
        double w = textureWidth > 0 ? textureWidth : 64.0;
        double h = textureHeight > 0 ? textureHeight : 64.0;
        return new Vector2D(normalizedUv.X * w, (1.0 - normalizedUv.Y) * h);
    }

    /// <summary>
    /// Builds a 6-face (24-vertex, 6-quad) USM <see cref="MeshData"/> for a <see cref="BedrockCube"/>
    /// with accurate normals and normalized UVs (supporting both Box UV and Per-Face UV).
    /// </summary>
    public static MeshData BuildCubeMesh(
        BedrockCube cube,
        BedrockBone owningBone,
        BedrockGeometryDescription description,
        string meshName,
        Guid? materialId = null)
    {
        ArgumentNullException.ThrowIfNull(cube);
        ArgumentNullException.ThrowIfNull(owningBone);
        ArgumentNullException.ThrowIfNull(description);

        var mesh = new MeshData(meshName, MeshTopology.Polygons);

        double inflate = cube.Inflate ?? owningBone.Inflate ?? 0.0;
        bool mirror = cube.Mirror ?? owningBone.Mirror ?? false;

        Vector3D min = new(
            cube.Origin.X - inflate,
            cube.Origin.Y - inflate,
            cube.Origin.Z - inflate);

        Vector3D max = new(
            cube.Origin.X + cube.Size.X + inflate,
            cube.Origin.Y + cube.Size.Y + inflate,
            cube.Origin.Z + cube.Size.Z + inflate);

        // 8 corners in Bedrock pixel space
        Vector3D[] corners =
        [
            new(min.X, min.Y, min.Z), // 0: left-bottom-front (north-east in Bedrock)
            new(max.X, min.Y, min.Z), // 1: right-bottom-front
            new(max.X, max.Y, min.Z), // 2: right-top-front
            new(min.X, max.Y, min.Z), // 3: left-top-front
            new(min.X, min.Y, max.Z), // 4: left-bottom-back
            new(max.X, min.Y, max.Z), // 5: right-bottom-back
            new(max.X, max.Y, max.Z), // 6: right-top-back
            new(min.X, max.Y, max.Z)  // 7: left-top-back
        ];

        // Apply per-cube rotation around per-cube pivot if present
        if (cube.Rotation.HasValue && cube.Rotation.Value != Vector3D.Zero)
        {
            Vector3D pivot = cube.Pivot ?? owningBone.Pivot;
            QuaternionD rotQ = QuaternionD.FromEulerAnglesDegrees(
                cube.Rotation.Value.X,
                cube.Rotation.Value.Y,
                cube.Rotation.Value.Z);

            for (int i = 0; i < corners.Length; i++)
            {
                corners[i] = pivot + rotQ.RotateVector(corners[i] - pivot);
            }
        }

        // Convert corners to USM space
        for (int i = 0; i < corners.Length; i++)
        {
            corners[i] = BedrockPositionToUsm(corners[i], scaleToBlocks: false);
        }

        // 6 faces: north (-Z), south (+Z), east (-X in Bedrock / +X in USM), west (+X in Bedrock / -X in USM), up (+Y), down (-Y)
        (string FaceName, int[] CornerIndices, Vector3D Normal)[] faceDefs =
        [
            ("north", [1, 0, 3, 2], new Vector3D(0, 0, -1)),
            ("south", [4, 5, 6, 7], new Vector3D(0, 0, 1)),
            ("east",  [0, 4, 7, 3], new Vector3D(1, 0, 0)),
            ("west",  [5, 1, 2, 6], new Vector3D(-1, 0, 0)),
            ("up",    [3, 7, 6, 2], new Vector3D(0, 1, 0)),
            ("down",  [0, 1, 5, 4], new Vector3D(0, -1, 0))
        ];

        foreach (var (faceName, cornerIdx, normal) in faceDefs)
        {
            if (cube.Uv?.Mode == BedrockUvMode.PerFaceUv &&
                !cube.Uv.Faces.ContainsKey(faceName))
            {
                // Omitted face in per-face UV is intentionally unrendered in Bedrock
                continue;
            }

            Vector2D[] faceUvs = ComputeFaceNormalizedUvs(cube, faceName, mirror, description.TextureWidth, description.TextureHeight);
            int baseVertIdx = mesh.Vertices.Count;

            for (int c = 0; c < 4; c++)
            {
                mesh.Vertices.Add(new Vertex(
                    corners[cornerIdx[c]],
                    normal,
                    faceUvs[c]));
            }

            mesh.Faces.Add(new Face([baseVertIdx, baseVertIdx + 1, baseVertIdx + 2, baseVertIdx + 3], materialId));
        }

        return mesh;
    }

    private static Vector2D[] ComputeFaceNormalizedUvs(
        BedrockCube cube,
        string faceName,
        bool mirror,
        int texW,
        int texH)
    {
        double u1 = 0, v1 = 0, u2 = 16, v2 = 16;

        if (cube.Uv?.Mode == BedrockUvMode.PerFaceUv &&
            cube.Uv.Faces.TryGetValue(faceName, out BedrockFaceUv? faceUv))
        {
            u1 = faceUv.Uv.X;
            v1 = faceUv.Uv.Y;
            Vector2D size = faceUv.UvSize ?? new Vector2D(cube.Size.X, cube.Size.Y);
            u2 = u1 + size.X;
            v2 = v1 + size.Y;
        }
        else if (cube.Uv?.Mode == BedrockUvMode.BoxUv)
        {
            double bx = cube.Uv.BoxUvOffset.X;
            double by = cube.Uv.BoxUvOffset.Y;
            double dx = Math.Floor(cube.Size.X);
            double dy = Math.Floor(cube.Size.Y);
            double dz = Math.Floor(cube.Size.Z);

            (u1, v1, u2, v2) = faceName switch
            {
                "up" => (bx + dz, by, bx + dz + dx, by + dz),
                "down" => (bx + dz + dx, by, bx + dz + dx + dx, by + dz),
                "east" => (bx, by + dz, bx + dz, by + dz + dy),
                "north" => (bx + dz, by + dz, bx + dz + dx, by + dz + dy),
                "west" => (bx + dz + dx, by + dz, bx + dz + dx + dz, by + dz + dy),
                "south" => (bx + dz + dx + dz, by + dz, bx + dz + dx + dz + dx, by + dz + dy),
                _ => (bx, by, bx + dx, by + dy)
            };

            if (mirror)
            {
                (u1, u2) = (u2, u1);
            }
        }

        return
        [
            BedrockPixelUvToNormalized(new Vector2D(u1, v2), texW, texH),
            BedrockPixelUvToNormalized(new Vector2D(u2, v2), texW, texH),
            BedrockPixelUvToNormalized(new Vector2D(u2, v1), texW, texH),
            BedrockPixelUvToNormalized(new Vector2D(u1, v1), texW, texH)
        ];
    }
}
