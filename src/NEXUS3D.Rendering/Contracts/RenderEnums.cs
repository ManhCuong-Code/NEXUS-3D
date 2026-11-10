namespace NEXUS3D.Rendering.Contracts;

public enum ShadingMode
{
    Solid = 0,
    Flat = 1,
    Smooth = 2,
    Wireframe = 3,
    SolidWireframe = 4,
    Textured = 5,
    VoxelGrid = 6
}

public enum CameraProjectionType
{
    Perspective = 0,
    Orthographic = 1
}

public enum CameraPresetView
{
    Front = 0,
    Back = 1,
    Left = 2,
    Right = 3,
    Top = 4,
    Bottom = 5,
    Isometric = 6,
    Reset = 7
}

public enum RenderNodeType
{
    Mesh = 0,
    VoxelChunk = 1,
    Bone = 2,
    Locator = 3,
    Group = 4
}
