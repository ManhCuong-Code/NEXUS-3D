using NEXUS3D.Domain.Voxels;
using NEXUS3D.Formats.Schematic.Nbt.Tags;

namespace NEXUS3D.Formats.Schematic.BlockEntities;

/// <summary>
/// Represents a block entity (Chest, Sign, Banner, Command Block, Spawner, or modded tile entity)
/// inside a Sponge or Legacy schematic.
/// </summary>
public sealed class SchematicBlockEntity
{
    public string Id { get; set; } = string.Empty;
    public VoxelPosition Position { get; set; }
    public NbtCompoundTag Data { get; set; } = new();
    public int SourceVersion { get; set; } = 2;
    public NbtCompoundTag? RawOriginalTag { get; set; }

    public SchematicBlockEntity DeepClone() => new()
    {
        Id = Id,
        Position = Position,
        Data = Data.CloneCompound(),
        SourceVersion = SourceVersion,
        RawOriginalTag = RawOriginalTag?.CloneCompound()
    };
}

