using NEXUS3D.Application.Serialization;
using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Coordinates;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Validation;

namespace NEXUS3D.Application.Scene;

/// <summary>
/// Application service orchestrating Universal Scene Model (USM) lifecycle, validation, cloning,
/// coordinate conversion, snapshots, and persistence.
/// </summary>
public interface ISceneService
{
    SceneOperationResult CreateScene(CreateSceneRequest request);
    SceneOperationResult CloneScene(CloneSceneRequest request);
    ValidationResult ValidateScene(ValidateSceneRequest request);
    SceneOperationResult ConvertCoordinateSystem(Scene3D scene, CoordinateSystem targetSystem);
    Result<SceneSnapshot> CreateSnapshot(Scene3D scene, string? label = null);
    SceneOperationResult RestoreSnapshot(SceneSnapshot snapshot);
    SceneOperationResult MergeScenes(Scene3D primaryScene, Scene3D secondaryScene, string? containerNodeName = null);

    Task<SceneSerializationResult> SaveSceneToFileAsync(
        Scene3D scene,
        string filePath,
        SceneSerializationOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<SceneSerializationResult> LoadSceneFromFileAsync(
        string filePath,
        SceneSerializationOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// In-memory or persistent repository for tracking active USM scenes during a workspace session.
/// </summary>
public interface ISceneRepository
{
    void Save(Scene3D scene);
    Scene3D? GetById(Guid sceneId);
    IReadOnlyList<Scene3D> GetAll();
    bool Remove(Guid sceneId);
    void Clear();
}
