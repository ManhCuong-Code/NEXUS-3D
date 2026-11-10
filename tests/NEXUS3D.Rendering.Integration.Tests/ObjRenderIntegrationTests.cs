using System.IO;
using System.Numerics;
using NEXUS3D.Formats.Obj.Services;
using NEXUS3D.Rendering.Domain;
using NEXUS3D.Rendering.Services;
using Xunit;

namespace NEXUS3D.Rendering.Integration.Tests;

public sealed class ObjRenderIntegrationTests
{
    private static string GetFixturePath(string relativePath)
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null)
        {
            string candidate = Path.Combine(current, "tests", "Fixtures", "Obj", relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
            current = Directory.GetParent(current)?.FullName;
        }
        throw new FileNotFoundException($"Could not locate fixture: {relativePath}");
    }

    [Fact]
    public async Task ParseObjToRenderScene_ProducesValidRenderableMeshAndMaterials()
    {
        string objPath = GetFixturePath("TexturedCube.obj");
        var parser = new ObjFormatParser();
        var readResult = await parser.ReadFileAsync(objPath);

        Assert.True(readResult.IsSuccess);
        Assert.NotNull(readResult.Scene);

        var boundsCalc = new BoundingBoxCalculator();
        var texLoader = new TextureLoader();
        var matAdapter = new MaterialRenderAdapter(texLoader);
        var sceneAdapter = new SceneRenderAdapter(matAdapter, boundsCalc);

        var renderScene = sceneAdapter.Adapt(readResult.Scene, RenderSettings.Default);

        Assert.NotNull(renderScene);
        Assert.True(renderScene.TotalTriangles > 0);
        Assert.True(renderScene.TotalVertices > 0);
        Assert.NotEmpty(renderScene.RootNodes);

        var rootNode = renderScene.RootNodes[0];
        Assert.NotEmpty(rootNode.Meshes);
        var mesh = rootNode.Meshes[0];

        Assert.NotEmpty(mesh.Positions);
        Assert.NotEmpty(mesh.Normals);
        Assert.NotEmpty(mesh.Indices);
        Assert.NotEmpty(mesh.WireframeIndices);
        Assert.True(mesh.BoundsMax.X > mesh.BoundsMin.X);
    }
}

