using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NEXUS3D.Application.Scene;
using NEXUS3D.Application.Serialization;
using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Materials;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Voxels;

namespace NEXUS3D.Infrastructure.Serialization.Json;

/// <summary>
/// Default schema migrator supporting USM v1 schema compatibility.
/// </summary>
public sealed class DefaultSceneSchemaMigrator : ISceneSchemaMigrator
{
    public int CurrentSchemaVersion => Scene3D.CurrentSchemaVersion;

    public bool CanMigrate(int sourceVersion, int targetVersion) =>
        sourceVersion == targetVersion && targetVersion == Scene3D.CurrentSchemaVersion;

    public Result<string> MigrateJson(string rawJson, int sourceVersion, int targetVersion)
    {
        if (!CanMigrate(sourceVersion, targetVersion))
        {
            return Result<string>.Failure(
                UsmErrorCodes.UnsupportedSchemaVersion,
                $"Cannot migrate USM schema from v{sourceVersion} to v{targetVersion}.");
        }

        return Result<string>.Success(rawJson);
    }
}

/// <summary>
/// Thread-safe in-memory repository for active <see cref="Scene3D"/> instances.
/// </summary>
public sealed class InMemorySceneRepository : ISceneRepository
{
    private readonly ConcurrentDictionary<Guid, Scene3D> _scenes = new();

    public void Save(Scene3D scene)
    {
        DomainGuard.NotNull(scene);
        _scenes[scene.Id] = scene;
    }

    public Scene3D? GetById(Guid sceneId) =>
        _scenes.TryGetValue(sceneId, out var scene) ? scene : null;

    public IReadOnlyList<Scene3D> GetAll() =>
        _scenes.Values.ToList();

    public bool Remove(Guid sceneId) =>
        _scenes.TryRemove(sceneId, out _);

    public void Clear() => _scenes.Clear();
}

/// <summary>
/// Deterministic, round-trip safe JSON serializer for <see cref="Scene3D"/> implementing <see cref="ISceneSerializer"/>.
/// Enforces schema versioning, maximum graph/JSON depth, and payload size guards.
/// </summary>
public sealed class SceneJsonSerializer : ISceneSerializer
{
    private readonly ISceneSchemaMigrator _schemaMigrator;

    public SceneJsonSerializer(ISceneSchemaMigrator? schemaMigrator = null)
    {
        _schemaMigrator = schemaMigrator ?? new DefaultSceneSchemaMigrator();
    }

    public int SupportedSchemaVersion => Scene3D.CurrentSchemaVersion;

    public SceneSerializationResult SerializeToString(Scene3D scene, SceneSerializationOptions? options = null)
    {
        DomainGuard.NotNull(scene);
        var effectiveOptions = options ?? SceneSerializationOptions.Default;

        int graphDepth = scene.Graph.ComputeMaxDepth();
        if (graphDepth > effectiveOptions.MaxDepth)
        {
            return SceneSerializationResult.Failure(
                new Error(
                    UsmErrorCodes.DataLimitExceeded,
                    $"Scene graph depth ({graphDepth}) exceeds maximum allowed serialization depth ({effectiveOptions.MaxDepth})."));
        }

        var envelope = MapSceneToEnvelope(scene, effectiveOptions);
        var jsonOptions = CreateJsonOptions(effectiveOptions);

        string json = JsonSerializer.Serialize(envelope, jsonOptions);
        long byteCount = Encoding.UTF8.GetByteCount(json);

        if (byteCount > effectiveOptions.MaxPayloadBytes)
        {
            return SceneSerializationResult.Failure(
                new Error(
                    UsmErrorCodes.DataLimitExceeded,
                    $"Serialized payload size ({byteCount} bytes) exceeds maximum allowed limit ({effectiveOptions.MaxPayloadBytes} bytes)."));
        }

        return SceneSerializationResult.SuccessSerialize(json, byteCount, SupportedSchemaVersion, scene.Metadata.Warnings);
    }

    public async Task<SceneSerializationResult> SerializeToStreamAsync(
        Scene3D scene,
        Stream outputStream,
        SceneSerializationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        DomainGuard.NotNull(scene);
        DomainGuard.NotNull(outputStream);

        var stringResult = SerializeToString(scene, options);
        if (!stringResult.IsSuccess || stringResult.SerializedJson is null)
        {
            return stringResult;
        }

        byte[] utf8Bytes = Encoding.UTF8.GetBytes(stringResult.SerializedJson);
        await outputStream.WriteAsync(utf8Bytes, cancellationToken).ConfigureAwait(false);
        await outputStream.FlushAsync(cancellationToken).ConfigureAwait(false);

        return stringResult;
    }

    public SceneSerializationResult DeserializeFromString(string json, SceneSerializationOptions? options = null)
    {
        var effectiveOptions = options ?? SceneSerializationOptions.Default;

        if (string.IsNullOrWhiteSpace(json))
        {
            return SceneSerializationResult.Failure(
                new Error(UsmErrorCodes.SerializationFailed, "JSON payload cannot be null or empty."));
        }

        long byteCount = Encoding.UTF8.GetByteCount(json);
        if (byteCount > effectiveOptions.MaxPayloadBytes)
        {
            return SceneSerializationResult.Failure(
                new Error(
                    UsmErrorCodes.DataLimitExceeded,
                    $"Input JSON payload size ({byteCount} bytes) exceeds maximum allowed limit ({effectiveOptions.MaxPayloadBytes} bytes)."));
        }

        UsmSceneEnvelopeDto? envelope;
        try
        {
            var jsonOptions = CreateJsonOptions(effectiveOptions);
            envelope = JsonSerializer.Deserialize<UsmSceneEnvelopeDto>(json, jsonOptions);
        }
        catch (JsonException ex)
        {
            if (ex.Message.Contains("depth", StringComparison.OrdinalIgnoreCase))
            {
                return SceneSerializationResult.Failure(
                    new Error(
                        UsmErrorCodes.DataLimitExceeded,
                        $"JSON nesting depth exceeded maximum allowed limit ({effectiveOptions.MaxDepth}): {ex.Message}"));
            }

            return SceneSerializationResult.Failure(
                new Error(UsmErrorCodes.SerializationFailed, $"Malformed USM JSON payload: {ex.Message}"));
        }

        if (envelope is null || !string.Equals(envelope.FormatIdentifier, "NEXUS3D.USM", StringComparison.Ordinal))
        {
            return SceneSerializationResult.Failure(
                new Error(UsmErrorCodes.SerializationFailed, "Invalid or missing NEXUS3D.USM format identifier."));
        }

        if (envelope.SchemaVersion <= 0)
        {
            return SceneSerializationResult.Failure(
                new Error(UsmErrorCodes.UnsupportedSchemaVersion, $"Invalid USM schema version '{envelope.SchemaVersion}'."));
        }

        if (envelope.SchemaVersion != SupportedSchemaVersion)
        {
            if (effectiveOptions.StrictSchemaValidation && !_schemaMigrator.CanMigrate(envelope.SchemaVersion, SupportedSchemaVersion))
            {
                return SceneSerializationResult.Failure(
                    new Error(
                        UsmErrorCodes.UnsupportedSchemaVersion,
                        $"USM schema version '{envelope.SchemaVersion}' is not supported by serializer version '{SupportedSchemaVersion}'."));
            }
        }

        try
        {
            var scene = MapEnvelopeToScene(envelope);
            int graphDepth = scene.Graph.ComputeMaxDepth();
            if (graphDepth > effectiveOptions.MaxDepth)
            {
                return SceneSerializationResult.Failure(
                    new Error(
                        UsmErrorCodes.DataLimitExceeded,
                        $"Reconstructed scene graph depth ({graphDepth}) exceeds maximum allowed depth ({effectiveOptions.MaxDepth})."));
            }

            return SceneSerializationResult.SuccessDeserialize(scene, byteCount, envelope.SchemaVersion, scene.Metadata.Warnings);
        }
        catch (Exception ex) when (ex is DomainException or InvalidOperationException or ArgumentException)
        {
            return SceneSerializationResult.Failure(
                new Error(UsmErrorCodes.SerializationFailed, $"Failed to reconstruct USM Scene from JSON: {ex.Message}"));
        }
    }

    public async Task<SceneSerializationResult> DeserializeFromStreamAsync(
        Stream inputStream,
        SceneSerializationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        DomainGuard.NotNull(inputStream);
        var effectiveOptions = options ?? SceneSerializationOptions.Default;

        if (inputStream.CanSeek && inputStream.Length > effectiveOptions.MaxPayloadBytes)
        {
            return SceneSerializationResult.Failure(
                new Error(
                    UsmErrorCodes.DataLimitExceeded,
                    $"Input stream length ({inputStream.Length} bytes) exceeds maximum allowed limit ({effectiveOptions.MaxPayloadBytes} bytes)."));
        }

        using var reader = new StreamReader(inputStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 65536, leaveOpen: true);
        string json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        return DeserializeFromString(json, effectiveOptions);
    }

    private static JsonSerializerOptions CreateJsonOptions(SceneSerializationOptions options)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = options.WriteIndented,
            MaxDepth = Math.Max(16, options.MaxDepth),
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
        };
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        return jsonOptions;
    }

    private static UsmSceneEnvelopeDto MapSceneToEnvelope(Scene3D scene, SceneSerializationOptions options)
    {
        var metadataClone = scene.Metadata.DeepClone();
        if (!options.IncludeRawData)
        {
            metadataClone.PreservedRawData.Clear();
        }

        var envelope = new UsmSceneEnvelopeDto
        {
            FormatIdentifier = "NEXUS3D.USM",
            SchemaVersion = scene.SchemaVersion,
            SceneId = scene.Id,
            Name = scene.Name,
            CreatedAt = scene.CreatedAt,
            ModifiedAt = scene.ModifiedAt,
            Metadata = metadataClone,
            Settings = scene.Settings.DeepClone(),
            Coordinates = scene.Coordinates.DeepClone(),
            RootNodeId = scene.Graph.Root.Id,
            Materials = scene.Materials.Materials.Select(m => m.DeepClone()).ToList(),
            Textures = scene.Textures.Select(t => t.DeepClone()).ToList(),
            Skeletons = scene.Skeletons.Select(s => s.DeepClone()).ToList(),
            Animations = scene.Animations.Select(a => a.DeepClone()).ToList()
        };

        foreach (var node in scene.Graph.Traverse())
        {
            envelope.Nodes.Add(new UsmSceneNodeDto
            {
                Id = node.Id,
                ParentId = node.ParentId,
                Name = node.Name,
                LocalTransform = node.LocalTransform.DeepClone(),
                IsVisible = node.IsVisible,
                IsLocked = node.IsLocked,
                ChildIds = node.Children.Select(c => c.Id).ToList(),
                Object = node.Object is not null ? MapSceneObjectToDto(node.Object) : null
            });
        }

        return envelope;
    }

    private static UsmSceneObjectDto MapSceneObjectToDto(SceneObject obj)
    {
        var dto = new UsmSceneObjectDto
        {
            Id = obj.Id,
            Name = obj.Name,
            SkeletonId = obj.SkeletonId
        };

        foreach (var kvp in obj.Properties)
        {
            dto.Properties[kvp.Key] = kvp.Value.Clone();
        }

        foreach (var kvp in obj.Extensions)
        {
            dto.Extensions[kvp.Key] = kvp.Value.DeepClone();
        }

        foreach (var mesh in obj.Meshes)
        {
            dto.Meshes.Add(new UsmMeshDataDto
            {
                Id = mesh.Id,
                Name = mesh.Name,
                Topology = mesh.Topology,
                Vertices = mesh.Vertices.Select(v => v.DeepClone()).ToList(),
                Faces = mesh.Faces.Select(f => new UsmFaceDto
                {
                    VertexIndices = f.VertexIndices.ToArray(),
                    MaterialId = f.MaterialId,
                    SmoothingGroup = f.SmoothingGroup
                }).ToList(),
                Submeshes = mesh.Submeshes.Select(s => s.DeepClone()).ToList()
            });
        }

        foreach (var grid in obj.VoxelGrids)
        {
            dto.VoxelGrids.Add(new UsmVoxelGridDto
            {
                Id = grid.Id,
                Name = grid.Name,
                Origin = grid.Origin,
                VoxelSize = grid.VoxelSize,
                Bounds = grid.Bounds,
                PaletteEntries = grid.Palette.Entries.Select(e => e.DeepClone()).ToList(),
                Voxels = grid.Voxels.Select(v => v.DeepClone()).ToList(),
                Layers = grid.Layers.Select(l => l.DeepClone()).ToList()
            });
        }

        return dto;
    }

    private static Scene3D MapEnvelopeToScene(UsmSceneEnvelopeDto envelope)
    {
        if (envelope.Nodes.Count == 0)
        {
            throw new InvalidOperationException("USM JSON envelope must contain at least the root SceneNode.");
        }

        var nodeMap = envelope.Nodes.ToDictionary(n => n.Id);
        if (!nodeMap.TryGetValue(envelope.RootNodeId, out var rootDto))
        {
            throw new InvalidOperationException($"Root node '{envelope.RootNodeId}' was not found in the serialized node list.");
        }

        var rootNode = CreateSceneNodeFromDto(rootDto);
        var graph = new SceneGraph(rootNode);

        // Iteratively attach children in breadth/depth order
        var queue = new Queue<(SceneNode ParentNode, UsmSceneNodeDto ParentDto)>();
        queue.Enqueue((rootNode, rootDto));

        while (queue.Count > 0)
        {
            var (parentNode, parentDto) = queue.Dequeue();
            foreach (var childId in parentDto.ChildIds)
            {
                if (!nodeMap.TryGetValue(childId, out var childDto))
                {
                    throw new InvalidOperationException($"Child node '{childId}' referenced by '{parentDto.Id}' was not found.");
                }

                var childNode = CreateSceneNodeFromDto(childDto);
                if (!graph.AddNode(parentNode, childNode))
                {
                    throw new InvalidOperationException($"Failed to attach child node '{childDto.Id}' to parent '{parentDto.Id}'.");
                }
                queue.Enqueue((childNode, childDto));
            }
        }

        var materials = new MaterialLibrary();
        foreach (var mat in envelope.Materials)
        {
            materials.Add(mat.DeepClone());
        }

        return new Scene3D
        {
            Id = envelope.SceneId,
            Name = envelope.Name,
            SchemaVersion = envelope.SchemaVersion,
            CreatedAt = envelope.CreatedAt,
            ModifiedAt = envelope.ModifiedAt,
            Metadata = envelope.Metadata.DeepClone(),
            Settings = envelope.Settings.DeepClone(),
            Coordinates = envelope.Coordinates.DeepClone(),
            Graph = graph,
            Materials = materials,
            Textures = envelope.Textures.Select(t => t.DeepClone()).ToList(),
            Skeletons = envelope.Skeletons.Select(s => s.DeepClone()).ToList(),
            Animations = envelope.Animations.Select(a => a.DeepClone()).ToList()
        };
    }

    private static SceneNode CreateSceneNodeFromDto(UsmSceneNodeDto dto)
    {
        SceneObject? obj = dto.Object is not null ? CreateSceneObjectFromDto(dto.Object) : null;
        return new SceneNode(dto.Name, obj)
        {
            Id = dto.Id,
            LocalTransform = dto.LocalTransform.DeepClone(),
            IsVisible = dto.IsVisible,
            IsLocked = dto.IsLocked
        };
    }

    private static SceneObject CreateSceneObjectFromDto(UsmSceneObjectDto dto)
    {
        var obj = new SceneObject(dto.Name)
        {
            Id = dto.Id,
            SkeletonId = dto.SkeletonId
        };

        foreach (var kvp in dto.Properties)
        {
            obj.Properties[kvp.Key] = kvp.Value.Clone();
        }

        foreach (var kvp in dto.Extensions)
        {
            obj.Extensions[kvp.Key] = kvp.Value.DeepClone();
        }

        foreach (var meshDto in dto.Meshes)
        {
            var mesh = new MeshData(meshDto.Name, meshDto.Topology)
            {
                Id = meshDto.Id,
                Vertices = meshDto.Vertices.Select(v => v.DeepClone()).ToList(),
                Faces = meshDto.Faces.Select(f => new Face(f.VertexIndices, f.MaterialId, f.SmoothingGroup)).ToList(),
                Submeshes = meshDto.Submeshes.Select(s => s.DeepClone()).ToList()
            };
            obj.Meshes.Add(mesh);
        }

        foreach (var gridDto in dto.VoxelGrids)
        {
            var palette = new VoxelPalette();
            foreach (var entry in gridDto.PaletteEntries.OrderBy(e => e.Index))
            {
                palette.Add(entry.DeepClone());
            }

            var grid = new VoxelGrid(gridDto.Name, new SparseVoxelStorageBackend(gridDto.Voxels.Count))
            {
                Id = gridDto.Id,
                Origin = gridDto.Origin,
                VoxelSize = gridDto.VoxelSize,
                Bounds = gridDto.Bounds,
                Palette = palette,
                Layers = gridDto.Layers.Select(l => l.DeepClone()).ToList()
            };

            foreach (var v in gridDto.Voxels)
            {
                grid.SetVoxel(v.DeepClone());
            }

            obj.VoxelGrids.Add(grid);
        }

        return obj;
    }
}
