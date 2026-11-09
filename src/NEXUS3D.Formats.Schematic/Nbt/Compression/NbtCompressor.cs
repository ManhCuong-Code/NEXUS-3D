using System.IO.Compression;

namespace NEXUS3D.Formats.Schematic.Nbt.Compression;

/// <summary>
/// Compresses raw NBT bytes into GZip, ZLib, or writes raw uncompressed bytes to the destination stream.
/// </summary>
public static class NbtCompressor
{
    public static async Task WriteCompressedAsync(
        ReadOnlyMemory<byte> rawNbtBytes,
        Stream destination,
        NbtCompressionType compression,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);

        NbtCompressionType effective = compression == NbtCompressionType.AutoDetect
            ? NbtCompressionType.GZip
            : compression;

        switch (effective)
        {
            case NbtCompressionType.None:
                await destination.WriteAsync(rawNbtBytes, cancellationToken).ConfigureAwait(false);
                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
                break;

            case NbtCompressionType.GZip:
                await using (var gzip = new GZipStream(destination, CompressionLevel.Optimal, leaveOpen: true))
                {
                    await gzip.WriteAsync(rawNbtBytes, cancellationToken).ConfigureAwait(false);
                    await gzip.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
                break;

            case NbtCompressionType.ZLib:
                await using (var zlib = new ZLibStream(destination, CompressionLevel.Optimal, leaveOpen: true))
                {
                    await zlib.WriteAsync(rawNbtBytes, cancellationToken).ConfigureAwait(false);
                    await zlib.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
                break;

            default:
                throw new NbtException(NbtErrorCodes.CompressionError, $"Unsupported NBT compression mode '{compression}'.");
        }
    }
}
