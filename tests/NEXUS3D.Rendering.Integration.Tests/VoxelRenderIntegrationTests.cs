using System.IO;
using NEXUS3D.Formats.VoxelJson.Services;
using NEXUS3D.Rendering.Domain;
using NEXUS3D.Rendering.Services;
using Xunit;

namespace NEXUS3D.Rendering.Integration.Tests;

public sealed class VoxelRenderIntegrationTests
{
    private static string GetFixturePath(string fileName)
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null)
        {
            string candidate = Path.Combine(current, "tests", "Fixtures", "Json", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
            current = Directory.GetParent(current)?.FullName;
        }
        throw new FileNotFoundException($"Could not locate fixture: {fileName}");
    }

    [Fact]
    public async Task ParseVoxelJsonToRenderScene_AdaptsVoxelsIntoRenderChunks()
    {
        string path = GetFixturePath("voxel_nexus_v1.json");
        var parser = new VoxelJsonFormatParser();
        var readResult = await parser.ReadFileAsync(path);

        Assert.True(readResult.IsSuccess);
        Assert.NotNull(readResult.Scene);

        var boundsCalc = new BoundingBoxCalculator();
        var texLoader = new TextureLoader();
        var matAdapter = new MaterialRenderAdapter(texLoader);
        var sceneAdapter = new SceneRenderAdapter(matAdapter, boundsCalc);

        var renderScene = sceneAdapter.Adapt(readResult.Scene, RenderSettings.Default);

        Assert.NotNull(renderScene);
        Assert.True(renderScene.TotalTriangles > 0 || renderScene.TotalVoxels > 0);
        Assert.NotEmpty(renderScene.RootNodes);
    }
}
