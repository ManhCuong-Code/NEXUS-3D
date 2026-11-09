using NEXUS3D.Formats.Schematic.Nbt.Compression;
using NEXUS3D.Formats.Schematic.Nbt.Tags;
using NEXUS3D.Formats.Schematic.Nbt.Utilities;

namespace NEXUS3D.Formats.Schematic.Nbt.IO;

/// <summary>
/// Represents a complete Named Binary Tag (NBT) document rooted at an <see cref="NbtCompoundTag"/>.
/// </summary>
public sealed class NbtDocument
{
    public NbtCompoundTag RootTag { get; }
    public NbtCompressionType Compression { get; }
    public NbtEndianness Endianness { get; }

    public NbtDocument(
        NbtCompoundTag rootTag,
        NbtCompressionType compression = NbtCompressionType.GZip,
        NbtEndianness endianness = NbtEndianness.BigEndian)
    {
        ArgumentNullException.ThrowIfNull(rootTag);
        RootTag = rootTag;
        Compression = compression;
        Endianness = endianness;
    }

    public NbtDocument DeepClone() =>
        new(RootTag.CloneCompound(), Compression, Endianness);
}
