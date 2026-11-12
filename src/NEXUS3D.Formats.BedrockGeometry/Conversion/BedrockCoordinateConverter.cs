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
    /// All vertices are generated in bone-local coordinates <c>(corners - bone.Pivot)</c> so that when
    /// the renderer applies the bone's world transform, vertices map precisely to model space.
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

        // 8 corners in Bedrock pixel space (model space)
        Vector3D[] corners =
        [
            new(min.X, min.Y, min.Z), // 0: East-Bottom-North (left-bottom-front in Bedrock)
            new(max.X, min.Y, min.Z), // 1: West-Bottom-North (right-bottom-front in Bedrock)
            new(max.X, max.Y, min.Z), // 2: West-Top-North
            new(min.X, max.Y, min.Z), // 3: East-Top-North
            new(min.X, min.Y, max.Z), // 4: East-Bottom-South
            new(max.X, min.Y, max.Z), // 5: West-Bottom-South
            new(max.X, max.Y, max.Z), // 6: West-Top-South
            new(min.X, max.Y, max.Z)  // 7: East-Top-South
        ];

        // Convert corners to USM space
        for (int i = 0; i < corners.Length; i++)
        {
            corners[i] = BedrockPositionToUsm(corners[i], scaleToBlocks: false);
        }

        // Apply per-cube rotation around per-cube pivot if present (in USM space)
        QuaternionD? cubeRotQ = null;
        if (cube.Rotation.HasValue && cube.Rotation.Value != Vector3D.Zero)
        {
            Vector3D usmCubePivot = BedrockPositionToUsm(cube.Pivot ?? owningBone.Pivot, scaleToBlocks: false);
            QuaternionD rotQ = BedrockRotationToUsmQuaternion(cube.Rotation.Value);
            cubeRotQ = rotQ;

            for (int i = 0; i < corners.Length; i++)
            {
                corners[i] = usmCubePivot + rotQ.RotateVector(corners[i] - usmCubePivot);
            }
        }

        // Convert corners to bone-local coordinates (corners - bone.Pivot)
        Vector3D usmBonePivot = BedrockPositionToUsm(owningBone.Pivot, scaleToBlocks: false);
        for (int i = 0; i < corners.Length; i++)
        {
            corners[i] -= usmBonePivot;
        }

        int texW = description.TextureWidth > 0 ? description.TextureWidth : 64;
        int texH = description.TextureHeight > 0 ? description.TextureHeight : 64;

        // 6 faces: north (-Z), south (+Z), east (+X in USM), west (-X in USM), up (+Y), down (-Y)
        string[] faceNames = ["north", "south", "east", "west", "up", "down"];

        foreach (string faceName in faceNames)
        {
            if (cube.Uv?.Mode == BedrockUvMode.PerFaceUv &&
                !cube.Uv.Faces.ContainsKey(faceName))
            {
                // Omitted face in per-face UV is intentionally unrendered in Bedrock
                continue;
            }

            var (cornerIndices, normal, uvs) = GetFaceGeometryAndUvs(
                faceName,
                cube,
                mirror,
                texW,
                texH,
                cubeRotQ);

            int baseVertIdx = mesh.Vertices.Count;

            for (int c = 0; c < 4; c++)
            {
                mesh.Vertices.Add(new Vertex(
                    corners[cornerIndices[c]],
                    normal,
                    uvs[c]));
            }

            mesh.Faces.Add(new Face([baseVertIdx, baseVertIdx + 1, baseVertIdx + 2, baseVertIdx + 3], materialId));
        }

        if (materialId.HasValue && mesh.Faces.Count > 0)
        {
            mesh.Submeshes.Add(new MeshSubmesh("cube", 0, mesh.Faces.Count, materialId.Value));
        }

        return mesh;
    }

    /// <summary>
    /// Computes USM space bounding box for a <see cref="BedrockCube"/>.
    /// If <paramref name="bonePivot"/> is provided, returns bone-local bounds.
    /// </summary>
    public static BoundingBox BedrockCubeToUsmBounds(BedrockCube cube, double inflate = 0.0, Vector3D? bonePivot = null)
    {
        ArgumentNullException.ThrowIfNull(cube);
        Vector3D min = new(
            cube.Origin.X - inflate,
            cube.Origin.Y - inflate,
            cube.Origin.Z - inflate);
        Vector3D max = new(
            cube.Origin.X + cube.Size.X + inflate,
            cube.Origin.Y + cube.Size.Y + inflate,
            cube.Origin.Z + cube.Size.Z + inflate);

        if (bonePivot.HasValue)
        {
            min -= bonePivot.Value;
            max -= bonePivot.Value;
        }

        Vector3D usmMin = BedrockPositionToUsm(min);
        Vector3D usmMax = BedrockPositionToUsm(max);

        return new BoundingBox(
            new Vector3D(Math.Min(usmMin.X, usmMax.X), Math.Min(usmMin.Y, usmMax.Y), Math.Min(usmMin.Z, usmMax.Z)),
            new Vector3D(Math.Max(usmMin.X, usmMax.X), Math.Max(usmMin.Y, usmMax.Y), Math.Max(usmMin.Z, usmMax.Z)));
    }

    private static (int[] CornerIndices, Vector3D Normal, Vector2D[] Uvs) GetFaceGeometryAndUvs(
        string faceName,
        BedrockCube cube,
        bool mirror,
        int texW,
        int texH,
        QuaternionD? cubeRotQ)
    {
        double u1 = 0, v1 = 0, u2 = 16, v2 = 16;
        int uvRotation = 0;

        if (cube.Uv?.Mode == BedrockUvMode.PerFaceUv &&
            cube.Uv.Faces.TryGetValue(faceName, out BedrockFaceUv? faceUv))
        {
            u1 = faceUv.Uv.X;
            v1 = faceUv.Uv.Y;
            Vector2D defaultSize = faceName switch
            {
                "up" or "down" => new Vector2D(cube.Size.X, cube.Size.Z),
                "east" or "west" => new Vector2D(cube.Size.Z, cube.Size.Y),
                _ => new Vector2D(cube.Size.X, cube.Size.Y)
            };
            Vector2D size = faceUv.UvSize ?? defaultSize;
            u2 = u1 + size.X;
            v2 = v1 + size.Y;
            uvRotation = faceUv.UvRotation ?? 0;
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

        // Normalized UV coordinates in USM space:
        // In Bedrock pixel space: v1 is top (y=0 is top of image), v2 is bottom.
        // In USM normalized space: V=1 is top of image, V=0 is bottom.
        // When HelixSceneRenderer converts to WPF TextureCoordinates (where Y=0 is top):
        // wpf.Y = 1.0 - usm.Y = 1.0 - (1.0 - pixelY / H) = pixelY / H (exact top of image).
        double uNorm1 = u1 / (double)texW;
        double uNorm2 = u2 / (double)texW;
        double vNormTop = 1.0 - (v1 / (double)texH);
        double vNormBottom = 1.0 - (v2 / (double)texH);

        var uvBL = new Vector2D(uNorm1, vNormBottom);
        var uvBR = new Vector2D(uNorm2, vNormBottom);
        var uvTR = new Vector2D(uNorm2, vNormTop);
        var uvTL = new Vector2D(uNorm1, vNormTop);

        // Apply UV rotation if specified (clockwise in 90 degree increments)
        if (uvRotation != 0)
        {
            int steps = (((uvRotation % 360) + 360) % 360) / 90;
            for (int s = 0; s < steps; s++)
            {
                (uvBL, uvBR, uvTR, uvTL) = (uvBR, uvTR, uvTL, uvBL);
            }
        }

        int[] cornerIndices;
        Vector3D baseNormal;
        Vector2D[] uvs;

        switch (faceName)
        {
            case "north":
                // North face (-Z): looking from outside: BL is c1, TL is c2, TR is c3, BR is c0.
                // Outward normal: (c2 - c1) x (c3 - c1) = (0, 0, -1).
                cornerIndices = [1, 2, 3, 0];
                baseNormal = new Vector3D(0, 0, -1);
                uvs = [uvBL, uvTL, uvTR, uvBR];
                break;

            case "south":
                // South face (+Z): looking from outside: BL is c4, TL is c7, TR is c6, BR is c5.
                // Outward normal: (c7 - c4) x (c6 - c4) = (0, 0, 1).
                cornerIndices = [4, 7, 6, 5];
                baseNormal = new Vector3D(0, 0, 1);
                uvs = [uvBL, uvTL, uvTR, uvBR];
                break;

            case "east":
                // East face (+X in USM): looking from outside: BL is c4, BR is c0, TR is c3, TL is c7.
                // Outward normal: (c0 - c4) x (c3 - c4) = (1, 0, 0).
                cornerIndices = [4, 0, 3, 7];
                baseNormal = new Vector3D(1, 0, 0);
                uvs = [uvBL, uvBR, uvTR, uvTL];
                break;

            case "west":
                // West face (-X in USM): looking from outside: BL is c1, BR is c5, TR is c6, TL is c2.
                // Outward normal: (c5 - c1) x (c6 - c1) = (-1, 0, 0).
                cornerIndices = [1, 5, 6, 2];
                baseNormal = new Vector3D(-1, 0, 0);
                uvs = [uvBL, uvBR, uvTR, uvTL];
                break;

            case "up":
                // Up face (+Y): looking from top: North is -Z, South is +Z, West is -X, East is +X.
                // In Minecraft Box UV: Top of texture connects to North (-Z), Bottom connects to South (+Z).
                // BL (South-West) is c6, BR (South-East) is c7, TR (North-East) is c3, TL (North-West) is c2.
                // Outward normal: (c7 - c6) x (c3 - c6) = (0, 1, 0).
                cornerIndices = [6, 7, 3, 2];
                baseNormal = new Vector3D(0, 1, 0);
                uvs = [uvBL, uvBR, uvTR, uvTL];
                break;

            case "down":
                // Down face (-Y): looking from bottom: South is +Z (top of texture), North is -Z (bottom).
                // BL (North-West) is c1, BR (North-East) is c0, TR (South-East) is c4, TL (South-West) is c5.
                // Outward normal: (c0 - c1) x (c4 - c1) = (0, -1, 0).
                cornerIndices = [1, 0, 4, 5];
                baseNormal = new Vector3D(0, -1, 0);
                uvs = [uvBL, uvBR, uvTR, uvTL];
                break;

            default:
                cornerIndices = [1, 2, 3, 0];
                baseNormal = new Vector3D(0, 0, -1);
                uvs = [uvBL, uvTL, uvTR, uvBR];
                break;
        }

        Vector3D normal = cubeRotQ.HasValue
            ? cubeRotQ.Value.RotateVector(baseNormal).Normalize()
            : baseNormal;

        return (cornerIndices, normal, uvs);
    }
}
