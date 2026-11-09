using System.Text;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Formats.Obj.Common;
using NEXUS3D.Formats.Obj.Geometry;
using NEXUS3D.Formats.Obj.Parsing;

namespace NEXUS3D.Obj.Tests;

public sealed class ObjParserTests
{
    private static async Task<ObjDocument> ParseTextAsync(string objText, ObjParseOptions? options = null)
    {
        var parser = new ObjParser();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(objText));
        return await parser.ParseDocumentAsync(stream, options ?? ObjParseOptions.Default);
    }

    [Fact]
    public async Task Parse_Vertices3DAnd4D_PreservesExactDoubleCoordinatesAndWeight()
    {
        const string obj = """
            # Vertex test
            v 1.25 -2.5 3.75
            v 4.0 8.0 12.0 2.0
            f 1 2 1
            """;

        var doc = await ParseTextAsync(obj, new ObjParseOptions { StrictValidation = false });

        Assert.Equal(2, doc.Positions.Count);
        Assert.Equal(new ObjVertex(1.25, -2.5, 3.75, 1.0, 2), doc.Positions[0]);
        Assert.Equal(new ObjVertex(4.0, 8.0, 12.0, 2.0, 3), doc.Positions[1]);
        Assert.Equal(new Vector3D(2.0, 4.0, 6.0), doc.Positions[1].ToVector3D());
        Assert.Equal(new Vector3D(4.0, 8.0, 12.0), doc.Positions[1].ToRawVector3D());
    }

    [Fact]
    public async Task Parse_TexCoords1D2D3D_NormalsAndParameterVertices_Succeeds()
    {
        const string obj = """
            v 0 0 0
            v 1 0 0
            v 0 1 0
            vt 0.5
            vt 0.25 0.75
            vt 0.1 0.2 0.9
            vn 0 0 1
            vp 0.3 0.6 1.0
            f 1/1/1 2/2/1 3/3/1
            """;

        var doc = await ParseTextAsync(obj);

        Assert.Equal(3, doc.TextureCoordinates.Count);
        Assert.Equal(1, doc.TextureCoordinates[0].Dimension);
        Assert.Equal(0.5, doc.TextureCoordinates[0].U);

        Assert.Equal(2, doc.TextureCoordinates[1].Dimension);
        Assert.Equal(new Vector2D(0.25, 0.75), doc.TextureCoordinates[1].ToVector2D());

        Assert.Equal(3, doc.TextureCoordinates[2].Dimension);
        Assert.Equal(new Vector3D(0.1, 0.2, 0.9), doc.TextureCoordinates[2].ToVector3D());

        Assert.Single(doc.Normals);
        Assert.Equal(new Vector3D(0, 0, 1), doc.Normals[0].ToVector3D());

        Assert.Single(doc.ParameterVertices);
        Assert.Equal(3, doc.ParameterVertices[0].Dimension);
        Assert.Equal(0.3, doc.ParameterVertices[0].U);
        Assert.Equal(0.6, doc.ParameterVertices[0].V);
        Assert.Equal(1.0, doc.ParameterVertices[0].W);
    }

    [Fact]
    public async Task Parse_AllFourFaceFormats_PositiveAndNegativeIndices_ResolvesCorrectly()
    {
        const string obj = """
            v 0 0 0
            v 1 0 0
            v 1 1 0
            v 0 1 0
            vt 0 0
            vt 1 0
            vt 1 1
            vt 0 1
            vn 0 0 1
            # 1) v only (positive)
            f 1 2 3
            # 2) v/vt (negative relative indices)
            f -4/-4 -3/-3 -2/-2
            # 3) v//vn
            f 1//1 3//1 4//1
            # 4) v/vt/vn (mixed positive and negative)
            f 1/1/1 2/2/-1 -1/-1/1
            """;

        var doc = await ParseTextAsync(obj);

        Assert.Equal(4, doc.Faces.Count);

        // Face 0: v only
        Assert.Equal(new ObjFaceVertex(0, null, null), doc.Faces[0].Vertices[0]);
        Assert.Equal(new ObjFaceVertex(1, null, null), doc.Faces[0].Vertices[1]);
        Assert.Equal(new ObjFaceVertex(2, null, null), doc.Faces[0].Vertices[2]);

        // Face 1: v/vt negative (-4 -> index 0, -3 -> index 1, -2 -> index 2)
        Assert.Equal(new ObjFaceVertex(0, 0, null), doc.Faces[1].Vertices[0]);
        Assert.Equal(new ObjFaceVertex(1, 1, null), doc.Faces[1].Vertices[1]);
        Assert.Equal(new ObjFaceVertex(2, 2, null), doc.Faces[1].Vertices[2]);

        // Face 2: v//vn
        Assert.Equal(new ObjFaceVertex(0, null, 0), doc.Faces[2].Vertices[0]);
        Assert.Equal(new ObjFaceVertex(2, null, 0), doc.Faces[2].Vertices[1]);
        Assert.Equal(new ObjFaceVertex(3, null, 0), doc.Faces[2].Vertices[2]);

        // Face 3: v/vt/vn mixed
        Assert.Equal(new ObjFaceVertex(0, 0, 0), doc.Faces[3].Vertices[0]);
        Assert.Equal(new ObjFaceVertex(1, 1, 0), doc.Faces[3].Vertices[1]);
        Assert.Equal(new ObjFaceVertex(3, 3, 0), doc.Faces[3].Vertices[2]);
    }

    [Fact]
    public async Task Parse_TriangleQuadAndConcaveNGon_PreservesPolygonsAndEarClipsAccurately()
    {
        // L-shaped 6-vertex concave polygon in XY plane (Z=0):
        // (0,0) -> (2,0) -> (2,1) -> (1,1) -> (1,2) -> (0,2)
        // Area = 2*1 + 1*1 = 3.0
        const string obj = """
            v 0 0 0
            v 2 0 0
            v 2 1 0
            v 1 1 0
            v 1 2 0
            v 0 2 0
            f 1 2 3
            f 1 2 3 4
            f 1 2 3 4 5 6
            """;

        var doc = await ParseTextAsync(obj);

        Assert.Equal(3, doc.Faces.Count);
        Assert.True(doc.Faces[0].IsTriangle);
        Assert.True(doc.Faces[1].IsQuad);
        Assert.True(doc.Faces[2].IsNGon);
        Assert.Equal(6, doc.Faces[2].Vertices.Count);

        // Triangulate the concave 6-gon via Ear-Clipping
        IReadOnlyList<ObjFace> triangles = doc.Faces[2].Triangulate(doc.Positions);
        Assert.Equal(4, triangles.Count);

        double totalArea = 0.0;
        foreach (ObjFace tri in triangles)
        {
            Assert.True(tri.IsTriangle);
            Vector3D a = doc.Positions[tri.Vertices[0].PositionIndex].ToRawVector3D();
            Vector3D b = doc.Positions[tri.Vertices[1].PositionIndex].ToRawVector3D();
            Vector3D c = doc.Positions[tri.Vertices[2].PositionIndex].ToRawVector3D();
            Vector3D cross = Vector3D.Cross(b - a, c - a);
            Assert.True(cross.Z > 0.0); // Preserves CCW winding in +Z
            totalArea += 0.5 * cross.Length();
        }

        Assert.Equal(3.0, totalArea, precision: 6);
    }

    [Fact]
    public async Task Parse_MultipleObjectsGroupsSmoothingGroupsPolylinesPointsAndContinuations_Succeeds()
    {
        const string obj = """
            # Header comment 1
            mtllib materials.mtl
            o Chassis
            g Hull Exterior
            s 1
            usemtl Steel
            v 0 0 0
            v 1 0 0
            v 1 1 0
            v 0 1 0
            f 1 2 \
              3 4
            o Turret
            g TopGroup
            s off
            usemtl Armor
            f 1 2 3
            l 1 2 3 4
            p 1 4
            custom_vendor_tag 42
            """;

        var doc = await ParseTextAsync(obj, new ObjParseOptions { StrictValidation = false });

        Assert.Single(doc.MaterialLibraries);
        Assert.Equal("materials.mtl", doc.MaterialLibraries[0]);

        Assert.Equal(2, doc.Objects.Count);
        Assert.True(doc.Objects.ContainsKey("Chassis"));
        Assert.True(doc.Objects.ContainsKey("Turret"));

        Assert.Equal(3, doc.Groups.Count);
        Assert.True(doc.Groups.ContainsKey("Hull"));
        Assert.True(doc.Groups.ContainsKey("Exterior"));
        Assert.True(doc.Groups.ContainsKey("TopGroup"));

        Assert.Equal(2, doc.Faces.Count);
        Assert.Equal("Chassis", doc.Faces[0].ObjectName);
        Assert.Equal(["Hull", "Exterior"], doc.Faces[0].GroupNames);
        Assert.Equal(1, doc.Faces[0].SmoothingGroup);
        Assert.Equal("Steel", doc.Faces[0].MaterialName);
        Assert.Equal(4, doc.Faces[0].Vertices.Count); // Line continuation merged 1 2 3 4

        Assert.Equal("Turret", doc.Faces[1].ObjectName);
        Assert.Equal(["TopGroup"], doc.Faces[1].GroupNames);
        Assert.Equal(0, doc.Faces[1].SmoothingGroup);
        Assert.Equal("Armor", doc.Faces[1].MaterialName);

        Assert.Single(doc.Polylines);
        Assert.Equal(4, doc.Polylines[0].Vertices.Count);

        Assert.Single(doc.Points);
        Assert.Equal(2, doc.Points[0].PositionIndices.Count);

        Assert.Single(doc.UnrecognizedStatements);
        Assert.Equal("custom_vendor_tag", doc.UnrecognizedStatements[0].Keyword);
        Assert.Contains(doc.Warnings, w => w.Code == ObjErrorCodes.UnsupportedStatement);
    }
}
