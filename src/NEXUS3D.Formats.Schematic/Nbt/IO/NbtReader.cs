using NEXUS3D.Formats.Schematic.Nbt.Compression;

namespace NEXUS3D.Formats.Schematic.Nbt.IO;

/// <summary>
/// Contract for reading <see cref="NbtDocument"/> instances from compressed or raw streams.
/// </summary>
public interface INbtReader
{
    Task<NbtDocument> ReadAsync(
        Stream source,
        NbtReadOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation of <see cref="INbtReader"/> handling GZip/ZLib decompression,
/// endianness validation, and full 13-tag NBT parsing.
/// </summary>
public sealed class NbtReader : INbtReader
{
    public async Task<NbtDocument> ReadAsync(
        Stream source,
        NbtReadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var effectiveOptions = options ?? NbtReadOptions.Default;

        var (rawBytes, detectedCompression) = await NbtDecompressor.DecompressToBufferAsync(
            source,
            effectiveOptions.Compression,
            effectiveOptions.MaxCompressedBytes,
            effectiveOptions.MaxDecompressedBytes,
            effectiveOptions.AllowUncompressed,
            cancellationToken).ConfigureAwait(false);

        var binaryReader = new NbtBinaryReader(rawBytes, effectiveOptions);
        var rootTag = binaryReader.ReadRootCompound();

        return new NbtDocument(rootTag, detectedCompression, effectiveOptions.Endianness);
    }
}

