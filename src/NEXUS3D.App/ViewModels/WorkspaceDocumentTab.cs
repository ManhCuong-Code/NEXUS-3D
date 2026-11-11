using CommunityToolkit.Mvvm.ComponentModel;
using NEXUS3D.Application.Models;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Rendering.Contracts;
using NEXUS3D.Rendering.Domain;
using NEXUS3D.Rendering.Profiles;

namespace NEXUS3D.App.ViewModels;

/// <summary>
/// Represents an independently managed 3D document tab within the multi-file workspace.
/// Preserves scene graph, camera orientation, viewer profile, materials, and settings.
/// </summary>
public sealed partial class WorkspaceDocumentTab : ObservableObject
{
    public Guid Id { get; } = Guid.NewGuid();

    [ObservableProperty]
    private string _title = "Untitled";

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private ModelFormatType _formatType = ModelFormatType.Unknown;

    [ObservableProperty]
    private FormatCategory _category = FormatCategory.PolygonMesh;

    [ObservableProperty]
    private IFormatDescriptor? _formatDescriptor;

    [ObservableProperty]
    private IViewerProfile _viewerProfile = new GenericViewerProfile();

    [ObservableProperty]
    private Scene3D? _rawUsmScene;

    [ObservableProperty]
    private RenderScene? _renderScene;

    [ObservableProperty]
    private CameraState _cameraState = CameraState.Default;

    [ObservableProperty]
    private RenderSettings _settings = RenderSettings.Default;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusSummary = "Ready";

    [ObservableProperty]
    private int _triangleCount;

    [ObservableProperty]
    private int _vertexCount;

    [ObservableProperty]
    private int _voxelCount;

    public string FormatBadge => FormatDescriptor?.ShortCode ?? FormatType.ToString().ToUpperInvariant();

    public string FormatColor => Category switch
    {
        FormatCategory.PolygonMesh => "#74B9FF",       // Sky blue
        FormatCategory.MinecraftSchematic => "#55EFC4", // Minecraft emerald
        FormatCategory.VoxelModel => "#FDCB6E",        // Warm amber
        FormatCategory.BedrockGeometry => "#FF7675",   // Coral red
        _ => "#A29BFE"                                 // Soft violet
    };

    public string FormattedStats => Category switch
    {
        FormatCategory.MinecraftSchematic or FormatCategory.VoxelModel => $"{VoxelCount:N0} voxels",
        FormatCategory.BedrockGeometry => $"{TriangleCount:N0} tris (Bones)",
        _ => $"{TriangleCount:N0} tris | {VertexCount:N0} verts"
    };

    public void UpdateStats(RenderScene rScene)
    {
        TriangleCount = (int)rScene.TotalTriangles;
        VertexCount = (int)rScene.TotalVertices;
        VoxelCount = (int)rScene.TotalVoxels;
        OnPropertyChanged(nameof(FormattedStats));
    }
}
