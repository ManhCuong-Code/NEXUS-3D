using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NEXUS3D.App.Bootstrap;
using NEXUS3D.Application.Scene;
using NEXUS3D.Domain.Coordinates;
using NEXUS3D.Domain.Factories;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Voxels;

namespace NEXUS3D.Integration.Tests;

public sealed class UsmSceneIntegrationTests
{
    [Fact]
    public async Task SceneService_EndToEndLifecycle_CreateConvertMergeSnapshotAndFilePersist_ShouldSucceed()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"NEXUS3D_USM_IntTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var services = new ServiceCollection();
            var options = new DependencyOptions
            {
                CustomAppDataRoot = tempRoot,
                MinimumLogLevel = LogLevel.Warning
            };

            DependencyConfig.ConfigureInfrastructure(services, options);
            DependencyConfig.ConfigureFormatsAndEngine(services);
            DependencyConfig.ConfigureApplicationServices(services);

            await ExecuteWithProviderAsync(services.BuildServiceProvider(), tempRoot);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                try { Directory.Delete(tempRoot, recursive: true); } catch { /* ignore cleanup lock */ }
            }
        }
    }

    private static async Task ExecuteWithProviderAsync(ServiceProvider provider, string tempRoot)
    {
        await using (provider)
        {
            var sceneService = provider.GetRequiredService<ISceneService>();
            var meshFactory = provider.GetRequiredService<MeshFactory>();
            var nodeFactory = provider.GetRequiredService<SceneNodeFactory>();
            var voxelFactory = provider.GetRequiredService<VoxelGridFactory>();

            // 1. Create Primary Scene
            var createRes = sceneService.CreateScene(new CreateSceneRequest(
                "PrimaryCastleScene",
                CoordinateSystem.DefaultUsm,
                Author: "IntegrationTester",
                Description: "End-to-end USM test"));

            Assert.True(createRes.IsSuccess);
            var primaryScene = createRes.Scene!;

            var boxMesh = meshFactory.CreateBox("KeepTower", new Vector3D(2, 4, 2));
            var towerNode = nodeFactory.CreateMeshNode(
                "KeepTowerNode",
                boxMesh,
                new Transform3D(new Vector3D(1, 0, -2), QuaternionD.Identity, Vector3D.One));
            primaryScene.Graph.AddNode(primaryScene.Graph.Root, towerNode);

            // 2. Snapshot before coordinate conversion
            var snapshotRes = sceneService.CreateSnapshot(primaryScene, "PreBedrockConversion");
            Assert.True(snapshotRes.IsSuccess);

            // 3. Convert coordinate system from Right-Handed Y-Up to Left-Handed Y-Up (BedrockDefault)
            var convertRes = sceneService.ConvertCoordinateSystem(primaryScene, CoordinateSystem.BedrockDefault);
            Assert.True(convertRes.IsSuccess);
            var bedrockScene = convertRes.Scene!;
            Assert.Equal(CoordinateHandedness.LeftHanded, bedrockScene.Coordinates.CurrentSystem.Handedness);
            Assert.True(bedrockScene.Coordinates.RequiresWindingOrderFlip);

            // 4. Restore Snapshot
            var restoreRes = sceneService.RestoreSnapshot(snapshotRes.Value!);
            Assert.True(restoreRes.IsSuccess);
            Assert.Equal(CoordinateHandedness.RightHanded, restoreRes.Scene!.Coordinates.CurrentSystem.Handedness);

            // 5. Create Secondary Voxel Scene & Merge into Primary Scene
            var secondaryRes = sceneService.CreateScene(new CreateSceneRequest("SecondaryTerrainScene"));
            var secondaryScene = secondaryRes.Scene!;
            var voxelGrid = voxelFactory.CreateEmpty("GroundGrid");
            int grassIdx = voxelGrid.Palette.GetOrAdd(new BlockState("minecraft", "grass_block"));
            voxelGrid.SetVoxel(new Voxel(new VoxelPosition(0, 0, 0), grassIdx));
            secondaryScene.Graph.AddNode(secondaryScene.Graph.Root, nodeFactory.CreateVoxelNode("GroundNode", voxelGrid));

            var mergeRes = sceneService.MergeScenes(primaryScene, secondaryScene, "ImportedTerrain");
            Assert.True(mergeRes.IsSuccess);
            var mergedScene = mergeRes.Scene!;
            Assert.Equal(1, mergedScene.CalculateStatistics().MeshCount);
            Assert.Equal(1, mergedScene.CalculateStatistics().VoxelGridCount);

            // 6. Save & Load USM JSON File
            string usmFilePath = Path.Combine(tempRoot, "merged_scene.usm.json");
            var saveRes = await sceneService.SaveSceneToFileAsync(mergedScene, usmFilePath);
            Assert.True(saveRes.IsSuccess);
            Assert.True(File.Exists(usmFilePath));

            var loadRes = await sceneService.LoadSceneFromFileAsync(usmFilePath);
            Assert.True(loadRes.IsSuccess);
            Assert.NotNull(loadRes.Scene);
            Assert.Equal(mergedScene.Id, loadRes.Scene!.Id);
            Assert.Equal(mergedScene.CalculateStatistics().NodeCount, loadRes.Scene.CalculateStatistics().NodeCount);
        }
    }
}
