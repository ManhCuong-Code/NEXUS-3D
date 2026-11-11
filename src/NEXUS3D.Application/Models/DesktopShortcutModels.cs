namespace NEXUS3D.Application.Models;

/// <summary>
/// Status and metadata of the NEXUS-3D Windows Desktop shortcut.
/// </summary>
public sealed record DesktopShortcutStatus(
    bool Exists,
    string ShortcutPath,
    string TargetPath,
    string WorkingDirectory,
    string IconLocation,
    bool IsTargetValid,
    bool IsDevelopmentBuild,
    string? Message = null);

