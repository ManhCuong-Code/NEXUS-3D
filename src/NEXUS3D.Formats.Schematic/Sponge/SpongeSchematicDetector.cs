using NEXUS3D.Formats.Schematic.Common;
using NEXUS3D.Formats.Schematic.Nbt.IO;
using NEXUS3D.Formats.Schematic.Nbt.Tags;
using NEXUS3D.Formats.Schematic.Nbt.Utilities;

namespace NEXUS3D.Formats.Schematic.Sponge;

/// <summary>
/// Inspects an <see cref="NbtDocument"/> to determine whether it conforms to Sponge Schematic v2 or v3,
/// and resolves the effective Sponge schematic compound tag.
/// </summary>
public static class SpongeSchematicDetector
{
    public static bool TryDetect(
        NbtDocument document,
        out SchematicFormatId formatId,
        out NbtCompoundTag schematicCompound,
        out int version)
    {
        ArgumentNullException.ThrowIfNull(document);
        formatId = SchematicFormatId.Unknown;
        schematicCompound = document.RootTag;
        version = 0;

        NbtCompoundTag candidate = ResolveCandidateCompound(document.RootTag);

        if (!candidate.TryGetInt("Version", out int ver))
        {
            return false;
        }

        version = ver;
        schematicCompound = candidate;

        if (ver == 2)
        {
            if (candidate.Contains("Palette") && (candidate.Contains("BlockData") || candidate.Contains("Blocks")))
            {
                formatId = SchematicFormatId.SpongeV2;
                return true;
            }
        }
        else if (ver == 3)
        {
            if (candidate.TryGetCompound("Blocks", out var blocksComp) &&
                blocksComp is not null &&
                blocksComp.Contains("Palette") &&
                blocksComp.Contains("Data"))
            {
                formatId = SchematicFormatId.SpongeV3;
                return true;
            }
        }

        // Has Version tag (e.g. Version=1 or Version=4) -> caller can report SCH002 UnsupportedVersion
        return false;
    }

    public static NbtCompoundTag ResolveCandidateCompound(NbtCompoundTag root)
    {
        if (root.TryGetCompound("Schematic", out var nested) &&
            nested is not null &&
            (nested.Contains("Version") || nested.Contains("Blocks") || nested.Contains("Width")))
        {
            return nested;
        }

        return root;
    }
}
