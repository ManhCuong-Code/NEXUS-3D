using NEXUS3D.Formats.Schematic.Nbt.Tags;

namespace NEXUS3D.Formats.Schematic.Common;

/// <summary>
/// Captures Sponge/WorldEdit schematic metadata (Name, Author, Date, RequiredMods, WEOffset) and preserves unknown tags.
/// </summary>
public sealed class SchematicMetadata
{
    public string? Name { get; set; }
    public string? Author { get; set; }
    public long? DateUnixMilliseconds { get; set; }
    public List<string> RequiredMods { get; set; } = [];
    public SchematicOffset? WorldEditOffset { get; set; }
    public SchematicOffset? WorldEditOrigin { get; set; }
    public string? LegacyMaterialsType { get; set; }
    public Dictionary<string, NbtTag> UnmappedTags { get; set; } = new(StringComparer.Ordinal);

    public SchematicMetadata DeepClone()
    {
        var clone = new SchematicMetadata
        {
            Name = Name,
            Author = Author,
            DateUnixMilliseconds = DateUnixMilliseconds,
            RequiredMods = RequiredMods.ToList(),
            WorldEditOffset = WorldEditOffset,
            WorldEditOrigin = WorldEditOrigin,
            LegacyMaterialsType = LegacyMaterialsType
        };

        foreach (var kvp in UnmappedTags)
        {
            clone.UnmappedTags[kvp.Key] = kvp.Value.DeepClone();
        }

        return clone;
    }
}
