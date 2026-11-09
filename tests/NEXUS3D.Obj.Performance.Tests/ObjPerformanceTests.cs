using System.Diagnostics;
using System.Globalization;
using System.Text;
using NEXUS3D.Formats.Obj.Conversion;
using NEXUS3D.Formats.Obj.Exporting;
using NEXUS3D.Formats.Obj.Parsing;

namespace NEXUS3D.Obj.Performance.Tests;

public sealed class ObjPerformanceTests
{
    private static byte[] GenerateGridMeshObjBytes(int rows, int cols, int objectCount = 1, int groupCount = 1, int materialCount = 1)
    {
        var sb = new StringBuilder((rows * cols) * 80);
        sb.AppendLine("# Generated Benchmark Mesh");

        int totalVerts = (rows + 1) * (cols + 1);
        for (int r = 0; r <= rows; r++)
        {
            double y = r * 0.1;
            for (int c = 0; c <= cols; c++)
            {
                double x = c * 0.1;
                double z = Math.Sin(x) * Math.Cos(y) * 0.05;
                sb.Append("v ")
                  .Append(x.ToString("0.0000", CultureInfo.InvariantCulture)).Append(' ')
                  .Append(y.ToString("0.0000", CultureInfo.InvariantCulture)).Append(' ')
                  .AppendLine(z.ToString("0.0000", CultureInfo.InvariantCulture));
            }
        }

        for (int r = 0; r <= rows; r++)
        {
            double v = (double)r / rows;
            for (int c = 0; c <= cols; c++)
            {
                double u = (double)c / cols;
                sb.Append("vt ")
                  .Append(u.ToString("0.0000", CultureInfo.InvariantCulture)).Append(' ')
                  .AppendLine(v.ToString("0.0000", CultureInfo.InvariantCulture));
            }
        }

        sb.AppendLine("vn 0 0 1");

        int totalCells = rows * cols;
        int cellsPerObject = Math.Max(1, totalCells / Math.Max(1, objectCount));
        int cellsPerGroup = Math.Max(1, totalCells / Math.Max(1, groupCount));
        int cellsPerMat = Math.Max(1, totalCells / Math.Max(1, materialCount));

        int cellIndex = 0;
        int stride = cols + 1;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (cellIndex % cellsPerObject == 0)
                {
                    sb.Append("o Object_").AppendLine((cellIndex / cellsPerObject).ToString(CultureInfo.InvariantCulture));
                }

                if (cellIndex % cellsPerGroup == 0)
                {
                    sb.Append("g Group_").AppendLine((cellIndex / cellsPerGroup).ToString(CultureInfo.InvariantCulture));
                    sb.Append("s ").AppendLine(((cellIndex / cellsPerGroup) % 4 + 1).ToString(CultureInfo.InvariantCulture));
                }

                if (cellIndex % cellsPerMat == 0)
                {
                    sb.Append("usemtl Mat_").AppendLine((cellIndex / cellsPerMat).ToString(CultureInfo.InvariantCulture));
                }

                int i0 = r * stride + c + 1;
                int i1 = i0 + 1;
                int i2 = (r + 1) * stride + c + 2;
                int i3 = (r + 1) * stride + c + 1;

                sb.Append("f ")
                  .Append(i0).Append('/').Append(i0).Append("/1 ")
                  .Append(i1).Append('/').Append(i1).Append("/1 ")
                  .Append(i2).Append('/').Append(i2).Append("/1 ")
                  .Append(i3).Append('/').Append(i3).AppendLine("/1");

                cellIndex++;
            }
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    [Theory]
    [InlineData(9, 9)]      // Small: 100 vertices, 81 quad faces
    [InlineData(70, 70)]    // Medium: 5,041 vertices, 4,900 quad faces
    [InlineData(223, 223)]  // Large: 50,176 vertices, 49,729 quad faces (~100k triangles)
    public async Task ParseConvertAndExport_SmallMediumAndLargeMeshes_CompletesWithinPerformanceBudget(int rows, int cols)
    {
        byte[] objBytes = GenerateGridMeshObjBytes(rows, cols);
        var parser = new ObjParser();
        var toUsm = new ObjToSceneConverter();
        var toObj = new SceneToObjConverter();
        var exporter = new ObjExporter();

        long memBefore = GC.GetTotalMemory(forceFullCollection: false);
        var swParse = Stopwatch.StartNew();
        using var inputStream = new MemoryStream(objBytes);
        var doc = await parser.ParseDocumentAsync(inputStream);
        swParse.Stop();

        var swConvert = Stopwatch.StartNew();
        var (scene, report) = toUsm.Convert(doc);
        var (exportedDoc, _, _) = toObj.Convert(scene);
        swConvert.Stop();

        var swExport = Stopwatch.StartNew();
        using var outputStream = new MemoryStream(objBytes.Length);
        var exportResult = await exporter.ExportAsync(exportedDoc, outputStream);
        swExport.Stop();
        long memAfter = GC.GetTotalMemory(forceFullCollection: false);

        int expectedVertices = (rows + 1) * (cols + 1);
        int expectedFaces = rows * cols;

        Assert.Equal(expectedVertices, doc.Positions.Count);
        Assert.Equal(expectedFaces, doc.Faces.Count);
        Assert.True(exportResult.Succeeded);
        Assert.Equal(expectedVertices, exportResult.VertexCount);
        Assert.Equal(expectedFaces, exportResult.FaceCount);

        // Even 50k vertices + 50k faces should parse, convert, and export well under 10 seconds
        Assert.True(swParse.ElapsedMilliseconds < 8000, $"Parse took {swParse.ElapsedMilliseconds} ms");
        Assert.True(swConvert.ElapsedMilliseconds < 8000, $"USM convert took {swConvert.ElapsedMilliseconds} ms");
        Assert.True(swExport.ElapsedMilliseconds < 8000, $"Export took {swExport.ElapsedMilliseconds} ms");
        Assert.True(memAfter - memBefore < 512L * 1024L * 1024L);
    }

    [Fact]
    public async Task ParseConvertAndExport_ManyObjectsGroupsAndMaterials_PreservesPartitioningRapidly()
    {
        byte[] objBytes = GenerateGridMeshObjBytes(
            rows: 40,
            cols: 40,
            objectCount: 40,
            groupCount: 80,
            materialCount: 20);

        var parser = new ObjParser();
        using var stream = new MemoryStream(objBytes);
        var doc = await parser.ParseDocumentAsync(stream);

        Assert.Equal(40, doc.Objects.Count);
        Assert.Equal(80, doc.Groups.Count);

        var (scene, report) = new ObjToSceneConverter().Convert(doc);
        Assert.Equal(40, report.ObjectCount);
        Assert.Equal(20, scene.Materials.Count);
    }
}
