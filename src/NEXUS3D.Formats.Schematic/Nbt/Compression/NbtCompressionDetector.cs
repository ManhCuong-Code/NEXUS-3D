namespace NEXUS3D.Formats.Schematic.Nbt.Compression;

/// <summary>
/// Inspects the leading magic bytes of an NBT stream to determine whether it is GZip-compressed,
/// ZLib-compressed, or uncompressed raw NBT.
/// </summary>
public static class NbtCompressionDetector
{
    public static NbtCompressionType Detect(ReadOnlySpan<byte> headerBytes)
    {
        if (headerBytes.Length < 2)
        {
            throw new NbtException(NbtErrorCodes.InvalidNbt, "NBT stream is too short to determine compression header.");
        }

        // GZip magic bytes: 0x1F 0x8B
        if (headerBytes[0] == 0x1F && headerBytes[1] == 0x8B)
        {
            return NbtCompressionType.GZip;
        }

        // ZLib header: 0x78 followed by 0x01, 0x5E, 0x9C, or 0xDA
        if (headerBytes[0] == 0x78 &&
            (headerBytes[1] == 0x01 || headerBytes[1] == 0x5E || headerBytes[1] == 0x9C || headerBytes[1] == 0xDA))
        {
            return NbtCompressionType.ZLib;
        }

        // Raw uncompressed NBT starts with TAG_Compound (0x0A)
        if (headerBytes[0] == 0x0A)
        {
            return NbtCompressionType.None;
        }

        throw new NbtException(
            NbtErrorCodes.CompressionError,
            $"Unrecognized NBT header bytes (0x{headerBytes[0]:X2} 0x{headerBytes[1]:X2}). Expected GZip (1F 8B), ZLib (78 xx), or TAG_Compound (0A).");
    }
}
