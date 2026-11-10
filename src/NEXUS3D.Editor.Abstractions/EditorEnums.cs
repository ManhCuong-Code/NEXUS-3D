namespace NEXUS3D.Editor.Abstractions;

/// <summary>
/// Active interactive manipulation tool mode.
/// </summary>
public enum EditorToolType
{
    None = 0,
    Select = 1,
    Translate = 2,
    Rotate = 3,
    Scale = 4,
    Pivot = 5,
    Bone = 6,
    Texture = 7
}

/// <summary>
/// Coordinate reference frame for transformation tools.
/// </summary>
public enum TransformSpace
{
    World = 0,
    Local = 1
}

/// <summary>
/// Specific interactive gizmo handle component being hovered or dragged.
/// </summary>
public enum GizmoHandle
{
    None = 0,
    AxisX = 1,
    AxisY = 2,
    AxisZ = 3,
    PlaneXY = 4,
    PlaneXZ = 5,
    PlaneYZ = 6,
    Center = 7
}

/// <summary>
/// Preset pivot placement modes.
/// </summary>
public enum PivotMode
{
    Custom = 0,
    Center = 1,
    Origin = 2,
    BoundsCenter = 3,
    SelectionCenter = 4
}

