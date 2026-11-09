using NEXUS3D.Formats.Schematic.Nbt.Compression;
using NEXUS3D.Formats.Schematic.Nbt.Utilities;

namespace NEXUS3D.Formats.Schematic.Nbt.IO;

/// <summary>
/// Security limits and decoding options for reading NBT binary streams.
/// </summary>
public sealed record NbtReadOptions
{
    public static NbtReadOptions Default { get; } = new();

    public NbtCompressionType Compression { get; init; } = NbtCompressionType.AutoDetect;
    public NbtEndianness Endianness { get; init; } = NbtEndianness.BigEndian;
    public bool AllowUncompressed { get; init; } = true;

    public long MaxCompressedBytes { get; init; } = 64L * 1024L * 1024L;     // 64 MB
    public long MaxDecompressedBytes { get; init; } = 256L * 1024L * 1024L;  // 256 MB
    public int MaxNestingDepth { get; init; } = 128;
    public int MaxStringLength { get; init; } = 32767;
    public int MaxArrayLength { get; init; } = 67_108_864;                   // 64M elements
    public int MaxListLength { get; init; } = 1_048_576;                     // 1M list items
    public int MaxTagCount { get; init; } = 2_097_152;                       // 2M total tags
}
