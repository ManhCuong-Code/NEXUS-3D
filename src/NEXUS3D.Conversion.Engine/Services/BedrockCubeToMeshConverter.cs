using Microsoft.Extensions.Logging;
using NEXUS3D.Conversion.Abstractions.Interfaces;
using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Shared.Guards;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Conversion.Engine.Services;

/// <summary>
/// Bakes hierarchical Bedrock bone-cuboid structures and poly-meshes into unified static <see cref="MeshData"/> models.
/// </summary>
public sealed class BedrockCubeToMeshConverter : IBedrockCubeToMeshConverter
{
    private readonly ILogger<BedrockCubeToMeshConverter> _logger;

    public BedrockCubeToMeshConverter(ILogger<BedrockCubeToMeshConverter> logger)
    {
        _logger = Guard.NotNull(logger);
    }

    public OperationResult<MeshData> ConvertBedrockCubesToMesh(
        Scene3D bedrockScene,
        BedrockCubeToMeshOptions? options = null)
    {
        Guard.NotNull(bedrockScene);
        options ??= new BedrockCubeToMeshOptions();

        string meshName = $"{bedrockScene.Name}_StaticMesh";
        var bakedMesh = new MeshData(meshName, MeshTopology.Polygons);

        int totalSourceMeshes = 0;
        int totalBakedFaces = 0;

        // Traverse hierarchy calculating world transformations
        TraverseAndBakeNode(
            node: bedrockScene.Graph.Root,
            parentTransform: Matrix4x4D.Identity,
            targetMesh: bakedMesh,
            options: options,
            ref totalSourceMeshes,
            ref totalBakedFaces);

        _logger.LogInformation(
            "Baked Bedrock scene '{SceneName}' into static mesh: {SourceMeshes} source meshes -> {Vertices} vertices, {Faces} faces.",
            bedrockScene.Name,
            totalSourceMeshes,
            bakedMesh.Vertices.Count,
            bakedMesh.Faces.Count);

        return OperationResult<MeshData>.Success(bakedMesh);
    }

    private static void TraverseAndBakeNode(
        SceneNode node,
        Matrix4x4D parentTransform,
        MeshData targetMesh,
        BedrockCubeToMeshOptions options,
        ref int totalSourceMeshes,
        ref int totalBakedFaces)
    {
        Matrix4x4D currentTransform = options.BakeTransforms
            ? node.GetWorldMatrix()
            : Matrix4x4D.Identity;

        if (node.Object is { } obj && obj.Meshes.Count > 0)
        {
            foreach (var mesh in obj.Meshes)
            {
                totalSourceMeshes++;
                int baseVertexIndex = targetMesh.Vertices.Count;

                // Transform and copy vertices
                for (int i = 0; i < mesh.Vertices.Count; i++)
                {
                    var v = mesh.Vertices[i];
                    Vector3D worldPos = currentTransform.TransformPoint(v.Position);
                    Vector3D? worldNorm = v.Normal.HasValue
                        ? currentTransform.TransformDirection(v.Normal.Value).Normalize()
                        : null;

                    targetMesh.Vertices.Add(new Vertex(worldPos, worldNorm, v.TexCoord, v.Color));
                }

                // Copy faces with re-indexed vertex offsets
                for (int f = 0; f < mesh.Faces.Count; f++)
                {
                    var face = mesh.Faces[f];
                    var reindexed = new int[face.VertexIndices.Count];
                    for (int k = 0; k < face.VertexIndices.Count; k++)
                    {
                        reindexed[k] = baseVertexIndex + face.VertexIndices[k];
                    }

                    var newFace = new Face(reindexed, face.MaterialId, face.SmoothingGroup)
                    {
                        GroupNames = face.GroupNames.Count > 0 ? face.GroupNames.ToArray() : [node.Name]
                    };

                    if (face.Corners is not null)
                    {
                        var newCorners = new FaceCorner[face.Corners.Count];
                        for (int k = 0; k < face.Corners.Count; k++)
                        {
                            var c = face.Corners[k];
                            newCorners[k] = c with
                            {
                                PositionIndex = baseVertexIndex + c.PositionIndex
                            };
                        }
                        newFace.Corners = newCorners;
                    }

                    targetMesh.Faces.Add(newFace);
                    totalBakedFaces++;
                }
            }
        }

        // Recursively traverse child nodes
        foreach (var child in node.Children)
        {
            TraverseAndBakeNode(child, currentTransform, targetMesh, options, ref totalSourceMeshes, ref totalBakedFaces);
        }
    }
}
