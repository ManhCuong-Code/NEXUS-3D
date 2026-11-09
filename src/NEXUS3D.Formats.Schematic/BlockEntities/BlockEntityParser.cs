using NEXUS3D.Domain.Voxels;
using NEXUS3D.Formats.Schematic.Common;
using NEXUS3D.Formats.Schematic.Nbt.Tags;
using NEXUS3D.Formats.Schematic.Nbt.Utilities;

namespace NEXUS3D.Formats.Schematic.BlockEntities;

/// <summary>
/// Parses block entities from Sponge v2 (<c>BlockEntities</c> / <c>TileEntities</c>),
/// Sponge v3 (<c>Blocks.BlockEntities</c> with nested <c>Data</c>), and Legacy (<c>TileEntities</c> with <c>id/x/y/z</c>).
/// Preserves all custom and modded NBT tags.
/// </summary>
public sealed class BlockEntityParser
{
    public List<SchematicBlockEntity> ParseSpongeV2BlockEntities(NbtListTag? listTag)
    {
        var results = new List<SchematicBlockEntity>();
        if (listTag is null || listTag.Count == 0)
        {
            return results;
        }

        for (int i = 0; i < listTag.Count; i++)
        {
            if (listTag[i] is not NbtCompoundTag compound)
            {
                throw new SchematicException(
                    SchematicErrorCodes.InvalidBlockEntity,
                    $"BlockEntity[{i}] in Sponge v2 schematic is not a TAG_Compound.");
            }

            string id = ReadEntityId(compound, i);
            VoxelPosition pos = ReadEntityPosition(compound, i);

            var data = new NbtCompoundTag("Data");
            foreach (var child in compound.GetAllTags())
            {
                if (!IsIdOrPosKey(child.Name))
                {
                    data.SetTag(child.DeepClone());
                }
            }

            results.Add(new SchematicBlockEntity
            {
                Id = id,
                Position = pos,
                Data = data,
                SourceVersion = 2,
                RawOriginalTag = compound.CloneCompound()
            });
        }

        return results;
    }

    public List<SchematicBlockEntity> ParseSpongeV3BlockEntities(NbtListTag? listTag)
    {
        var results = new List<SchematicBlockEntity>();
        if (listTag is null || listTag.Count == 0)
        {
            return results;
        }

        for (int i = 0; i < listTag.Count; i++)
        {
            if (listTag[i] is not NbtCompoundTag compound)
            {
                throw new SchematicException(
                    SchematicErrorCodes.InvalidBlockEntity,
                    $"BlockEntity[{i}] in Sponge v3 schematic is not a TAG_Compound.");
            }

            string id = ReadEntityId(compound, i);
            VoxelPosition pos = ReadEntityPosition(compound, i);

            NbtCompoundTag data;
            if (compound.TryGetCompound("Data", out var nestedData) && nestedData is not null)
            {
                data = nestedData.CloneCompound();
            }
            else
            {
                data = new NbtCompoundTag("Data");
                foreach (var child in compound.GetAllTags())
                {
                    if (!IsIdOrPosKey(child.Name))
                    {
                        data.SetTag(child.DeepClone());
                    }
                }
            }

            results.Add(new SchematicBlockEntity
            {
                Id = id,
                Position = pos,
                Data = data,
                SourceVersion = 3,
                RawOriginalTag = compound.CloneCompound()
            });
        }

        return results;
    }

    public List<SchematicBlockEntity> ParseLegacyTileEntities(NbtListTag? listTag)
    {
        var results = new List<SchematicBlockEntity>();
        if (listTag is null || listTag.Count == 0)
        {
            return results;
        }

        for (int i = 0; i < listTag.Count; i++)
        {
            if (listTag[i] is not NbtCompoundTag compound)
            {
                throw new SchematicException(
                    SchematicErrorCodes.InvalidBlockEntity,
                    $"TileEntity[{i}] in Legacy schematic is not a TAG_Compound.");
            }

            string id = ReadEntityId(compound, i);
            VoxelPosition pos = ReadEntityPosition(compound, i);

            var data = new NbtCompoundTag("Data");
            foreach (var child in compound.GetAllTags())
            {
                if (!IsIdOrPosKey(child.Name))
                {
                    data.SetTag(child.DeepClone());
                }
            }

            results.Add(new SchematicBlockEntity
            {
                Id = id,
                Position = pos,
                Data = data,
                SourceVersion = 1,
                RawOriginalTag = compound.CloneCompound()
            });
        }

        return results;
    }

    private static string ReadEntityId(NbtCompoundTag compound, int index)
    {
        if (compound.TryGetString("Id", out string id) && !string.IsNullOrWhiteSpace(id))
        {
            return id;
        }

        if (compound.TryGetString("id", out string lowerId) && !string.IsNullOrWhiteSpace(lowerId))
        {
            return lowerId;
        }

        throw new SchematicException(
            SchematicErrorCodes.InvalidBlockEntity,
            $"BlockEntity[{index}] is missing required 'Id' / 'id' string tag.");
    }

    private static VoxelPosition ReadEntityPosition(NbtCompoundTag compound, int index)
    {
        if (compound.TryGetIntArray("Pos", out int[] posArr))
        {
            if (posArr.Length != 3)
            {
                throw new SchematicException(
                    SchematicErrorCodes.InvalidBlockEntity,
                    $"BlockEntity[{index}] 'Pos' IntArray must have 3 elements, found {posArr.Length}.");
            }
            return new VoxelPosition(posArr[0], posArr[1], posArr[2]);
        }

        if (compound.TryGetNumericAsInt("x", out int x) &&
            compound.TryGetNumericAsInt("y", out int y) &&
            compound.TryGetNumericAsInt("z", out int z))
        {
            return new VoxelPosition(x, y, z);
        }

        throw new SchematicException(
            SchematicErrorCodes.InvalidBlockEntity,
            $"BlockEntity[{index}] is missing valid 'Pos' IntArray or ('x', 'y', 'z') coordinate tags.");
    }

    private static bool IsIdOrPosKey(string key) =>
        key is "Id" or "id" or "Pos" or "x" or "y" or "z";
}
