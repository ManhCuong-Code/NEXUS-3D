using System.Text;
using NEXUS3D.Formats.Obj.Common;
using NEXUS3D.Formats.Obj.Exporting;
using NEXUS3D.Formats.Obj.Geometry;
using NEXUS3D.Formats.Obj.Parsing;

namespace NEXUS3D.Obj.Tests;

public sealed class ObjExporterTests
{
    [Fact]
    public async Task Export_OneBasedIndicesAndOptionalTriangulation_ProducesValidWavefrontObj()
    {
        var doc = new ObjDocument();
        doc.MaterialLibraries.Add("quad.mtl");
        doc.Positions.Add(new ObjVertex(0.0, 0.0, 0.0));
        doc.Positions.Add(new ObjVertex(1.12345678, 0.0, 0.0));
        doc.Positions.Add(new ObjVertex(1.12345678, 1.0, 0.0));
        doc.Positions.Add(new ObjVertex(0.0, 1.0, 0.0));

        doc.TextureCoordinates.Add(new ObjTextureCoordinate(0.0, 0.0));
        doc.TextureCoordinates.Add(new ObjTextureCoordinate(1.0, 0.0));
        doc.TextureCoordinates.Add(new ObjTextureCoordinate(1.0, 1.0));
        doc.TextureCoordinates.Add(new ObjTextureCoordinate(0.0, 1.0));

        doc.Normals.Add(new ObjNormal(0.0, 0.0, 1.0));

        doc.Faces.Add(new ObjFace
        {
            ObjectName = "Panel",
            GroupNames = ["MainGroup"],
            SmoothingGroup = 1,
            MaterialName = "PanelMat",
            Vertices =
            [
                new ObjFaceVertex(0, 0, 0),
                new ObjFaceVertex(1, 1, 0),
                new ObjFaceVertex(2, 2, 0),
                new ObjFaceVertex(3, 3, 0)
            ]
        });

        var exporter = new ObjExporter();

        // 1. Export preserving quad with FloatPrecision = 4
        using var quadStream = new MemoryStream();
        ObjExportResult quadResult = await exporter.ExportAsync(
            doc,
            quadStream,
            new ObjExportOptions { FloatPrecision = 4, TriangulatePolygons = false });

        Assert.True(quadResult.Succeeded);
        Assert.Equal(4, quadResult.VertexCount);
        Assert.Equal(1, quadResult.FaceCount);

        string quadText = Encoding.UTF8.GetString(quadStream.ToArray());
        Assert.Contains("mtllib quad.mtl", quadText);
        Assert.Contains("v 1.1235 0 0", quadText);
        Assert.Contains("o Panel", quadText);
        Assert.Contains("g MainGroup", quadText);
        Assert.Contains("s 1", quadText);
        Assert.Contains("usemtl PanelMat", quadText);
        Assert.Contains("f 1/1/1 2/2/1 3/3/1 4/4/1", quadText);

        // 2. Export with TriangulatePolygons = true
        using var triStream = new MemoryStream();
        ObjExportResult triResult = await exporter.ExportAsync(
            doc,
            triStream,
            new ObjExportOptions { FloatPrecision = 6, TriangulatePolygons = true });

        Assert.True(triResult.Succeeded);
        Assert.Equal(2, triResult.FaceCount);

        triStream.Position = 0;
        ObjDocument reParsed = await new ObjParser().ParseDocumentAsync(triStream);
        Assert.Equal(2, reParsed.Faces.Count);
        Assert.All(reParsed.Faces, f => Assert.True(f.IsTriangle));
    }
}

