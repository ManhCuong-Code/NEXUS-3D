using NEXUS3D.Domain.Enums;
using NEXUS3D.Rendering.Contracts;
using NEXUS3D.Rendering.Domain;

namespace NEXUS3D.Rendering.Profiles;

/// <summary>
/// Specialized 3D view mode and visual presentation profile tailored to specific format types.
/// </summary>
public enum ViewerProfileType
{
    Generic = 0,
    Mesh = 1,
    Block = 2,
    Voxel = 3,
    BedrockGeometry = 4
}

/// <summary>
/// Contract for a specialized format-tailored viewer profile defining default camera, grid, lighting, and shading.
/// </summary>
public interface IViewerProfile
{
    ViewerProfileType ProfileType { get; }
    string Name { get; }
    string Description { get; }
    ShadingMode DefaultShading { get; }
    CameraPresetView DefaultCameraPreset { get; }
    CameraProjectionType DefaultProjection { get; }
    bool ShowGrid { get; }
    bool ShowAxes { get; }
    bool ShowWireframe { get; }
    bool ShowBones { get; }
    bool ShowBoundingBox { get; }
    string BackgroundColorHex { get; }
    float CameraDistanceMultiplier { get; }

    RenderSettings CreateDefaultSettings();
}

public sealed class MeshViewerProfile : IViewerProfile
{
    public ViewerProfileType ProfileType => ViewerProfileType.Mesh;
    public string Name => "Polygon Mesh Studio";
    public string Description => "High-fidelity surface shading, texture mapping, wireframe overlay, and mesh hierarchy inspection.";
    public ShadingMode DefaultShading => ShadingMode.Textured;
    public CameraPresetView DefaultCameraPreset => CameraPresetView.Reset;
    public CameraProjectionType DefaultProjection => CameraProjectionType.Perspective;
    public bool ShowGrid => true;
    public bool ShowAxes => true;
    public bool ShowWireframe => false;
    public bool ShowBones => false;
    public bool ShowBoundingBox => false;
    public string BackgroundColorHex => "#1E1E24";
    public float CameraDistanceMultiplier => 1.8f;

    public RenderSettings CreateDefaultSettings() => new(
        Shading: DefaultShading,
        ShowGrid: ShowGrid,
        ShowAxes: ShowAxes,
        ShowBoundingBox: ShowBoundingBox,
        ShowWireframe: ShowWireframe,
        ShowBones: ShowBones,
        ShowTextures: true,
        BackgroundColorHex: BackgroundColorHex);
}

public sealed class BlockViewerProfile : IViewerProfile
{
    public ViewerProfileType ProfileType => ViewerProfileType.Block;
    public string Name => "Minecraft Block Workspace";
    public string Description => "Minecraft voxel coordinate space with standard block palette recognition, alpha transparency, and isometric overview.";
    public ShadingMode DefaultShading => ShadingMode.Textured;
    public CameraPresetView DefaultCameraPreset => CameraPresetView.Isometric;
    public CameraProjectionType DefaultProjection => CameraProjectionType.Perspective;
    public bool ShowGrid => true;
    public bool ShowAxes => true;
    public bool ShowWireframe => false;
    public bool ShowBones => false;
    public bool ShowBoundingBox => true;
    public string BackgroundColorHex => "#181B22";
    public float CameraDistanceMultiplier => 2.0f;

    public RenderSettings CreateDefaultSettings() => new(
        Shading: DefaultShading,
        ShowGrid: ShowGrid,
        ShowAxes: ShowAxes,
        ShowBoundingBox: ShowBoundingBox,
        ShowWireframe: ShowWireframe,
        ShowBones: ShowBones,
        ShowTextures: true,
        BackgroundColorHex: BackgroundColorHex);
}

public sealed class VoxelViewerProfile : IViewerProfile
{
    public ViewerProfileType ProfileType => ViewerProfileType.Voxel;
    public string Name => "Voxel Studio Workspace";
    public string Description => "Voxel grid matrix inspection, palette swatches, chunk boundary outlines, and cuboid structure tools.";
    public ShadingMode DefaultShading => ShadingMode.Textured;
    public CameraPresetView DefaultCameraPreset => CameraPresetView.Isometric;
    public CameraProjectionType DefaultProjection => CameraProjectionType.Perspective;
    public bool ShowGrid => true;
    public bool ShowAxes => true;
    public bool ShowWireframe => false;
    public bool ShowBones => false;
    public bool ShowBoundingBox => true;
    public string BackgroundColorHex => "#161820";
    public float CameraDistanceMultiplier => 1.9f;

    public RenderSettings CreateDefaultSettings() => new(
        Shading: DefaultShading,
        ShowGrid: ShowGrid,
        ShowAxes: ShowAxes,
        ShowBoundingBox: ShowBoundingBox,
        ShowWireframe: ShowWireframe,
        ShowBones: ShowBones,
        ShowTextures: true,
        BackgroundColorHex: BackgroundColorHex);
}

public sealed class BedrockGeometryViewerProfile : IViewerProfile
{
    public ViewerProfileType ProfileType => ViewerProfileType.BedrockGeometry;
    public string Name => "Bedrock Geometry Studio";
    public string Description => "Blockbench-style bone kinematics, cuboid element hierarchy, pivot point gizmos, and box UV rendering.";
    public ShadingMode DefaultShading => ShadingMode.Textured;
    public CameraPresetView DefaultCameraPreset => CameraPresetView.Front;
    public CameraProjectionType DefaultProjection => CameraProjectionType.Perspective;
    public bool ShowGrid => true;
    public bool ShowAxes => true;
    public bool ShowWireframe => false;
    public bool ShowBones => true;
    public bool ShowBoundingBox => false;
    public string BackgroundColorHex => "#1A1A22";
    public float CameraDistanceMultiplier => 1.7f;

    public RenderSettings CreateDefaultSettings() => new(
        Shading: DefaultShading,
        ShowGrid: ShowGrid,
        ShowAxes: ShowAxes,
        ShowBoundingBox: ShowBoundingBox,
        ShowWireframe: ShowWireframe,
        ShowBones: ShowBones,
        ShowTextures: true,
        BackgroundColorHex: BackgroundColorHex);
}

public sealed class GenericViewerProfile : IViewerProfile
{
    public ViewerProfileType ProfileType => ViewerProfileType.Generic;
    public string Name => "Universal 3D Viewer";
    public string Description => "Standard 3D inspection profile.";
    public ShadingMode DefaultShading => ShadingMode.Textured;
    public CameraPresetView DefaultCameraPreset => CameraPresetView.Reset;
    public CameraProjectionType DefaultProjection => CameraProjectionType.Perspective;
    public bool ShowGrid => true;
    public bool ShowAxes => true;
    public bool ShowWireframe => false;
    public bool ShowBones => true;
    public bool ShowBoundingBox => false;
    public string BackgroundColorHex => "#1E1E24";
    public float CameraDistanceMultiplier => 1.8f;

    public RenderSettings CreateDefaultSettings() => RenderSettings.Default;
}

/// <summary>
/// Registry resolving viewer profiles for model formats and workspace contexts.
/// </summary>
public static class ViewerProfileRegistry
{
    private static readonly IViewerProfile Mesh = new MeshViewerProfile();
    private static readonly IViewerProfile Block = new BlockViewerProfile();
    private static readonly IViewerProfile Voxel = new VoxelViewerProfile();
    private static readonly IViewerProfile Bedrock = new BedrockGeometryViewerProfile();
    private static readonly IViewerProfile Generic = new GenericViewerProfile();

    public static IViewerProfile GetProfile(ViewerProfileType type) => type switch
    {
        ViewerProfileType.Mesh => Mesh,
        ViewerProfileType.Block => Block,
        ViewerProfileType.Voxel => Voxel,
        ViewerProfileType.BedrockGeometry => Bedrock,
        _ => Generic
    };

    public static IViewerProfile ResolveForFormat(ModelFormatType formatType) => formatType switch
    {
        ModelFormatType.Obj => Mesh,
        ModelFormatType.Schematic => Block,
        ModelFormatType.VoxelJson => Voxel,
        ModelFormatType.BedrockGeometry => Bedrock,
        _ => Generic
    };
}
