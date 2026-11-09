using NEXUS3D.Formats.Schematic.Common;
using NEXUS3D.Formats.Schematic.Nbt.Tags;
using NEXUS3D.Formats.Schematic.Nbt.Utilities;

namespace NEXUS3D.Formats.Schematic.BlockEntities;

/// <summary>
/// Serializes <see cref="SchematicBlockEntity"/> instances into Sponge v2, Sponge v3, or Legacy <c>TileEntities</c> NBT format,
/// preserving all custom/unedited NBT tags.
/// </summary>
public sealed class BlockEntityWriter
{
    public NbtListTag WriteSpongeV2BlockEntities(
        IEnumerable<SchematicBlockEntity> entities,
        SchematicDimensions dimensions,
        string tagName = "BlockEntities")
    {
        var list = new NbtListTag(tagName, NbtTagType.Compound);
        foreach (var entity in entities)
        {
            ValidateEntityForWrite(entity, dimensions);
            var compound = new NbtCompoundTag(string.Empty);
            compound.PutString("Id", entity.Id);
            compound.PutIntArray("Pos", [entity.Position.X, entity.Position.Y, entity.Position.Z]);

            foreach (var tag in entity.Data.GetAllTags())
            {
                if (tag.Name is not ("Id" or "id" or "Pos" or "x" or "y" or "z"))
                {
                    compound.SetTag(tag.DeepClone());
                }
            }

            list.Add(compound);
        }

        return list;
    }

    public NbtListTag WriteSpongeV3BlockEntities(
        IEnumerable<SchematicBlockEntity> entities,
        SchematicDimensions dimensions,
        string tagName = "BlockEntities")
    {
        var list = new NbtListTag(tagName, NbtTagType.Compound);
        foreach (var entity in entities)
        {
            ValidateEntityForWrite(entity, dimensions);
            var compound = new NbtCompoundTag(string.Empty);
            compound.PutString("Id", entity.Id);
            compound.PutIntArray("Pos", [entity.Position.X, entity.Position.Y, entity.Position.Z]);

            if (entity.Data.Count > 0)
            {
                var dataClone = entity.Data.CloneCompound();
                dataClone.Name = "Data";
                compound.SetTag(dataClone);
            }

            list.Add(compound);
        }

        return list;
    }

    public NbtListTag WriteLegacyTileEntities(
        IEnumerable<SchematicBlockEntity> entities,
        SchematicDimensions dimensions,
        string tagName = "TileEntities")
    {
        var list = new NbtListTag(tagName, NbtTagType.Compound);
        foreach (var entity in entities)
        {
            ValidateEntityForWrite(entity, dimensions);
            var compound = new NbtCompoundTag(string.Empty);
            compound.PutString("id", entity.Id);
            compound.PutInt("x", entity.Position.X);
            compound.PutInt("y", entity.Position.Y);
            compound.PutInt("z", entity.Position.Z);

            foreach (var tag in entity.Data.GetAllTags())
            {
                if (tag.Name is not ("Id" or "id" or "Pos" or "x" or "y" or "z"))
                {
                    compound.SetTag(tag.DeepClone());
                }
            }

            list.Add(compound);
        }

        return list;
    }

    private static void ValidateEntityForWrite(SchematicBlockEntity entity, SchematicDimensions dimensions)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            throw new SchematicException(
                SchematicErrorCodes.InvalidBlockEntity,
                $"BlockEntity at {entity.Position} has an empty Id.");
        }

        if (!dimensions.Contains(entity.Position))
        {
            throw new SchematicException(
                SchematicErrorCodes.InvalidBlockEntity,
                $"BlockEntity '{entity.Id}' at {entity.Position} is outside schematic bounds ({dimensions.Width}x{dimensions.Height}x{dimensions.Length}).");
        }
    }
}
