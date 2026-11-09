using NEXUS3D.Formats.Schematic.Common;
using NEXUS3D.Formats.Schematic.Nbt.Tags;
using NEXUS3D.Formats.Schematic.Nbt.Utilities;

namespace NEXUS3D.Formats.Schematic.Sponge;

/// <summary>
/// Reads and writes Sponge Schematic <c>Metadata</c> compound tags, preserving WorldEdit offsets,
/// RequiredMods, Author, Name, Date, and any custom unmapped tags.
/// </summary>
public sealed class SpongeMetadataParser
{
    public SchematicMetadata Parse(NbtCompoundTag? metadataCompound)
    {
        var metadata = new SchematicMetadata();
        if (metadataCompound is null)
        {
            return metadata;
        }

        if (metadataCompound.TryGetString("Name", out string name))
        {
            metadata.Name = name;
        }

        if (metadataCompound.TryGetString("Author", out string author))
        {
            metadata.Author = author;
        }

        if (metadataCompound.TryGetLong("Date", out long date))
        {
            metadata.DateUnixMilliseconds = date;
        }

        if (metadataCompound.TryGetList("RequiredMods", out var modsList) && modsList is not null)
        {
            for (int i = 0; i < modsList.Count; i++)
            {
                if (modsList[i] is NbtStringTag modStr && !string.IsNullOrWhiteSpace(modStr.Value))
                {
                    metadata.RequiredMods.Add(modStr.Value);
                }
            }
        }

        if (metadataCompound.TryGetNumericAsInt("WEOffsetX", out int weX) &&
            metadataCompound.TryGetNumericAsInt("WEOffsetY", out int weY) &&
            metadataCompound.TryGetNumericAsInt("WEOffsetZ", out int weZ))
        {
            metadata.WorldEditOffset = new SchematicOffset(weX, weY, weZ);
        }

        if (metadataCompound.TryGetCompound("WorldEdit", out var weComp) && weComp is not null)
        {
            if (weComp.TryGetIntArray("Origin", out int[] originArr) && originArr.Length == 3)
            {
                metadata.WorldEditOrigin = new SchematicOffset(originArr[0], originArr[1], originArr[2]);
            }
        }

        foreach (var tag in metadataCompound.GetAllTags())
        {
            if (tag.Name is not ("Name" or "Author" or "Date" or "RequiredMods" or "WEOffsetX" or "WEOffsetY" or "WEOffsetZ"))
            {
                metadata.UnmappedTags[tag.Name] = tag.DeepClone();
            }
        }

        return metadata;
    }

    public NbtCompoundTag? Write(SchematicMetadata? metadata)
    {
        if (metadata is null)
        {
            return null;
        }

        var compound = new NbtCompoundTag("Metadata");

        if (!string.IsNullOrWhiteSpace(metadata.Name))
        {
            compound.PutString("Name", metadata.Name);
        }

        if (!string.IsNullOrWhiteSpace(metadata.Author))
        {
            compound.PutString("Author", metadata.Author);
        }

        if (metadata.DateUnixMilliseconds.HasValue)
        {
            compound.PutLong("Date", metadata.DateUnixMilliseconds.Value);
        }

        if (metadata.RequiredMods.Count > 0)
        {
            var modsList = new NbtListTag("RequiredMods", NbtTagType.String);
            foreach (var mod in metadata.RequiredMods)
            {
                modsList.Add(new NbtStringTag(string.Empty, mod));
            }
            compound.SetTag(modsList);
        }

        if (metadata.WorldEditOffset.HasValue)
        {
            var we = metadata.WorldEditOffset.Value;
            compound.PutInt("WEOffsetX", we.X);
            compound.PutInt("WEOffsetY", we.Y);
            compound.PutInt("WEOffsetZ", we.Z);
        }

        foreach (var kvp in metadata.UnmappedTags)
        {
            compound.SetTag(kvp.Value.DeepClone());
        }

        return compound.Count > 0 ? compound : null;
    }
}

