using NEXUS3D.Domain.Metadata;
using NEXUS3D.Domain.Validation;
using NEXUS3D.Formats.BedrockGeometry.Exporting;
using NEXUS3D.Formats.BedrockGeometry.Models;

namespace NEXUS3D.Formats.BedrockGeometry.Conversion;

/// <summary>
/// Options controlling bidirectional mapping between <see cref="BedrockGeometryDocument"/> and USM <see cref="NEXUS3D.Domain.Scene.Scene3D"/>.
/// </summary>
public sealed record BedrockMappingOptions
{
    public static BedrockMappingOptions Default { get; } = new();

    public bool StrictMode { get; init; }
    public bool PreserveRawData { get; init; } = true;
    public bool GenerateCubeMeshes { get; init; } = true;
    public string? TargetFormatVersion { get; init; }
    public string? SourceFileName { get; init; }
    public long SourceFileSizeBytes { get; init; }
    public BedrockGeometryWriteOptions ExportOptions { get; init; } = BedrockGeometryWriteOptions.Default;
}

/// <summary>
/// Detailed report of a conversion between Minecraft Bedrock Geometry JSON and USM <see cref="NEXUS3D.Domain.Scene.Scene3D"/>.
/// </summary>
public sealed class BedrockConversionReport
{
    public string FormatVersion { get; init; } = "1.12.0";
    public BedrockGeometrySchemaFamily SchemaFamily { get; init; }
    public int GeometryCount { get; init; }
    public int BoneCount { get; init; }
    public int CubeCount { get; init; }
    public int LocatorCount { get; init; }
    public int PolyMeshCount { get; init; }
    public int GeneratedMeshCount { get; init; }
    public int PreservedExtensionCount { get; init; }
    public int ValidCubeCount { get; init; }
    public int ErrorCubeCount { get; init; }
    public int MeshCount { get; init; }
    public int FaceCount { get; init; }
    public int VertexCount { get; init; }
    public int TriangleCount { get; init; }
    public int CubesWithUvCount { get; init; }
    public int BonesWithParentCount { get; init; }
    public LossReport LossReport { get; init; } = new();
    public List<WarningRecord> Warnings { get; init; } = [];
    public List<ValidationIssue> Issues { get; init; } = [];
}
