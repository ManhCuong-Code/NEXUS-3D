using System.IO;
using System.Numerics;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Skeletons;
using NEXUS3D.Rendering.Contracts;
using NEXUS3D.Rendering.Domain;

namespace NEXUS3D.Rendering.Services;

/// <summary>
/// Core adapter converting USM (Scene3D) into GPU-optimized RenderScene,
/// managing multi-material submeshes, block material registry, textures, and hierarchy.
/// </summary>
public sealed class SceneRenderAdapter : IRenderSceneAdapter
{
    private readonly IMaterialRenderAdapter _materialAdapter;
    private readonly IBoundingBoxCalculator _boundsCalculator;
    private readonly MeshRenderBuilder _meshBuilder = new();
    private readonly VoxelRenderBuilder _voxelBuilder = new();

    public SceneRenderAdapter(
        IMaterialRenderAdapter materialAdapter,
        IBoundingBoxCalculator boundsCalculator)
    {
        _materialAdapter = materialAdapter;
        _boundsCalculator = boundsCalculator;
    }

    public RenderScene Adapt(Scene3D scene, RenderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(scene);
        settings ??= RenderSettings.Default;

        var renderScene = new RenderScene
        {
            Name = scene.Name
        };

        // Determine base directory for texture resolution
        string? baseDirectory = null;
        if (!string.IsNullOrWhiteSpace(scene.Metadata.Source?.OriginalFileName))
        {
            try
            {
                string fn = scene.Metadata.Source.OriginalFileName;
                baseDirectory = Path.GetDirectoryName(fn);
                if (string.IsNullOrEmpty(baseDirectory) && File.Exists(fn))
                {
                    baseDirectory = Path.GetDirectoryName(Path.GetFullPath(fn));
                }
            }
            catch
            {
                // Ignore invalid file paths
            }
        }

        // 1. Adapt Materials
        foreach (var mat in scene.Materials.Materials)
        {
            var rMat = _materialAdapter.AdaptMaterial(mat, baseDirectory);
            renderScene.Materials[rMat.Id] = rMat;
            renderScene.Materials[rMat.Name] = rMat;
        }

        if (!renderScene.Materials.ContainsKey("default"))
        {
            var defMat = _materialAdapter.GetDefaultMaterial();
            renderScene.Materials["default"] = defMat;
        }

        // 2. Adapt Skeletons
        foreach (var skeleton in scene.Skeletons)
        {
            var rootBones = BuildBoneHierarchy(skeleton);
            renderScene.Skeletons.AddRange(rootBones);
        }

        // 3. Adapt Scene Graph
        int totalTriangles = 0;
        int totalVertices = 0;
        int totalVoxels = 0;

        foreach (var rootNode in scene.Graph.Root.Children)
        {
            var rNode = AdaptNode(rootNode, Matrix4x4.Identity, settings, renderScene, ref totalTriangles, ref totalVertices, ref totalVoxels);
            renderScene.RootNodes.Add(rNode);
        }

        renderScene.TotalTriangles = totalTriangles;
        renderScene.TotalVertices = totalVertices;
        renderScene.TotalVoxels = totalVoxels;

        // 4. Calculate Scene Bounding Box
        _boundsCalculator.CalculateSceneBounds(renderScene);

        return renderScene;
    }

    private RenderNode AdaptNode(
        SceneNode node,
        Matrix4x4 parentWorld,
        RenderSettings settings,
        RenderScene scene,
        ref int totalTriangles,
        ref int totalVertices,
        ref int totalVoxels)
    {
        var localMat = ToNumericsMatrix(node.LocalTransform.ToMatrix());
        var worldMat = localMat * parentWorld;

        var rNode = new RenderNode
        {
            Id = node.Id.ToString(),
            Name = node.Name,
            NodeType = RenderNodeType.Mesh,
            LocalTransform = localMat,
            WorldTransform = worldMat
        };

        if (node.Object is not null)
        {
            // Process Meshes (supporting multi-material submesh partitioning)
            foreach (var mesh in node.Object.Meshes)
            {
                var rMeshes = _meshBuilder.BuildMulti(mesh, "default");
                foreach (var rMesh in rMeshes)
                {
                    rNode.Meshes.Add(rMesh);
                    totalTriangles += rMesh.TriangleCount;
                    totalVertices += rMesh.VertexCount;
                }
            }

            // Process Voxel Grids (supporting multi-block materials)
            foreach (var voxelGrid in node.Object.VoxelGrids)
            {
                var chunk = _voxelBuilder.Build(voxelGrid, enableCulling: true);
                rNode.VoxelChunks.Add(chunk);
                totalVoxels += chunk.VoxelCount;

                // Register block materials in renderScene.Materials
                foreach (var subMesh in chunk.SubMeshes)
                {
                    if (!string.IsNullOrEmpty(subMesh.MaterialId) && !scene.Materials.ContainsKey(subMesh.MaterialId))
                    {
                        string blockId = subMesh.MaterialId.StartsWith("block:")
                            ? subMesh.MaterialId["block:".Length..]
                            : subMesh.MaterialId;

                        var blockInfo = BlockMaterialRegistry.GetBlockInfo(blockId);
                        var blockMat = new RenderMaterial(
                            Id: subMesh.MaterialId,
                            Name: blockInfo.DisplayName,
                            BaseColor: blockInfo.Color,
                            SpecularColor: Vector4.One * 0.15f,
                            Shininess: 15f,
                            Opacity: blockInfo.Opacity,
                            IsDoubleSided: true);

                        scene.Materials[subMesh.MaterialId] = blockMat;
                    }

                    totalTriangles += subMesh.TriangleCount;
                    totalVertices += subMesh.VertexCount;
                }

                if (chunk.SubMeshes.Count == 0 && chunk.Mesh is not null)
                {
                    totalTriangles += chunk.Mesh.TriangleCount;
                    totalVertices += chunk.Mesh.VertexCount;
                }
            }
        }

        // Process Children
        foreach (var child in node.Children)
        {
            var childNode = AdaptNode(child, worldMat, settings, scene, ref totalTriangles, ref totalVertices, ref totalVoxels);
            rNode.Children.Add(childNode);
        }

        _boundsCalculator.CalculateNodeBounds(rNode);
        return rNode;
    }

    private static List<RenderBone> BuildBoneHierarchy(SkeletonData skeleton)
    {
        var result = new List<RenderBone>();
        var boneMap = new Dictionary<Guid, RenderBone>();

        foreach (var bone in skeleton.Bones)
        {
            var rBone = new RenderBone
            {
                Name = bone.Name,
                Pivot = new Vector3((float)bone.Pivot.X, (float)bone.Pivot.Y, (float)bone.Pivot.Z),
                Rotation = new Vector3((float)bone.BindRotationDegrees.X, (float)bone.BindRotationDegrees.Y, (float)bone.BindRotationDegrees.Z),
                Transform = ToNumericsMatrix(bone.LocalTransform.ToMatrix())
            };
            boneMap[bone.Id] = rBone;
        }

        foreach (var bone in skeleton.Bones)
        {
            var current = boneMap[bone.Id];
            if (bone.ParentBoneId.HasValue && boneMap.TryGetValue(bone.ParentBoneId.Value, out var parent))
            {
                current.ParentName = parent.Name;
                parent.Children.Add(current);
            }
            else
            {
                result.Add(current);
            }
        }

        void ComputeWorldMatrices(RenderBone b, Matrix4x4 parentMatrix)
        {
            b.WorldMatrix = b.Transform * parentMatrix;
            foreach (var c in b.Children)
            {
                ComputeWorldMatrices(c, b.WorldMatrix);
            }
        }

        foreach (var root in result)
        {
            ComputeWorldMatrices(root, Matrix4x4.Identity);
        }

        return result;
    }

    private static Matrix4x4 ToNumericsMatrix(Matrix4x4D m) =>
        new(
            (float)m.M11, (float)m.M12, (float)m.M13, (float)m.M14,
            (float)m.M21, (float)m.M22, (float)m.M23, (float)m.M24,
            (float)m.M31, (float)m.M32, (float)m.M33, (float)m.M34,
            (float)m.M41, (float)m.M42, (float)m.M43, (float)m.M44);
}
