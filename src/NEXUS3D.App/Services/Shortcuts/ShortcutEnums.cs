namespace NEXUS3D.App.Services.Shortcuts;

/// <summary>
/// Logical grouping of keyboard shortcuts for UI presentation and customization.
/// </summary>
public enum ShortcutCategory
{
    General,
    FileManagement,
    Viewport3D,
    ModelEditing,
    Conversion,
    Navigation
}

/// <summary>
/// Contextual scope in which a keyboard shortcut is active.
/// </summary>
public enum ShortcutScope
{
    /// <summary>
    /// Active everywhere across the application unless text input is focused.
    /// </summary>
    Global,

    /// <summary>
    /// Active only when the 3D Viewport or Editor Workspace is focused / active.
    /// </summary>
    ViewportOnly
}

