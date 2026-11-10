using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NEXUS3D.Domain.Materials;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Skeletons;
using NEXUS3D.Editor.Abstractions;
using NEXUS3D.Editor.Engine.Commands;
using NEXUS3D.Editor.Engine.Mathematics;
using NEXUS3D.Rendering.Contracts;
using NEXUS3D.Rendering.Domain;

namespace NEXUS3D.App.ViewModels;

/// <summary>
/// ViewModel displaying properties, interactive transform inputs, pivot, bones, textures, and UV controls.
/// </summary>
public sealed partial class ObjectInspectorViewModel : ObservableObject
{
    private readonly IRenderService _renderService;
    private readonly ITransformService _transformService;
    private readonly IPivotService _pivotService;
    private readonly IBoneEditorService _boneService;
    private readonly ITextureEditorService _textureService;
    private readonly IUndoRedoService _undoRedoService;
    private readonly ISelectionService _selectionService;

    private bool _isUpdatingFromModel;

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

    // --- Interactive Transform Properties ---
    [ObservableProperty]
    private double _posX;

    [ObservableProperty]
    private double _posY;

    [ObservableProperty]
    private double _posZ;

    [ObservableProperty]
    private double _rotX;

    [ObservableProperty]
    private double _rotY;

    [ObservableProperty]
    private double _rotZ;

    [ObservableProperty]
    private double _scaleX = 1.0;

    [ObservableProperty]
    private double _scaleY = 1.0;

    [ObservableProperty]
    private double _scaleZ = 1.0;

    [ObservableProperty]
    private TransformSpace _activeSpace = TransformSpace.World;

    // --- Pivot Properties ---
    [ObservableProperty]
    private double _pivotX;

    [ObservableProperty]
    private double _pivotY;

    [ObservableProperty]
    private double _pivotZ;

    // --- UV & Texture Properties ---
    [ObservableProperty]
    private string _selectedSlot = "Albedo";

    [ObservableProperty]
    private double _uvOffsetX;

    [ObservableProperty]
    private double _uvOffsetY;

    [ObservableProperty]
    private double _uvScaleX = 1.0;

    [ObservableProperty]
    private double _uvScaleY = 1.0;

    [ObservableProperty]
    private double _uvRotation;

    // --- History Status ---
    [ObservableProperty]
    private bool _canUndo;

    [ObservableProperty]
    private bool _canRedo;

    [ObservableProperty]
    private int _undoCount;

    [ObservableProperty]
    private int _redoCount;

    public SceneNode? ActiveNode { get; private set; }
    public BoneData? ActiveBone { get; private set; }
    public Scene3D? ActiveScene { get; private set; }

    public ObjectInspectorViewModel(
        IRenderService renderService,
        ITransformService transformService,
        IPivotService pivotService,
        IBoneEditorService boneService,
        ITextureEditorService textureService,
        IUndoRedoService undoRedoService,
        ISelectionService selectionService)
    {
        _renderService = renderService;
        _transformService = transformService;
        _pivotService = pivotService;
        _boneService = boneService;
        _textureService = textureService;
        _undoRedoService = undoRedoService;
        _selectionService = selectionService;

        _undoRedoService.HistoryChanged += (s, e) => UpdateHistoryStatus();
        UpdateHistoryStatus();
    }

    public void InspectUsmNode(SceneNode? node, Scene3D? scene)
    {
        ActiveNode = node;
        ActiveBone = null;
        ActiveScene = scene;

        if (node is null)
        {
            Clear();
            return;
        }

        _isUpdatingFromModel = true;
        try
        {
            HasSelection = true;
            ElementName = node.Name;
            ElementType = node.Object?.VoxelGrids.Count > 0 ? "Voxel Model" : "Mesh Model";

            var worldPos = node.GetWorldMatrix().Translation;
            PositionDisplay = $"({worldPos.X:F2}, {worldPos.Y:F2}, {worldPos.Z:F2})";

            var worldBounds = node.GetWorldBounds();
            if (worldBounds.HasValue)
            {
                var size = worldBounds.Value.Max - worldBounds.Value.Min;
                BoundsSizeDisplay = $"{size.X:F2} × {size.Y:F2} × {size.Z:F2}";
            }
            else
            {
                BoundsSizeDisplay = "(0.0, 0.0, 0.0)";
            }

            TriangleCount = node.Object?.Meshes.Sum(m => m.Faces.Count) ?? 0;
            VertexCount = node.Object?.Meshes.Sum(m => m.Vertices.Count) ?? 0;
            VoxelCount = node.Object?.VoxelGrids.Sum(v => v.GetVoxelCount()) ?? 0;

            // Load transform values
            PosX = node.LocalTransform.Position.X;
            PosY = node.LocalTransform.Position.Y;
            PosZ = node.LocalTransform.Position.Z;

            var euler = TransformMath.QuaternionToEulerDegrees(node.LocalTransform.Rotation);
            RotX = Math.Round(euler.X, 1);
            RotY = Math.Round(euler.Y, 1);
            RotZ = Math.Round(euler.Z, 1);

            ScaleX = Math.Round(node.LocalTransform.Scale.X, 3);
            ScaleY = Math.Round(node.LocalTransform.Scale.Y, 3);
            ScaleZ = Math.Round(node.LocalTransform.Scale.Z, 3);

            // Pivot values
            var pivot = _pivotService.GetPivot(node, worldSpace: false);
            PivotX = pivot.X;
            PivotY = pivot.Y;
            PivotZ = pivot.Z;

            // Material & Texture
            var firstMesh = node.Object?.Meshes.FirstOrDefault();
            if (firstMesh?.Faces.FirstOrDefault()?.MaterialId is { } matId &&
                scene?.Materials.Materials.FirstOrDefault(m => m.Id == matId) is { } mat)
            {
                MaterialName = mat.Name;
                if (mat.TextureSlots.TryGetValue(SelectedSlot, out var slot))
                {
                    TextureSummary = $"Slot '{SelectedSlot}': Texture {slot.TextureId}";
                    UvOffsetX = slot.Offset.X;
                    UvOffsetY = slot.Offset.Y;
                    UvScaleX = slot.Scale.X;
                    UvScaleY = slot.Scale.Y;
                    UvRotation = slot.Rotation;
                }
                else
                {
                    TextureSummary = $"Slot '{SelectedSlot}': None";
                }
            }
            else
            {
                MaterialName = "Default Surface";
                TextureSummary = "None";
            }
        }
        finally
        {
            _isUpdatingFromModel = false;
        }
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
        ActiveNode = null;
        ActiveBone = null;
        ElementName = "None";
        ElementType = "None";
        PositionDisplay = "(0.0, 0.0, 0.0)";
        BoundsSizeDisplay = "(0.0, 0.0, 0.0)";
        TriangleCount = 0;
        VertexCount = 0;
        VoxelCount = 0;
        MaterialName = "None";
        TextureSummary = "None";

        PosX = PosY = PosZ = 0;
        RotX = RotY = RotZ = 0;
        ScaleX = ScaleY = ScaleZ = 1.0;
        PivotX = PivotY = PivotZ = 0;
    }

    [RelayCommand]
    public void ApplyNumericTransform()
    {
        if (_isUpdatingFromModel || ActiveNode is null) return;

        var oldTransform = ActiveNode.LocalTransform.DeepClone();
        var newPos = new Vector3D(PosX, PosY, PosZ);
        var newRot = QuaternionD.FromEulerAnglesDegrees(RotX, RotY, RotZ);
        var newScale = new Vector3D(
            Math.Abs(ScaleX) < 0.001 ? 0.001 : ScaleX,
            Math.Abs(ScaleY) < 0.001 ? 0.001 : ScaleY,
            Math.Abs(ScaleZ) < 0.001 ? 0.001 : ScaleZ);

        var newTransform = new Transform3D(newPos, newRot, newScale);
        var cmd = new TransformCommand($"Transform '{ActiveNode.Name}'", ActiveNode, oldTransform, newTransform);
        _undoRedoService.Execute(cmd);
    }

    [RelayCommand]
    public void ResetTransform()
    {
        if (ActiveNode is null) return;

        var oldTransform = ActiveNode.LocalTransform.DeepClone();
        var newTransform = Transform3D.Identity();
        var cmd = new TransformCommand($"Reset Transform '{ActiveNode.Name}'", ActiveNode, oldTransform, newTransform);
        _undoRedoService.Execute(cmd);

        InspectUsmNode(ActiveNode, ActiveScene);
    }

    [RelayCommand]
    public void ApplyBakeTransform()
    {
        if (ActiveNode is null) return;

        var cmd = new ApplyTransformCommand([ActiveNode]);
        _undoRedoService.Execute(cmd);

        InspectUsmNode(ActiveNode, ActiveScene);
    }

    [RelayCommand]
    public void CenterPivot()
    {
        if (ActiveNode is null) return;

        _pivotService.CenterPivot(ActiveNode);
        InspectUsmNode(ActiveNode, ActiveScene);
    }

    [RelayCommand]
    public void PivotToOrigin()
    {
        if (ActiveNode is null) return;

        _pivotService.PivotToOrigin(ActiveNode);
        InspectUsmNode(ActiveNode, ActiveScene);
    }

    [RelayCommand]
    public void PivotToBoundsCenter()
    {
        if (ActiveNode is null) return;

        _pivotService.PivotToBoundsCenter(ActiveNode);
        InspectUsmNode(ActiveNode, ActiveScene);
    }

    [RelayCommand]
    public void ToggleTransformSpace()
    {
        ActiveSpace = ActiveSpace == TransformSpace.World ? TransformSpace.Local : TransformSpace.World;
        _transformService.TransformSpace = ActiveSpace;
    }

    [RelayCommand]
    public void ApplyUVTransform()
    {
        if (ActiveNode?.Object is null) return;

        foreach (var mesh in ActiveNode.Object.Meshes)
        {
            var oldUVs = mesh.Vertices.Select(v => v.TexCoord).ToList();
            _textureService.TransformUVs(mesh, new Vector2D(UvOffsetX, UvOffsetY), new Vector2D(UvScaleX, UvScaleY), UvRotation);
            var newUVs = mesh.Vertices.Select(v => v.TexCoord).ToList();

            var cmd = new UVTransformCommand(mesh, oldUVs, newUVs);
            _undoRedoService.Execute(cmd);
        }
    }

    [RelayCommand]
    public void FlipUVHorizontal()
    {
        if (ActiveNode?.Object is null) return;

        foreach (var mesh in ActiveNode.Object.Meshes)
        {
            var oldUVs = mesh.Vertices.Select(v => v.TexCoord).ToList();
            _textureService.FlipUVs(mesh, horizontal: true, vertical: false);
            var newUVs = mesh.Vertices.Select(v => v.TexCoord).ToList();

            var cmd = new UVTransformCommand(mesh, oldUVs, newUVs);
            _undoRedoService.Execute(cmd);
        }
    }

    [RelayCommand]
    public void FlipUVVertical()
    {
        if (ActiveNode?.Object is null) return;

        foreach (var mesh in ActiveNode.Object.Meshes)
        {
            var oldUVs = mesh.Vertices.Select(v => v.TexCoord).ToList();
            _textureService.FlipUVs(mesh, horizontal: false, vertical: true);
            var newUVs = mesh.Vertices.Select(v => v.TexCoord).ToList();

            var cmd = new UVTransformCommand(mesh, oldUVs, newUVs);
            _undoRedoService.Execute(cmd);
        }
    }

    [RelayCommand]
    public void Undo()
    {
        _undoRedoService.Undo();
        if (ActiveNode is not null)
        {
            InspectUsmNode(ActiveNode, ActiveScene);
        }
    }

    [RelayCommand]
    public void Redo()
    {
        _undoRedoService.Redo();
        if (ActiveNode is not null)
        {
            InspectUsmNode(ActiveNode, ActiveScene);
        }
    }

    private void UpdateHistoryStatus()
    {
        CanUndo = _undoRedoService.CanUndo;
        CanRedo = _undoRedoService.CanRedo;
        UndoCount = _undoRedoService.UndoHistory.Count;
        RedoCount = _undoRedoService.RedoHistory.Count;
    }
}
