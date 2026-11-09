using NEXUS3D.Formats.Schematic.Nbt.Compression;
using NEXUS3D.Formats.Schematic.Nbt.Utilities;

namespace NEXUS3D.Formats.Schematic.Nbt.IO;

/// <summary>
/// Compression and byte-order options for writing NBT binary streams.
/// </summary>
public sealed record NbtWriteOptions
{
    public static NbtWriteOptions Default { get; } = new();

    public NbtCompressionType Compression { get; init; } = NbtCompressionType.GZip;
    public NbtEndianness Endianness { get; init; } = NbtEndianness.BigEndian;
    public int MaxNestingDepth { get; init; } = 128;
}

