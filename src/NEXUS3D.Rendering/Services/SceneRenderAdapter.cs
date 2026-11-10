using System.Numerics;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Skeletons;
using NEXUS3D.Rendering.Contracts;
using NEXUS3D.Rendering.Domain;

namespace NEXUS3D.Rendering.Services;

/// <summary>
/// Core adapter converting USM (Scene3D) into GPU-optimized RenderScene.
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

        // 1. Adapt Materials
        foreach (var mat in scene.Materials.Materials)
        {
            var rMat = _materialAdapter.AdaptMaterial(mat);
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
            var rNode = AdaptNode(rootNode, Matrix4x4.Identity, settings, ref totalTriangles, ref totalVertices, ref totalVoxels);
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
            // Process Meshes
            foreach (var mesh in node.Object.Meshes)
            {
                var rMesh = _meshBuilder.Build(mesh, "default");
                rNode.Meshes.Add(rMesh);
                totalTriangles += rMesh.TriangleCount;
                totalVertices += rMesh.VertexCount;
            }

            // Process Voxel Grids
            foreach (var voxelGrid in node.Object.VoxelGrids)
            {
                var chunk = _voxelBuilder.Build(voxelGrid, enableCulling: true);
                rNode.VoxelChunks.Add(chunk);
                totalVoxels += chunk.VoxelCount;
                if (chunk.Mesh is not null)
                {
                    totalTriangles += chunk.Mesh.TriangleCount;
                    totalVertices += chunk.Mesh.VertexCount;
                }
            }
        }

        // Process Children
        foreach (var child in node.Children)
        {
            var childNode = AdaptNode(child, worldMat, settings, ref totalTriangles, ref totalVertices, ref totalVoxels);
            rNode.Children.Add(childNode);
        }

        _boundsCalculator.CalculateNodeBounds(rNode);
        return rNode;
    }

    private static List<RenderBone> BuildBoneHierarchy(SkeletonData skeleton)
    {
        var result = new List<RenderBone>();
        var boneMap = new Dictionary<Guid, RenderBone>();

        foreach (var b in skeleton.Bones)
        {
            var rBone = new RenderBone
            {
                Name = b.Name,
                Pivot = new Vector3((float)b.Pivot.X, (float)b.Pivot.Y, (float)b.Pivot.Z),
                Rotation = new Vector3((float)b.BindRotationDegrees.X, (float)b.BindRotationDegrees.Y, (float)b.BindRotationDegrees.Z),
                Transform = ToNumericsMatrix(b.LocalTransform.ToMatrix())
            };
            boneMap[b.Id] = rBone;
        }

        foreach (var b in skeleton.Bones)
        {
            var rBone = boneMap[b.Id];
            if (b.ParentBoneId.HasValue && boneMap.TryGetValue(b.ParentBoneId.Value, out var parentBone))
            {
                rBone.ParentName = parentBone.Name;
                parentBone.Children.Add(rBone);
            }
            else
            {
                result.Add(rBone);
            }
        }

        // Compute world pivots
        foreach (var root in result)
        {
            ComputeBoneWorldTransforms(root, Matrix4x4.Identity);
        }

        return result;
    }

    private static void ComputeBoneWorldTransforms(RenderBone bone, Matrix4x4 parentMatrix)
    {
        bone.WorldMatrix = bone.Transform * parentMatrix;
        foreach (var child in bone.Children)
        {
            ComputeBoneWorldTransforms(child, bone.WorldMatrix);
        }
    }

    private static Matrix4x4 ToNumericsMatrix(Matrix4x4D m)
    {
        return new Matrix4x4(
            (float)m.M11, (float)m.M12, (float)m.M13, (float)m.M14,
            (float)m.M21, (float)m.M22, (float)m.M23, (float)m.M24,
            (float)m.M31, (float)m.M32, (float)m.M33, (float)m.M34,
            (float)m.M41, (float)m.M42, (float)m.M43, (float)m.M44);
    }
}
