using System.Text.Json;
using NEXUS3D.Domain.Metadata;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Formats.Schematic.Nbt.Compression;
using NEXUS3D.Formats.Schematic.Nbt.IO;
using NEXUS3D.Formats.Schematic.Nbt.Tags;

namespace NEXUS3D.Formats.Schematic.BlockEntities;

/// <summary>
/// Maps between <see cref="SchematicBlockEntity"/> and USM <see cref="BlockEntityData"/>,
/// preserving the full binary NBT payload inside <see cref="RawDataContainer"/> so zero inventory/text/modded tags are lost.
/// </summary>
public sealed class BlockEntityMapper
{
    public BlockEntityData MapToUsm(SchematicBlockEntity schematicEntity)
    {
        ArgumentNullException.ThrowIfNull(schematicEntity);

        var usmEntity = new BlockEntityData(schematicEntity.Id);

        // Extract top-level scalar/string properties for quick inspection in USM
        foreach (var tag in schematicEntity.Data.GetAllTags())
        {
            switch (tag)
            {
                case NbtStringTag s:
                    usmEntity.Properties[tag.Name] = JsonSerializer.SerializeToElement(s.Value);
                    break;
                case NbtIntTag i:
                    usmEntity.Properties[tag.Name] = JsonSerializer.SerializeToElement(i.Value);
                    break;
                case NbtShortTag sh:
                    usmEntity.Properties[tag.Name] = JsonSerializer.SerializeToElement(sh.Value);
                    break;
                case NbtByteTag b:
                    usmEntity.Properties[tag.Name] = JsonSerializer.SerializeToElement(b.Value);
                    break;
                case NbtLongTag l:
                    usmEntity.Properties[tag.Name] = JsonSerializer.SerializeToElement(l.Value);
                    break;
            }
        }

        // Preserve complete binary NBT of Data compound for 100% lossless round-trip
        using var ms = new MemoryStream();
        var writer = new NbtBinaryWriter(ms, new NbtWriteOptions { Compression = NbtCompressionType.None });
        var dataCompound = schematicEntity.Data.CloneCompound();
        dataCompound.Name = "Data";
        writer.WriteRootCompound(dataCompound);

        usmEntity.RawData = new RawDataContainer
        {
            Name = $"BlockEntity:{schematicEntity.Id}",
            SourceFormat = $"schematic_v{schematicEntity.SourceVersion}",
            PayloadKind = RawPayloadKind.Binary,
            BinaryPayload = ms.ToArray()
        };

        return usmEntity;
    }

    public SchematicBlockEntity MapFromUsm(VoxelPosition position, BlockEntityData usmEntity, int targetVersion = 2)
    {
        ArgumentNullException.ThrowIfNull(usmEntity);

        NbtCompoundTag dataCompound;
        if (usmEntity.RawData is { PayloadKind: RawPayloadKind.Binary, BinaryPayload.Length: > 0 })
        {
            var reader = new NbtBinaryReader(usmEntity.RawData.BinaryPayload);
            dataCompound = reader.ReadRootCompound();
        }
        else
        {
            dataCompound = new NbtCompoundTag("Data");
            foreach (var kvp in usmEntity.Properties)
            {
                if (kvp.Value.ValueKind == JsonValueKind.String)
                {
                    dataCompound.SetTag(new NbtStringTag(kvp.Key, kvp.Value.GetString() ?? string.Empty));
                }
                else if (kvp.Value.ValueKind == JsonValueKind.Number && kvp.Value.TryGetInt32(out int intVal))
                {
                    dataCompound.SetTag(new NbtIntTag(kvp.Key, intVal));
                }
            }
        }

        return new SchematicBlockEntity
        {
            Id = usmEntity.TypeId,
            Position = position,
            Data = dataCompound,
            SourceVersion = targetVersion
        };
    }
}
