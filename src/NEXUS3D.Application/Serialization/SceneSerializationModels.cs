using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Metadata;
using NEXUS3D.Domain.Scene;

namespace NEXUS3D.Application.Serialization;

/// <summary>
/// Options controlling USM JSON serialization and deserialization behavior, security limits, and fidelity.
/// </summary>
public sealed record SceneSerializationOptions
{
    public const int DefaultMaxDepth = 128;
    public const long DefaultMaxPayloadBytes = 256L * 1024L * 1024L; // 256 MB

    public static SceneSerializationOptions Default { get; } = new();

    public bool WriteIndented { get; init; } = true;
    public int MaxDepth { get; init; } = DefaultMaxDepth;
    public long MaxPayloadBytes { get; init; } = DefaultMaxPayloadBytes;
    public bool IncludeRawData { get; init; } = true;
    public bool StrictSchemaValidation { get; init; } = true;
}

/// <summary>
/// Result of a USM serialization or deserialization operation.
/// </summary>
public sealed record SceneSerializationResult
{
    public bool IsSuccess { get; init; }
    public Scene3D? Scene { get; init; }
    public string? SerializedJson { get; init; }
    public long PayloadByteCount { get; init; }
    public int SchemaVersion { get; init; } = Scene3D.CurrentSchemaVersion;
    public Error? Error { get; init; }
    public IReadOnlyList<WarningRecord> Warnings { get; init; } = [];

    public static SceneSerializationResult SuccessSerialize(
        string json,
        long byteCount,
        int schemaVersion = Scene3D.CurrentSchemaVersion,
        IReadOnlyList<WarningRecord>? warnings = null) =>
        new()
        {
            IsSuccess = true,
            SerializedJson = json,
            PayloadByteCount = byteCount,
            SchemaVersion = schemaVersion,
            Warnings = warnings ?? []
        };

    public static SceneSerializationResult SuccessDeserialize(
        Scene3D scene,
        long byteCount,
        int schemaVersion = Scene3D.CurrentSchemaVersion,
        IReadOnlyList<WarningRecord>? warnings = null) =>
        new()
        {
            IsSuccess = true,
            Scene = scene,
            PayloadByteCount = byteCount,
            SchemaVersion = schemaVersion,
            Warnings = warnings ?? []
        };

    public static SceneSerializationResult Failure(
        Error error,
        IReadOnlyList<WarningRecord>? warnings = null) =>
        new()
        {
            IsSuccess = false,
            Error = error,
            Warnings = warnings ?? []
        };
}

/// <summary>
/// Contract for serializing and deserializing <see cref="Scene3D"/> instances to/from USM JSON format.
/// </summary>
public interface ISceneSerializer
{
    int SupportedSchemaVersion { get; }

    SceneSerializationResult SerializeToString(Scene3D scene, SceneSerializationOptions? options = null);

    Task<SceneSerializationResult> SerializeToStreamAsync(
        Scene3D scene,
        Stream outputStream,
        SceneSerializationOptions? options = null,
        CancellationToken cancellationToken = default);

    SceneSerializationResult DeserializeFromString(string json, SceneSerializationOptions? options = null);

    Task<SceneSerializationResult> DeserializeFromStreamAsync(
        Stream inputStream,
        SceneSerializationOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Contract for migrating older USM JSON schema versions to the current schema version.
/// </summary>
public interface ISceneSchemaMigrator
{
    int CurrentSchemaVersion { get; }
    bool CanMigrate(int sourceVersion, int targetVersion);
    Result<string> MigrateJson(string rawJson, int sourceVersion, int targetVersion);
}
