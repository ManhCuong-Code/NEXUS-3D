namespace NEXUS3D.Formats.Schematic.Nbt.Compression;

/// <summary>
/// Compression mode for NBT streams.
/// </summary>
public enum NbtCompressionType
{
    AutoDetect = 0,
    None = 1,
    GZip = 2,
    ZLib = 3
}
