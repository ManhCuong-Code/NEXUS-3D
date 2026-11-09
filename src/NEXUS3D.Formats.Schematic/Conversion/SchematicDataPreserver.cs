using NEXUS3D.Domain.Metadata;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Formats.Schematic.Common;
using NEXUS3D.Formats.Schematic.Nbt.Compression;
using NEXUS3D.Formats.Schematic.Nbt.IO;
using NEXUS3D.Formats.Schematic.Nbt.Tags;
using NEXUS3D.Formats.Schematic.Nbt.Utilities;

namespace NEXUS3D.Formats.Schematic.Conversion;

/// <summary>
/// Preserves and restores Sponge/Legacy-specific raw NBT payloads (<c>Biomes</c>, <c>Entities</c>,
/// unmapped root tags, and unmapped metadata tags) inside USM <see cref="RawDataContainer"/> instances.
/// </summary>
public static class SchematicDataPreserver
{
    public const string BiomesContainerName = "schematic:biomes";
    public const string EntitiesContainerName = "schematic:entities";
    public const string UnmappedRootContainerName = "schematic:unmapped_root";
    public const string UnmappedMetadataContainerName = "schematic:unmapped_metadata";

    public static void PreserveIntoScene(SchematicDocument document, Scene3D scene)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(scene);

        string sourceFormat = document.Format.ToString();

        if (document.PreservedBiomesTag is not null)
        {
            var clone = document.PreservedBiomesTag.CloneCompound();
            clone.Name = "Biomes";
            scene.Metadata.PreservedRawData.Add(SerializeCompoundToContainer(BiomesContainerName, sourceFormat, clone));
            scene.Metadata.LossReport.RecordPreservedRaw(
                "Biomes",
                "Sponge v3 Biomes compound preserved losslessly as binary NBT in SceneMetadata.PreservedRawData.");
        }

        if (document.PreservedEntitiesTag is not null)
        {
            var wrapper = new NbtCompoundTag("EntitiesWrapper");
            var listClone = (NbtListTag)document.PreservedEntitiesTag.DeepClone();
            listClone.Name = "Entities";
            wrapper.SetTag(listClone);

            scene.Metadata.PreservedRawData.Add(SerializeCompoundToContainer(EntitiesContainerName, sourceFormat, wrapper));
            scene.Metadata.LossReport.RecordPreservedRaw(
                "Entities",
                $"Schematic Entities list ({listClone.Count} entries) preserved losslessly as binary NBT in SceneMetadata.PreservedRawData.");
        }

        if (document.UnmappedRootTags.Count > 0)
        {
            var rootWrapper = new NbtCompoundTag("UnmappedRoot");
            foreach (var kvp in document.UnmappedRootTags)
            {
                rootWrapper.SetTag(kvp.Value.DeepClone());
            }

            scene.Metadata.PreservedRawData.Add(SerializeCompoundToContainer(UnmappedRootContainerName, sourceFormat, rootWrapper));
            scene.Metadata.LossReport.RecordPreservedRaw(
                "UnmappedRootTags",
                $"Preserved {document.UnmappedRootTags.Count} custom root NBT tag(s) in SceneMetadata.PreservedRawData.");
        }

        if (document.Metadata.UnmappedTags.Count > 0)
        {
            var metaWrapper = new NbtCompoundTag("UnmappedMetadata");
            foreach (var kvp in document.Metadata.UnmappedTags)
            {
                metaWrapper.SetTag(kvp.Value.DeepClone());
            }

            scene.Metadata.PreservedRawData.Add(SerializeCompoundToContainer(UnmappedMetadataContainerName, sourceFormat, metaWrapper));
            scene.Metadata.LossReport.RecordPreservedRaw(
                "UnmappedMetadataTags",
                $"Preserved {document.Metadata.UnmappedTags.Count} custom Metadata NBT tag(s) in SceneMetadata.PreservedRawData.");
        }
    }

    public static void RestoreFromScene(Scene3D scene, SchematicDocument document)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(document);

        foreach (var container in scene.Metadata.PreservedRawData)
        {
            if (container.PayloadKind != RawPayloadKind.Binary || container.BinaryPayload is not { Length: > 0 })
            {
                continue;
            }

            switch (container.Name)
            {
                case BiomesContainerName:
                {
                    var comp = DeserializeCompoundFromContainer(container);
                    comp.Name = "Biomes";
                    document.PreservedBiomesTag = comp;
                    break;
                }
                case EntitiesContainerName:
                {
                    var wrapper = DeserializeCompoundFromContainer(container);
                    if (wrapper.TryGetList("Entities", out var entitiesList) && entitiesList is not null)
                    {
                        document.PreservedEntitiesTag = (NbtListTag)entitiesList.DeepClone();
                    }
                    break;
                }
                case UnmappedRootContainerName:
                {
                    var wrapper = DeserializeCompoundFromContainer(container);
                    foreach (var tag in wrapper.GetAllTags())
                    {
                        document.UnmappedRootTags[tag.Name] = tag.DeepClone();
                    }
                    break;
                }
                case UnmappedMetadataContainerName:
                {
                    var wrapper = DeserializeCompoundFromContainer(container);
                    foreach (var tag in wrapper.GetAllTags())
                    {
                        document.Metadata.UnmappedTags[tag.Name] = tag.DeepClone();
                    }
                    break;
                }
            }
        }
    }

    private static RawDataContainer SerializeCompoundToContainer(string name, string sourceFormat, NbtCompoundTag compound)
    {
        using var ms = new MemoryStream();
        var writer = new NbtBinaryWriter(ms, new NbtWriteOptions { Compression = NbtCompressionType.None });
        writer.WriteRootCompound(compound);

        return new RawDataContainer
        {
            Name = name,
            SourceFormat = sourceFormat,
            PayloadKind = RawPayloadKind.Binary,
            BinaryPayload = ms.ToArray()
        };
    }

    private static NbtCompoundTag DeserializeCompoundFromContainer(RawDataContainer container)
    {
        var reader = new NbtBinaryReader(container.BinaryPayload!);
        return reader.ReadRootCompound();
    }
}
