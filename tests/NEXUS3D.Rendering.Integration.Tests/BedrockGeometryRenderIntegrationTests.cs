using System.IO;
using NEXUS3D.Formats.BedrockGeometry.Services;
using NEXUS3D.Rendering.Domain;
using NEXUS3D.Rendering.Services;
using Xunit;

namespace NEXUS3D.Rendering.Integration.Tests;

public sealed class BedrockGeometryRenderIntegrationTests
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
    public async Task ParseBedrockGeometryToRenderScene_AdaptsBonesAndGeometry()
    {
        string path = GetFixturePath("bedrock_1_16_0_guardian.geo.json");
        var parser = new BedrockGeometryFormatParser();
        var readResult = await parser.ReadFileAsync(path);

        Assert.True(readResult.IsSuccess);
        Assert.NotNull(readResult.Scene);

        var boundsCalc = new BoundingBoxCalculator();
        var texLoader = new TextureLoader();
        var matAdapter = new MaterialRenderAdapter(texLoader);
        var sceneAdapter = new SceneRenderAdapter(matAdapter, boundsCalc);

        var renderScene = sceneAdapter.Adapt(readResult.Scene, RenderSettings.Default);

        Assert.NotNull(renderScene);
        Assert.NotEmpty(renderScene.RootNodes);
        Assert.True(renderScene.Skeletons.Count > 0 || renderScene.RootNodes.Any(n => n.Bones.Count > 0 || n.Meshes.Count > 0));
    }
}

