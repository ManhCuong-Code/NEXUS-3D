using System.Collections.Concurrent;
using System.IO;
using System.Numerics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using NEXUS3D.Rendering.Contracts;
using NEXUS3D.Rendering.Domain;

namespace NEXUS3D.App.Rendering;

/// <summary>
/// Hardware-accelerated WPF / HelixToolkit scene converter translating RenderScene snapshots into ModelVisual3D and LinesVisual3D,
/// supporting multi-material meshes, multi-block voxel rendering, and texture mapping.
/// </summary>
public sealed class HelixSceneRenderer : IDisposable
{
    private readonly ConcurrentDictionary<string, ImageBrush> _textureBrushCache = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, Model3DGroup> NodeVisualGroups { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<GeometryModel3D, RenderNode> ModelToNodeMap { get; } = new();
    private bool _isDisposed;

    public void ClearCache()
    {
        _textureBrushCache.Clear();
        NodeVisualGroups.Clear();
        ModelToNodeMap.Clear();
    }

    public bool ContainsModel(GeometryModel3D model) => ModelToNodeMap.ContainsKey(model);
    public RenderNode? GetNodeForModel(GeometryModel3D model) => ModelToNodeMap.GetValueOrDefault(model);

    public void Dispose()
    {
        if (!_isDisposed)
        {
            ClearCache();
            _isDisposed = true;
        }
    }

    public ModelVisual3D BuildSceneVisual(RenderScene scene, RenderSettings settings, out LinesVisual3D linesVisual, out BoundingBoxWireFrameVisual3D boundsVisual)
    {
        NodeVisualGroups.Clear();
        ModelToNodeMap.Clear();
        var rootModelGroup = new Model3DGroup();
        var rootVisual = new ModelVisual3D { Content = rootModelGroup };

        linesVisual = new LinesVisual3D
        {
            Color = Colors.Cyan,
            Thickness = 1.0
        };

        boundsVisual = new BoundingBoxWireFrameVisual3D
        {
            Color = Colors.Yellow,
            Thickness = 1.5
        };

        if (scene is null)
        {
            return rootVisual;
        }

        // 1. Build Mesh and Voxel Models
        foreach (var node in scene.RootNodes)
        {
            BuildNodeVisual(node, scene, settings, rootModelGroup, linesVisual);
        }

        // 2. Build Wireframe if enabled globally
        if (settings.Shading is ShadingMode.Wireframe or ShadingMode.SolidWireframe || settings.ShowWireframe)
        {
            linesVisual.Color = Colors.White;
        }

        // 3. Build Skeleton Bones
        if (settings.ShowBones && scene.Skeletons.Count > 0)
        {
            foreach (var bone in scene.Skeletons)
            {
                BuildBoneVisual(bone, linesVisual);
            }
        }

        // 4. Build Scene Bounds Visual
        if (settings.ShowBoundingBox && scene.BoundsMin != scene.BoundsMax)
        {
            boundsVisual.BoundingBox = new Rect3D(
                scene.BoundsMin.X,
                scene.BoundsMin.Y,
                scene.BoundsMin.Z,
                Math.Max(0.1, scene.BoundsMax.X - scene.BoundsMin.X),
                Math.Max(0.1, scene.BoundsMax.Y - scene.BoundsMin.Y),
                Math.Max(0.1, scene.BoundsMax.Z - scene.BoundsMin.Z));
        }

        return rootVisual;
    }

    private void BuildNodeVisual(
        RenderNode node,
        RenderScene scene,
        RenderSettings settings,
        Model3DGroup parentGroup,
        LinesVisual3D linesVisual)
    {
        var nodeGroup = new Model3DGroup();
        if (!node.WorldTransform.IsIdentity)
        {
            nodeGroup.Transform = ToMatrixTransform3D(node.WorldTransform);
        }

        if (!string.IsNullOrEmpty(node.Name))
        {
            NodeVisualGroups[node.Name] = nodeGroup;
        }
        if (!string.IsNullOrEmpty(node.Id))
        {
            NodeVisualGroups[node.Id] = nodeGroup;
        }

        // 1. Meshes
        foreach (var mesh in node.Meshes)
        {
            var geom = BuildMeshGeometry(mesh);
            var mat = ResolveMaterial(mesh.MaterialId, scene, settings);

            var model = new GeometryModel3D(geom, mat)
            {
                BackMaterial = settings.Shading is ShadingMode.Wireframe ? null : mat
            };

            ModelToNodeMap[model] = node;
            nodeGroup.Children.Add(model);

            // Wireframe lines
            if (settings.Shading is ShadingMode.Wireframe or ShadingMode.SolidWireframe || settings.ShowWireframe)
            {
                AddWireframeLines(mesh, linesVisual, node.WorldTransform);
            }

            // Normal lines
            if (settings.ShowNormals)
            {
                AddNormalLines(mesh, linesVisual, settings.NormalScale, node.WorldTransform);
            }
        }

        // 2. Voxel Chunks (supporting per-block multi-material rendering)
        foreach (var chunk in node.VoxelChunks)
        {
            var meshesToRender = chunk.SubMeshes.Count > 0
                ? (IReadOnlyList<RenderMesh>)chunk.SubMeshes
                : (chunk.Mesh is not null ? [chunk.Mesh] : []);

            foreach (var vMesh in meshesToRender)
            {
                var geom = BuildMeshGeometry(vMesh, chunk.Position);
                var mat = ResolveMaterial(vMesh.MaterialId, scene, settings);

                var model = new GeometryModel3D(geom, mat)
                {
                    BackMaterial = mat
                };

                ModelToNodeMap[model] = node;
                nodeGroup.Children.Add(model);

                if (settings.Shading is ShadingMode.Wireframe or ShadingMode.SolidWireframe || settings.ShowWireframe)
                {
                    AddWireframeLines(vMesh, linesVisual, node.WorldTransform, chunk.Position);
                }
            }
        }

        parentGroup.Children.Add(nodeGroup);

        // 3. Children Nodes
        foreach (var child in node.Children)
        {
            BuildNodeVisual(child, scene, settings, parentGroup, linesVisual);
        }
    }

    private static MeshGeometry3D BuildMeshGeometry(RenderMesh mesh, Vector3? positionOffset = null)
    {
        var geom = new MeshGeometry3D();
        var offset = positionOffset ?? Vector3.Zero;

        var positions = new Point3DCollection(mesh.Positions.Length);
        for (int i = 0; i < mesh.Positions.Length; i++)
        {
            var p = mesh.Positions[i];
            positions.Add(new Point3D(p.X + offset.X, p.Y + offset.Y, p.Z + offset.Z));
        }
        geom.Positions = positions;

        var indices = new Int32Collection(mesh.Indices.Length);
        for (int i = 0; i < mesh.Indices.Length; i++)
        {
            indices.Add(mesh.Indices[i]);
        }
        geom.TriangleIndices = indices;

        if (mesh.Normals.Length == mesh.Positions.Length)
        {
            var normals = new Vector3DCollection(mesh.Normals.Length);
            for (int i = 0; i < mesh.Normals.Length; i++)
            {
                var n = mesh.Normals[i];
                normals.Add(new System.Windows.Media.Media3D.Vector3D(n.X, n.Y, n.Z));
            }
            geom.Normals = normals;
        }

        if (mesh.UVs.Length == mesh.Positions.Length)
        {
            var uvs = new PointCollection(mesh.UVs.Length);
            for (int i = 0; i < mesh.UVs.Length; i++)
            {
                var uv = mesh.UVs[i];
                uvs.Add(new System.Windows.Point(uv.X, 1.0 - uv.Y)); // Flip V for WPF convention
            }
            geom.TextureCoordinates = uvs;
        }

        geom.Freeze();
        return geom;
    }

    private Material ResolveMaterial(string? materialId, RenderScene scene, RenderSettings settings)
    {
        if (settings.Shading == ShadingMode.Wireframe)
        {
            return new DiffuseMaterial(Brushes.Transparent);
        }

        RenderMaterial? rMat = null;
        if (!string.IsNullOrEmpty(materialId))
        {
            scene.Materials.TryGetValue(materialId, out rMat);
        }

        rMat ??= RenderMaterial.Default;

        // Try load texture if enabled
        if (settings.ShowTextures && !string.IsNullOrEmpty(rMat.DiffuseTexturePath) && File.Exists(rMat.DiffuseTexturePath))
        {
            var brush = GetOrCreateTextureBrush(rMat.DiffuseTexturePath);
            if (brush is not null)
            {
                var texMat = new DiffuseMaterial(brush);
                texMat.Freeze();
                return texMat;
            }
        }

        // Solid / Translucent Color
        float alpha = rMat.BaseColor.W * rMat.Opacity;
        var col = Color.FromArgb(
            (byte)Math.Clamp(alpha * 255f, 0, 255),
            (byte)Math.Clamp(rMat.BaseColor.X * 255f, 0, 255),
            (byte)Math.Clamp(rMat.BaseColor.Y * 255f, 0, 255),
            (byte)Math.Clamp(rMat.BaseColor.Z * 255f, 0, 255));

        var solidBrush = new SolidColorBrush(col);
        solidBrush.Freeze();

        var matGroup = new MaterialGroup();
        matGroup.Children.Add(new DiffuseMaterial(solidBrush));

        if (rMat.SpecularColor.Length() > 0.1f)
        {
            var specCol = Color.FromArgb(
                (byte)Math.Clamp(rMat.SpecularColor.W * 255f, 0, 255),
                (byte)Math.Clamp(rMat.SpecularColor.X * 255f, 0, 255),
                (byte)Math.Clamp(rMat.SpecularColor.Y * 255f, 0, 255),
                (byte)Math.Clamp(rMat.SpecularColor.Z * 255f, 0, 255));
            var specBrush = new SolidColorBrush(specCol);
            specBrush.Freeze();
            matGroup.Children.Add(new SpecularMaterial(specBrush, rMat.Shininess));
        }

        matGroup.Freeze();
        return matGroup;
    }

    private ImageBrush? GetOrCreateTextureBrush(string texturePath)
    {
        return _textureBrushCache.GetOrAdd(texturePath, path =>
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();

                var brush = new ImageBrush(bitmap)
                {
                    TileMode = TileMode.Tile,
                    ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
                    ViewboxUnits = BrushMappingMode.RelativeToBoundingBox,
                    Viewport = new Rect(0, 0, 1, 1)
                };
                RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.NearestNeighbor);
                brush.Freeze();
                return brush;
            }
            catch
            {
                return null!;
            }
        });
    }

    private static void AddWireframeLines(RenderMesh mesh, LinesVisual3D linesVisual, Matrix4x4? worldTransform = null, Vector3? positionOffset = null)
    {
        if (mesh.WireframeIndices.Length == 0) return;

        var points = linesVisual.Points ?? new Point3DCollection();
        var offset = positionOffset ?? Vector3.Zero;
        bool hasTransform = worldTransform.HasValue && !worldTransform.Value.IsIdentity;
        var mat = worldTransform ?? Matrix4x4.Identity;

        for (int i = 0; i < mesh.WireframeIndices.Length; i += 2)
        {
            int idx0 = mesh.WireframeIndices[i];
            int idx1 = mesh.WireframeIndices[i + 1];

            if (idx0 < mesh.Positions.Length && idx1 < mesh.Positions.Length)
            {
                var p0 = mesh.Positions[idx0] + offset;
                var p1 = mesh.Positions[idx1] + offset;

                if (hasTransform)
                {
                    p0 = Vector3.Transform(p0, mat);
                    p1 = Vector3.Transform(p1, mat);
                }

                points.Add(new Point3D(p0.X, p0.Y, p0.Z));
                points.Add(new Point3D(p1.X, p1.Y, p1.Z));
            }
        }
        linesVisual.Points = points;
    }

    private static void AddNormalLines(RenderMesh mesh, LinesVisual3D linesVisual, float scale, Matrix4x4? worldTransform = null)
    {
        if (mesh.Normals.Length != mesh.Positions.Length) return;

        var points = linesVisual.Points ?? new Point3DCollection();
        bool hasTransform = worldTransform.HasValue && !worldTransform.Value.IsIdentity;
        var mat = worldTransform ?? Matrix4x4.Identity;

        for (int i = 0; i < mesh.Positions.Length; i++)
        {
            var p0 = mesh.Positions[i];
            var n = mesh.Normals[i] * scale;

            if (hasTransform)
            {
                p0 = Vector3.Transform(p0, mat);
                n = Vector3.TransformNormal(n, mat);
            }

            var p1 = p0 + n;
            points.Add(new Point3D(p0.X, p0.Y, p0.Z));
            points.Add(new Point3D(p1.X, p1.Y, p1.Z));
        }
        linesVisual.Points = points;
    }

    private static void BuildBoneVisual(RenderBone bone, LinesVisual3D linesVisual)
    {
        var points = linesVisual.Points ?? new Point3DCollection();
        var worldPivot = Vector3.Transform(bone.Pivot, bone.WorldMatrix);

        foreach (var child in bone.Children)
        {
            var childPivot = Vector3.Transform(child.Pivot, child.WorldMatrix);
            points.Add(new Point3D(worldPivot.X, worldPivot.Y, worldPivot.Z));
            points.Add(new Point3D(childPivot.X, childPivot.Y, childPivot.Z));

            BuildBoneVisual(child, linesVisual);
        }

        linesVisual.Points = points;
    }

    public static MatrixTransform3D ToMatrixTransform3D(Matrix4x4 m)
    {
        var mat = new Matrix3D(
            m.M11, m.M12, m.M13, m.M14,
            m.M21, m.M22, m.M23, m.M24,
            m.M31, m.M32, m.M33, m.M34,
            m.M41, m.M42, m.M43, m.M44);
        var transform = new MatrixTransform3D(mat);
        transform.Freeze();
        return transform;
    }
}
