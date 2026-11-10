using System.Numerics;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Rendering.Contracts;
using NEXUS3D.Rendering.Domain;

namespace NEXUS3D.Rendering.Services;

/// <summary>
/// Core state manager and coordinator for rendering scenes, camera controls, and object selections.
/// </summary>
public sealed class RenderService : IRenderService
{
    private readonly IRenderSceneAdapter _adapter;
    private readonly ICameraService _cameraService;
    private readonly IBoundingBoxCalculator _boundsCalculator;

    public RenderScene? CurrentScene { get; private set; }
    public RenderSettings Settings { get; set; } = RenderSettings.Default;
    public CameraState Camera
    {
        get => _cameraService.CurrentCamera;
        set => _cameraService.SetCamera(value);
    }

    public SelectionInfo CurrentSelection { get; private set; } = SelectionInfo.None;

    public RenderService(
        IRenderSceneAdapter adapter,
        ICameraService cameraService,
        IBoundingBoxCalculator boundsCalculator)
    {
        _adapter = adapter;
        _cameraService = cameraService;
        _boundsCalculator = boundsCalculator;
    }

    public Task<RenderScene> LoadSceneAsync(Scene3D scene, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var rScene = _adapter.Adapt(scene, Settings);
        CurrentScene = rScene;

        // Auto-frame camera on newly loaded scene
        _cameraService.FocusBounds(rScene.BoundsMin, rScene.BoundsMax);

        ClearSelection();
        return Task.FromResult(rScene);
    }

    public void UpdateSettings(RenderSettings settings)
    {
        Settings = settings ?? RenderSettings.Default;
    }

    public void SelectNode(string? nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId) || CurrentScene is null)
        {
            ClearSelection();
            return;
        }

        var node = FindNodeRecursive(CurrentScene.RootNodes, nodeId);
        if (node is null)
        {
            ClearSelection();
            return;
        }

        CurrentSelection = new SelectionInfo(
            SelectedNodeId: node.Id,
            SelectedMeshId: node.Meshes.FirstOrDefault()?.Id,
            SelectedBoneName: null,
            SelectedPosition: (node.BoundsMin + node.BoundsMax) * 0.5f,
            BoundsMin: node.BoundsMin,
            BoundsMax: node.BoundsMax,
            SummaryText: $"Node: {node.Name} ({node.Meshes.Count} meshes, {node.VoxelChunks.Count} chunks)");
    }

    public void SelectBone(string? boneName)
    {
        if (string.IsNullOrWhiteSpace(boneName) || CurrentScene is null)
        {
            ClearSelection();
            return;
        }

        var bone = FindBoneRecursive(CurrentScene.Skeletons, boneName);
        if (bone is null)
        {
            ClearSelection();
            return;
        }

        var pivot = Vector3.Transform(bone.Pivot, bone.WorldMatrix);
        CurrentSelection = new SelectionInfo(
            SelectedNodeId: null,
            SelectedMeshId: null,
            SelectedBoneName: bone.Name,
            SelectedPosition: pivot,
            BoundsMin: pivot - Vector3.One * 0.2f,
            BoundsMax: pivot + Vector3.One * 0.2f,
            SummaryText: $"Bone: {bone.Name} (Pivot: {pivot:F2})");
    }

    public void ClearSelection()
    {
        CurrentSelection = SelectionInfo.None;
    }

    private static RenderNode? FindNodeRecursive(IEnumerable<RenderNode> nodes, string id)
    {
        foreach (var n in nodes)
        {
            if (string.Equals(n.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return n;
            }

            var childFound = FindNodeRecursive(n.Children, id);
            if (childFound is not null)
            {
                return childFound;
            }
        }

        return null;
    }

    private static RenderBone? FindBoneRecursive(IEnumerable<RenderBone> bones, string name)
    {
        foreach (var b in bones)
        {
            if (string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return b;
            }

            var childFound = FindBoneRecursive(b.Children, name);
            if (childFound is not null)
            {
                return childFound;
            }
        }

        return null;
    }
}

