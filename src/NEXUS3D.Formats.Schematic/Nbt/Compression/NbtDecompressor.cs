using System.Buffers;
using System.IO.Compression;

namespace NEXUS3D.Formats.Schematic.Nbt.Compression;

/// <summary>
/// Safely decompresses GZip, ZLib, or raw NBT streams while strictly enforcing maximum compressed and
/// decompressed byte limits to prevent zip-bomb / decompression-bomb attacks.
/// </summary>
public static class NbtDecompressor
{
    public static async Task<(byte[] RawBytes, NbtCompressionType DetectedCompression)> DecompressToBufferAsync(
        Stream source,
        NbtCompressionType requestedCompression,
        long maxCompressedBytes,
        long maxDecompressedBytes,
        bool allowUncompressed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        byte[] compressedBytes = await ReadBoundedStreamAsync(
            source,
            maxCompressedBytes,
            NbtErrorCodes.LimitExceeded,
            "Compressed NBT input stream exceeds maximum allowed compressed size.",
            cancellationToken).ConfigureAwait(false);

        if (compressedBytes.Length < 2)
        {
            throw new NbtException(NbtErrorCodes.InvalidNbt, "NBT payload is empty or truncated.");
        }

        NbtCompressionType actualCompression = requestedCompression == NbtCompressionType.AutoDetect
            ? NbtCompressionDetector.Detect(compressedBytes)
            : requestedCompression;

        if (actualCompression == NbtCompressionType.None)
        {
            if (!allowUncompressed)
            {
                throw new NbtException(
                    NbtErrorCodes.CompressionError,
                    "Uncompressed NBT is disabled by current NbtReadOptions.");
            }

            if (compressedBytes.Length > maxDecompressedBytes)
            {
                throw new NbtException(
                    NbtErrorCodes.LimitExceeded,
                    $"Uncompressed NBT size ({compressedBytes.Length}) exceeds maximum allowed size ({maxDecompressedBytes}).");
            }

            return (compressedBytes, NbtCompressionType.None);
        }

        try
        {
            using var compressedMemory = new MemoryStream(compressedBytes, writable: false);
            using Stream decompressionStream = actualCompression switch
            {
                NbtCompressionType.GZip => new GZipStream(compressedMemory, CompressionMode.Decompress, leaveOpen: true),
                NbtCompressionType.ZLib => new ZLibStream(compressedMemory, CompressionMode.Decompress, leaveOpen: true),
                _ => throw new NbtException(NbtErrorCodes.CompressionError, $"Unsupported compression mode '{actualCompression}'.")
            };

            byte[] decompressedBytes = await ReadBoundedStreamAsync(
                decompressionStream,
                maxDecompressedBytes,
                NbtErrorCodes.LimitExceeded,
                $"Decompressed NBT payload exceeds maximum allowed size ({maxDecompressedBytes} bytes).",
                cancellationToken).ConfigureAwait(false);

            if (decompressedBytes.Length == 0)
            {
                throw new NbtException(
                    NbtErrorCodes.CompressionError,
                    $"Corrupted or truncated {actualCompression} stream produced zero decompressed bytes.");
            }

            return (decompressedBytes, actualCompression);
        }
        catch (NbtException)
        {
            throw;
        }
        catch (InvalidDataException ex)
        {
            throw new NbtException(NbtErrorCodes.CompressionError, $"Corrupted {actualCompression} stream: {ex.Message}", ex);
        }
    }

    private static async Task<byte[]> ReadBoundedStreamAsync(
        Stream stream,
        long maxBytes,
        string limitErrorCode,
        string limitErrorMessage,
        CancellationToken cancellationToken)
    {
        byte[] rented = ArrayPool<byte>.Shared.Rent(65536);
        try
        {
            using var output = new MemoryStream();
            long totalRead = 0;

            while (true)
            {
                int read = await stream.ReadAsync(rented.AsMemory(0, rented.Length), cancellationToken).ConfigureAwait(false);
                if (read <= 0)
                {
                    break;
                }

                totalRead += read;
                if (totalRead > maxBytes)
                {
                    throw new NbtException(limitErrorCode, limitErrorMessage);
                }

                output.Write(rented, 0, read);
            }

            return output.ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
