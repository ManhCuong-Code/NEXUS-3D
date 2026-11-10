using System.IO;
using System.Numerics;
using CommunityToolkit.Mvvm.ComponentModel;
using NEXUS3D.Rendering.Contracts;
using NEXUS3D.Rendering.Domain;

namespace NEXUS3D.App.ViewModels;

/// <summary>
/// ViewModel displaying properties, transforms, geometry statistics, and materials for selected elements.
/// </summary>
public sealed partial class ObjectInspectorViewModel : ObservableObject
{
    private readonly IRenderService _renderService;

    [ObservableProperty]
    private bool _hasSelection;

    [ObservableProperty]
    private string _elementName = "None";

    [ObservableProperty]
    private string _elementType = "None";

    [ObservableProperty]
    private string _positionDisplay = "(0.0, 0.0, 0.0)";

    [ObservableProperty]
    private string _boundsSizeDisplay = "(0.0, 0.0, 0.0)";

    [ObservableProperty]
    private int _triangleCount;

    [ObservableProperty]
    private int _vertexCount;

    [ObservableProperty]
    private int _voxelCount;

    [ObservableProperty]
    private string _materialName = "None";

    [ObservableProperty]
    private string _textureSummary = "None";

    public ObjectInspectorViewModel(IRenderService renderService)
    {
        _renderService = renderService;
    }

    public void InspectNode(RenderNode? node, RenderScene? scene)
    {
        if (node is null)
        {
            Clear();
            return;
        }

        HasSelection = true;
        ElementName = node.Name;
        ElementType = node.NodeType.ToString();

        var center = (node.BoundsMin + node.BoundsMax) * 0.5f;
        var size = node.BoundsMax - node.BoundsMin;
        PositionDisplay = $"({center.X:F2}, {center.Y:F2}, {center.Z:F2})";
        BoundsSizeDisplay = $"{size.X:F2} × {size.Y:F2} × {size.Z:F2}";

        TriangleCount = node.Meshes.Sum(m => m.TriangleCount);
        VertexCount = node.Meshes.Sum(m => m.VertexCount);
        VoxelCount = node.VoxelChunks.Sum(c => c.VoxelCount);

        var firstMesh = node.Meshes.FirstOrDefault();
        if (firstMesh?.MaterialId is not null && scene?.Materials.TryGetValue(firstMesh.MaterialId, out var mat) == true)
        {
            MaterialName = mat.Name;
            TextureSummary = !string.IsNullOrEmpty(mat.DiffuseTexturePath) ? Path.GetFileName(mat.DiffuseTexturePath) : "No Texture";
        }
        else
        {
            MaterialName = "Default / Vertex Colored";
            TextureSummary = "None";
        }
    }

    public void InspectBone(RenderBone? bone)
    {
        if (bone is null)
        {
            Clear();
            return;
        }

        HasSelection = true;
        ElementName = bone.Name;
        ElementType = "Skeleton Bone";
        PositionDisplay = $"Pivot: ({bone.Pivot.X:F2}, {bone.Pivot.Y:F2}, {bone.Pivot.Z:F2})";
        BoundsSizeDisplay = $"Rotation: ({bone.Rotation.X:F1}°, {bone.Rotation.Y:F1}°, {bone.Rotation.Z:F1}°)";
        TriangleCount = 0;
        VertexCount = 0;
        VoxelCount = 0;
        MaterialName = "Bone Joint";
        TextureSummary = bone.ParentName is not null ? $"Parent: {bone.ParentName}" : "Root Bone";
    }

    public void Clear()
    {
        HasSelection = false;
        ElementName = "None";
        ElementType = "None";
        PositionDisplay = "(0.0, 0.0, 0.0)";
        BoundsSizeDisplay = "(0.0, 0.0, 0.0)";
        TriangleCount = 0;
        VertexCount = 0;
        VoxelCount = 0;
        MaterialName = "None";
        TextureSummary = "None";
    }
}
