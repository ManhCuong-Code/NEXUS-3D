using Microsoft.Extensions.Logging;
using NEXUS3D.Application.Interfaces;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Shared.Guards;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Application.UseCases;

/// <summary>
/// Singleton workspace state coordinator maintaining the active 3D model across all application modules.
/// </summary>
public sealed class WorkspaceSceneSession : IWorkspaceSceneSession
{
    private readonly IModelLoadingService _modelLoadingService;
    private readonly ILogger<WorkspaceSceneSession> _logger;
    private readonly object _syncRoot = new();

    public Scene3D? ActiveScene { get; private set; }
    public string? ActiveFilePath { get; private set; }
    public IFormatDescriptor? ActiveFormat { get; private set; }

    public bool HasActiveScene => ActiveScene is not null;

    public event EventHandler<SceneSessionChangedEventArgs>? SceneChanged;

    public WorkspaceSceneSession(
        IModelLoadingService modelLoadingService,
        ILogger<WorkspaceSceneSession> logger)
    {
        _modelLoadingService = Guard.NotNull(modelLoadingService);
        _logger = Guard.NotNull(logger);
    }

    public async Task<OperationResult<Scene3D>> LoadFileIntoSessionAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var result = await _modelLoadingService.LoadModelAsync(filePath, cancellationToken);
        if (result.IsFailure || result.Value is null)
        {
            return result;
        }

        var format = _modelLoadingService.DetectFormat(filePath);
        SetActiveScene(result.Value, filePath, format);
        return result;
    }

    public void SetActiveScene(Scene3D scene, string? filePath = null, IFormatDescriptor? format = null)
    {
        ArgumentNullException.ThrowIfNull(scene);

        lock (_syncRoot)
        {
            ActiveScene = scene;
            ActiveFilePath = filePath;
            ActiveFormat = format ?? (filePath is not null ? _modelLoadingService.DetectFormat(filePath) : null);
        }

        _logger.LogInformation("Active workspace scene updated to '{Name}' (Source: {Path})",
            scene.Name, filePath ?? "In-Memory");

        SceneChanged?.Invoke(this, new SceneSessionChangedEventArgs(ActiveScene, ActiveFilePath, ActiveFormat));
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            ActiveScene = null;
            ActiveFilePath = null;
            ActiveFormat = null;
        }

        _logger.LogInformation("Active workspace scene cleared.");
        SceneChanged?.Invoke(this, new SceneSessionChangedEventArgs(null, null, null));
    }
}

