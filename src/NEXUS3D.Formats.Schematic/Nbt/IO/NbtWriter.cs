using NEXUS3D.Formats.Schematic.Nbt.Compression;

namespace NEXUS3D.Formats.Schematic.Nbt.IO;

/// <summary>
/// Contract for writing <see cref="NbtDocument"/> instances to output streams with GZip/ZLib/None compression.
/// </summary>
public interface INbtWriter
{
    Task WriteAsync(
        NbtDocument document,
        Stream destination,
        NbtWriteOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation of <see cref="INbtWriter"/>.
/// </summary>
public sealed class NbtWriter : INbtWriter
{
    public async Task WriteAsync(
        NbtDocument document,
        Stream destination,
        NbtWriteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(destination);

        var effectiveOptions = options ?? NbtWriteOptions.Default;

        using var rawMemory = new MemoryStream();
        var binaryWriter = new NbtBinaryWriter(rawMemory, effectiveOptions);
        binaryWriter.WriteRootCompound(document.RootTag);

        NbtCompressionType targetCompression = effectiveOptions.Compression == NbtCompressionType.AutoDetect
            ? (document.Compression == NbtCompressionType.AutoDetect ? NbtCompressionType.GZip : document.Compression)
            : effectiveOptions.Compression;

        await NbtCompressor.WriteCompressedAsync(
            rawMemory.GetBuffer().AsMemory(0, (int)rawMemory.Length),
            destination,
            targetCompression,
            cancellationToken).ConfigureAwait(false);
    }
}
