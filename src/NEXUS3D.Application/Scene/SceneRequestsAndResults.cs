using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Coordinates;
using NEXUS3D.Domain.Metadata;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Validation;

namespace NEXUS3D.Application.Scene;

/// <summary>
/// Request payload for creating a new <see cref="Scene3D"/>.
/// </summary>
public sealed record CreateSceneRequest(
    string Name,
    CoordinateSystem? CoordinateSystem = null,
    SourceMetadata? SourceMetadata = null,
    SceneSettings? Settings = null,
    string Author = "",
    string Description = "");

/// <summary>
/// Request payload for cloning an existing <see cref="Scene3D"/>.
/// </summary>
public sealed record CloneSceneRequest(
    Scene3D SourceScene,
    string? NewSceneName = null,
    bool PreserveIds = true,
    bool ValidateAfterClone = true);

/// <summary>
/// Request payload for validating a <see cref="Scene3D"/>.
/// </summary>
public sealed record ValidateSceneRequest(
    Scene3D Scene,
    bool FailOnWarnings = false);

/// <summary>
/// Standardized result wrapper for USM scene operations.
/// </summary>
public sealed record SceneOperationResult
{
    public bool IsSuccess { get; init; }
    public Scene3D? Scene { get; init; }
    public SceneStatistics? Statistics { get; init; }
    public ValidationResult Validation { get; init; } = ValidationResult.Success();
    public Error? Error { get; init; }

    public static SceneOperationResult Success(Scene3D scene, ValidationResult? validation = null) =>
        new()
        {
            IsSuccess = true,
            Scene = scene,
            Statistics = scene.CalculateStatistics(),
            Validation = validation ?? ValidationResult.Success()
        };

    public static SceneOperationResult Failure(Error error, ValidationResult? validation = null) =>
        new()
        {
            IsSuccess = false,
            Error = error,
            Validation = validation ?? ValidationResult.Success()
        };
}
