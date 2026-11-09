using Microsoft.Extensions.Logging;
using NEXUS3D.Application.Serialization;
using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Coordinates;
using NEXUS3D.Domain.Factories;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Metadata;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Validation;

namespace NEXUS3D.Application.Scene;

/// <summary>
/// Default implementation of <see cref="ISceneService"/> managing USM creation, validation,
/// deep cloning, coordinate system transformation, snapshots, and file serialization.
/// </summary>
public sealed class SceneService : ISceneService
{
    private readonly SceneFactory _sceneFactory;
    private readonly SceneNodeFactory _nodeFactory;
    private readonly IValidator<Scene3D> _sceneValidator;
    private readonly ICoordinateConverter _coordinateConverter;
    private readonly ISceneSerializer _sceneSerializer;
    private readonly ISceneRepository _sceneRepository;
    private readonly ILogger<SceneService> _logger;

    public SceneService(
        SceneFactory sceneFactory,
        SceneNodeFactory nodeFactory,
        IValidator<Scene3D> sceneValidator,
        ICoordinateConverter coordinateConverter,
        ISceneSerializer sceneSerializer,
        ISceneRepository sceneRepository,
        ILogger<SceneService> logger)
    {
        _sceneFactory = DomainGuard.NotNull(sceneFactory);
        _nodeFactory = DomainGuard.NotNull(nodeFactory);
        _sceneValidator = DomainGuard.NotNull(sceneValidator);
        _coordinateConverter = DomainGuard.NotNull(coordinateConverter);
        _sceneSerializer = DomainGuard.NotNull(sceneSerializer);
        _sceneRepository = DomainGuard.NotNull(sceneRepository);
        _logger = DomainGuard.NotNull(logger);
    }

    public SceneOperationResult CreateScene(CreateSceneRequest request)
    {
        DomainGuard.NotNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return SceneOperationResult.Failure(
                new Error(UsmErrorCodes.InvalidScene, "Scene name cannot be null or whitespace."));
        }

        var coordinateSystem = request.CoordinateSystem ?? CoordinateSystem.DefaultUsm;
        var coordMeta = new CoordinateMetadata
        {
            SourceSystem = coordinateSystem.DeepClone(),
            CurrentSystem = coordinateSystem.DeepClone(),
            OriginalUnit = coordinateSystem.Unit,
            CurrentUnit = coordinateSystem.Unit
        };

        var scene = _sceneFactory.Create(request.Name.Trim(), coordMeta);
        scene.Metadata.Author = request.Author;
        scene.Metadata.Description = request.Description;
        if (request.SourceMetadata is not null)
        {
            scene.Metadata.Source = request.SourceMetadata.DeepClone();
        }
        if (request.Settings is not null)
        {
            scene.Settings = request.Settings.DeepClone();
        }

        var validation = _sceneValidator.Validate(scene);
        if (!validation.IsValid)
        {
            return SceneOperationResult.Failure(
                new Error(UsmErrorCodes.InvalidScene, "Created scene failed initial validation."),
                validation);
        }

        _sceneRepository.Save(scene);
        _logger.LogInformation("Created new USM Scene '{SceneName}' ({SceneId}).", scene.Name, scene.Id);

        return SceneOperationResult.Success(scene, validation);
    }

    public SceneOperationResult CloneScene(CloneSceneRequest request)
    {
        DomainGuard.NotNull(request);
        DomainGuard.NotNull(request.SourceScene);

        var cloned = request.SourceScene.DeepClone(preserveIds: request.PreserveIds);
        if (!string.IsNullOrWhiteSpace(request.NewSceneName))
        {
            cloned.Name = request.NewSceneName.Trim();
            cloned.Metadata.Title = cloned.Name;
            cloned.UpdateModifiedTime();
        }

        var validation = ValidationResult.Success();
        if (request.ValidateAfterClone)
        {
            validation = _sceneValidator.Validate(cloned);
            if (!validation.IsValid)
            {
                return SceneOperationResult.Failure(
                    new Error(UsmErrorCodes.InvalidScene, "Cloned scene failed validation."),
                    validation);
            }
        }

        _sceneRepository.Save(cloned);
        _logger.LogInformation("Cloned USM Scene '{SceneName}' ({SceneId}).", cloned.Name, cloned.Id);
        return SceneOperationResult.Success(cloned, validation);
    }

    public ValidationResult ValidateScene(ValidateSceneRequest request)
    {
        DomainGuard.NotNull(request);
        DomainGuard.NotNull(request.Scene);

        var result = _sceneValidator.Validate(request.Scene);
        if (request.FailOnWarnings && result.HasWarnings)
        {
            var escalated = result.Issues.Select(issue =>
                issue.Severity == ValidationSeverity.Warning
                    ? new ValidationIssue(issue.Code, issue.Message, ValidationSeverity.Error, issue.PropertyPath, issue.ObjectId)
                    : issue);
            return ValidationResult.Failure(escalated);
        }

        return result;
    }

    public SceneOperationResult ConvertCoordinateSystem(Scene3D scene, CoordinateSystem targetSystem)
    {
        DomainGuard.NotNull(scene);
        DomainGuard.NotNull(targetSystem);

        if (!targetSystem.IsValid)
        {
            return SceneOperationResult.Failure(
                new Error(UsmErrorCodes.InvalidCoordinateSystem, "Target coordinate system does not have an orthogonal basis."));
        }

        var sourceSystem = scene.Coordinates.CurrentSystem;
        if (!sourceSystem.IsValid)
        {
            return SceneOperationResult.Failure(
                new Error(UsmErrorCodes.InvalidCoordinateSystem, "Source coordinate system does not have an orthogonal basis."));
        }

        var convertedScene = scene.DeepClone(preserveIds: true);
        Matrix4x4D conversionMatrix = _coordinateConverter.CreateConversionMatrix(sourceSystem, targetSystem);
        bool flipWinding = _coordinateConverter.RequiresWindingFlip(sourceSystem, targetSystem);

        foreach (var node in convertedScene.Graph.Traverse())
        {
            if (node.Id != convertedScene.Graph.Root.Id)
            {
                node.LocalTransform.Position = _coordinateConverter.ConvertPosition(
                    node.LocalTransform.Position,
                    sourceSystem,
                    targetSystem);
            }

            if (node.Object is { } sceneObj)
            {
                for (int m = 0; m < sceneObj.Meshes.Count; m++)
                {
                    sceneObj.Meshes[m] = ConvertMeshCoordinates(sceneObj.Meshes[m], sourceSystem, targetSystem, flipWinding);
                }
            }
        }

        convertedScene.Coordinates.CurrentSystem = targetSystem.DeepClone();
        convertedScene.Coordinates.CurrentUnit = targetSystem.Unit;
        convertedScene.Coordinates.AppliedConversionMatrix = conversionMatrix * convertedScene.Coordinates.AppliedConversionMatrix;
        convertedScene.Coordinates.ScaleFactor *= AxisMapping.GetUnitScaleFactor(sourceSystem.Unit, targetSystem.Unit);
        convertedScene.Metadata.ProvenanceHistory.Add(new DataProvenance(
            DataOriginKind.Converted,
            nameof(SceneService),
            nameof(ConvertCoordinateSystem),
            $"Converted coordinate system (WindingFlip={flipWinding})."));
        convertedScene.UpdateModifiedTime();

        var validation = _sceneValidator.Validate(convertedScene);
        return SceneOperationResult.Success(convertedScene, validation);
    }

    public Result<SceneSnapshot> CreateSnapshot(Scene3D scene, string? label = null)
    {
        DomainGuard.NotNull(scene);
        return Result<SceneSnapshot>.Success(scene.CreateSnapshot(label));
    }

    public SceneOperationResult RestoreSnapshot(SceneSnapshot snapshot)
    {
        DomainGuard.NotNull(snapshot);
        var restored = snapshot.Restore();
        var validation = _sceneValidator.Validate(restored);

        if (!validation.IsValid)
        {
            return SceneOperationResult.Failure(
                new Error(UsmErrorCodes.InvalidScene, "Restored snapshot failed validation."),
                validation);
        }

        _sceneRepository.Save(restored);
        return SceneOperationResult.Success(restored, validation);
    }

    public SceneOperationResult MergeScenes(Scene3D primaryScene, Scene3D secondaryScene, string? containerNodeName = null)
    {
        DomainGuard.NotNull(primaryScene);
        DomainGuard.NotNull(secondaryScene);

        var merged = primaryScene.DeepClone(preserveIds: true);
        var secondaryClone = secondaryScene.DeepClone(preserveIds: true);

        foreach (var material in secondaryClone.Materials.Materials)
        {
            merged.Materials.Add(material.DeepClone());
        }

        foreach (var texture in secondaryClone.Textures)
        {
            if (merged.Textures.All(t => t.Id != texture.Id))
            {
                merged.Textures.Add(texture.DeepClone());
            }
        }

        foreach (var skeleton in secondaryClone.Skeletons)
        {
            if (merged.Skeletons.All(s => s.Id != skeleton.Id))
            {
                merged.Skeletons.Add(skeleton.DeepClone());
            }
        }

        foreach (var animation in secondaryClone.Animations)
        {
            if (merged.Animations.All(a => a.Id != animation.Id))
            {
                merged.Animations.Add(animation.DeepClone());
            }
        }

        var containerNode = _nodeFactory.CreateEmptyNode(
            string.IsNullOrWhiteSpace(containerNodeName)
                ? $"Merged_{secondaryClone.Name}"
                : containerNodeName.Trim());

        merged.Graph.AddNode(merged.Graph.Root, containerNode);

        foreach (var child in secondaryClone.Graph.Root.Children)
        {
            var clonedSubtree = CloneSubtreeWithNewIds(child);
            merged.Graph.AddNode(containerNode, clonedSubtree);
        }

        merged.UpdateModifiedTime();
        var validation = _sceneValidator.Validate(merged);
        return SceneOperationResult.Success(merged, validation);
    }

    public async Task<SceneSerializationResult> SaveSceneToFileAsync(
        Scene3D scene,
        string filePath,
        SceneSerializationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        DomainGuard.NotNull(scene);
        DomainGuard.NotNullOrWhiteSpace(filePath);

        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true);
        return await _sceneSerializer.SerializeToStreamAsync(scene, fileStream, options, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SceneSerializationResult> LoadSceneFromFileAsync(
        string filePath,
        SceneSerializationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        DomainGuard.NotNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            return SceneSerializationResult.Failure(
                new Error(UsmErrorCodes.SerializationFailed, $"Scene file '{filePath}' does not exist."));
        }

        await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true);
        var result = await _sceneSerializer.DeserializeFromStreamAsync(fileStream, options, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess && result.Scene is not null)
        {
            _sceneRepository.Save(result.Scene);
        }

        return result;
    }

    private MeshData ConvertMeshCoordinates(
        MeshData mesh,
        CoordinateSystem sourceSystem,
        CoordinateSystem targetSystem,
        bool flipWinding)
    {
        var converted = mesh.DeepClone();

        for (int i = 0; i < converted.Vertices.Count; i++)
        {
            var v = converted.Vertices[i];
            Vector3D pos = _coordinateConverter.ConvertPosition(v.Position, sourceSystem, targetSystem);
            Vector3D? normal = v.Normal.HasValue
                ? _coordinateConverter.ConvertDirection(v.Normal.Value, sourceSystem, targetSystem).Normalize()
                : null;

            var updated = new Vertex(pos, normal, v.TexCoord, v.Color);
            foreach (var attr in v.CustomAttributes)
            {
                updated.CustomAttributes[attr.Key] = attr.Value;
            }
            converted.Vertices[i] = updated;
        }

        if (flipWinding)
        {
            for (int i = 0; i < converted.Faces.Count; i++)
            {
                var f = converted.Faces[i];
                var reversedIndices = f.VertexIndices.Reverse().ToArray();
                converted.Faces[i] = new Face(reversedIndices, f.MaterialId, f.SmoothingGroup);
            }
        }

        return converted;
    }

    private static SceneNode CloneSubtreeWithNewIds(SceneNode root)
    {
        var clonedRoot = new SceneNode(root.Name, root.Object?.DeepClone())
        {
            Id = Guid.NewGuid(),
            LocalTransform = root.LocalTransform.DeepClone(),
            IsVisible = root.IsVisible,
            IsLocked = root.IsLocked
        };

        var stack = new Stack<(SceneNode Source, SceneNode Target)>();
        stack.Push((root, clonedRoot));

        while (stack.Count > 0)
        {
            var (source, target) = stack.Pop();
            foreach (var child in source.Children)
            {
                var clonedChild = new SceneNode(child.Name, child.Object?.DeepClone())
                {
                    Id = Guid.NewGuid(),
                    LocalTransform = child.LocalTransform.DeepClone(),
                    IsVisible = child.IsVisible,
                    IsLocked = child.IsLocked
                };
                target.AddChild(clonedChild);
                stack.Push((child, clonedChild));
            }
        }

        return clonedRoot;
    }
}
