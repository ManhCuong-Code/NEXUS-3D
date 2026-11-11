using NEXUS3D.Application.Models;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Application.Interfaces;

/// <summary>
/// Service for managing the NEXUS-3D Windows Shell desktop shortcut (.lnk).
/// Resolves standard Windows Known Folders (Desktop), inspects target executable and icon locations,
/// and handles creation, recreation, removal, and installation directory opening.
/// </summary>
public interface IDesktopShortcutService
{
    /// <summary>
    /// Gets detailed status and metadata of the desktop shortcut.
    /// </summary>
    DesktopShortcutStatus GetShortcutStatus();

    /// <summary>
    /// Checks whether the NEXUS-3D shortcut exists on the current user's desktop.
    /// </summary>
    bool CheckShortcutExists();

    /// <summary>
    /// Creates the NEXUS-3D shortcut on the desktop.
    /// </summary>
    /// <param name="overwrite">When true, overwrites any existing shortcut. When false, fails safely if shortcut exists.</param>
    OperationResult CreateShortcut(bool overwrite = false);

    /// <summary>
    /// Recreates or updates the desktop shortcut with the latest application executable and icon paths.
    /// </summary>
    OperationResult RecreateShortcut();

    /// <summary>
    /// Removes the NEXUS-3D shortcut from the desktop if it exists.
    /// </summary>
    OperationResult RemoveShortcut();

    /// <summary>
    /// Opens the application installation folder in Windows Explorer.
    /// </summary>
    OperationResult OpenInstallationFolder();

    /// <summary>
    /// Automatically ensures the desktop shortcut exists if enabled in user preferences.
    /// Skips creation in temporary development build directories (bin\Debug, bin\Release).
    /// </summary>
    OperationResult EnsureShortcutOnStartup(bool autoCreateRequested);
}

