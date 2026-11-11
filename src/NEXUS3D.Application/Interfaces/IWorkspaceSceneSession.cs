using NEXUS3D.Domain.Scene;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Application.Interfaces;

/// <summary>
/// Event arguments for workspace scene changes.
/// </summary>
public sealed class SceneSessionChangedEventArgs : EventArgs
{
    public Scene3D? Scene { get; }
    public string? FilePath { get; }
    public IFormatDescriptor? Format { get; }

    public SceneSessionChangedEventArgs(Scene3D? scene, string? filePath, IFormatDescriptor? format)
    {
        Scene = scene;
        FilePath = filePath;
        Format = format;
    }
}

/// <summary>
/// Central shared session holding the active 3D model across Viewport, Voxelization, Converter, and Inspector workspaces.
/// </summary>
public interface IWorkspaceSceneSession
{
    /// <summary>
    /// Currently loaded Universal Scene Model (USM).
    /// </summary>
    Scene3D? ActiveScene { get; }

    /// <summary>
    /// File path of the currently loaded model, if loaded from disk.
    /// </summary>
    string? ActiveFilePath { get; }

    /// <summary>
    /// Detected format descriptor of the active document.
    /// </summary>
    IFormatDescriptor? ActiveFormat { get; }

    /// <summary>
    /// Whether an active scene model is loaded.
    /// </summary>
    bool HasActiveScene { get; }

    /// <summary>
    /// Fired whenever the active scene is loaded, modified, or cleared.
    /// </summary>
    event EventHandler<SceneSessionChangedEventArgs>? SceneChanged;

    /// <summary>
    /// Loads a model from disk into the active session, notifying all listening workspaces.
    /// </summary>
    Task<OperationResult<Scene3D>> LoadFileIntoSessionAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets an in-memory scene as the active workspace model (e.g. from conversion or voxelization output).
    /// </summary>
    void SetActiveScene(Scene3D scene, string? filePath = null, IFormatDescriptor? format = null);

    /// <summary>
    /// Clears the active workspace scene.
    /// </summary>
    void Clear();
}

