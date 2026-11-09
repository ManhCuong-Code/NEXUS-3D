using NEXUS3D.Formats.Json.Abstractions;

namespace NEXUS3D.Formats.VoxelJson.Models;

/// <summary>
/// Descriptive metadata for a parsed <see cref="VoxelDocument"/>.
/// </summary>
public sealed class VoxelDocumentMetadata
{
    public string Name { get; set; } = "Untitled Voxel Model";
    public string? Author { get; set; }
    public string? Description { get; set; }
    public string SchemaVersion { get; set; } = "1.0";
    public string? ParentModel { get; set; }
    public bool? AmbientOcclusion { get; set; }
    public List<string> Tags { get; set; } = [];
    public Dictionary<string, string> CustomMetadata { get; set; } = new(StringComparer.Ordinal);
    public JsonExtensionData ExtensionData { get; set; } = new();

    public VoxelDocumentMetadata DeepClone() => new()
    {
        Name = Name,
        Author = Author,
        Description = Description,
        SchemaVersion = SchemaVersion,
        ParentModel = ParentModel,
        AmbientOcclusion = AmbientOcclusion,
        Tags = [.. Tags],
        CustomMetadata = new Dictionary<string, string>(CustomMetadata, StringComparer.Ordinal),
        ExtensionData = ExtensionData.DeepClone()
    };
}
