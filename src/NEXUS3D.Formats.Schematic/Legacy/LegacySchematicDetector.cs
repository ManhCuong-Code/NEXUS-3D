using NEXUS3D.Formats.Schematic.Common;
using NEXUS3D.Formats.Schematic.Nbt.IO;
using NEXUS3D.Formats.Schematic.Nbt.Tags;
using NEXUS3D.Formats.Schematic.Nbt.Utilities;

namespace NEXUS3D.Formats.Schematic.Legacy;

/// <summary>
/// Inspects an <see cref="NbtDocument"/> to determine whether it conforms to the Legacy MCEdit/WorldEdit
/// <c>.schematic</c> format (Pre-1.13 Alpha / 1.12.2).
/// </summary>
public static class LegacySchematicDetector
{
    public static bool TryDetect(
        NbtDocument document,
        out NbtCompoundTag schematicCompound)
    {
        ArgumentNullException.ThrowIfNull(document);
        schematicCompound = ResolveCandidateCompound(document.RootTag);

        // Must NOT be Sponge v2 or v3 (which have an integer Version tag and Palette)
        if (schematicCompound.TryGetInt("Version", out _))
        {
            return false;
        }

        if (!schematicCompound.Contains("Width") ||
            !schematicCompound.Contains("Height") ||
            !schematicCompound.Contains("Length"))
        {
            return false;
        }

        // Legacy schematics have ByteArray "Blocks" and ByteArray "Data" (plus usually Materials = "Alpha")
        return schematicCompound.TryGetByteArray("Blocks", out _) &&
               schematicCompound.TryGetByteArray("Data", out _);
    }

    public static NbtCompoundTag ResolveCandidateCompound(NbtCompoundTag root)
    {
        if (root.TryGetCompound("Schematic", out var nested) &&
            nested is not null &&
            nested.Contains("Blocks") &&
            nested.Contains("Data"))
        {
            return nested;
        }

        return root;
    }
}

