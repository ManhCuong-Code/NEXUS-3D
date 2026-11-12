using System.IO.Compression;
using System.Numerics;
using NEXUS3D.Domain.Materials;
using NEXUS3D.Domain.Mathematics;

namespace NEXUS3D.Formats.BedrockGeometry.Exporting;

/// <summary>
/// Generates pixel-perfect 32-bit RGBA PNG texture atlases and swatch palettes for Bedrock models and resource packs.
/// Pure C# implementation with zero external dependencies, supporting full alpha transparency.
/// </summary>
public static class BedrockTextureAtlasGenerator
{
    private static readonly uint[] CrcTable = InitializeCrcTable();

    /// <summary>
    /// Generates a solid or grid-based RGBA PNG texture atlas from a collection of colors or materials.
    /// Returns the PNG bytes and UV coordinates for each key.
    /// </summary>
    public static (byte[] PngBytes, Dictionary<string, Vector2D> SwatchUvs, int Width, int Height) GenerateAtlas(
        IReadOnlyDictionary<string, Vector4> colorMap,
        int minSize = 64)
    {
        ArgumentNullException.ThrowIfNull(colorMap);

        int count = Math.Max(1, colorMap.Count);
        // Find grid dimensions (e.g., 2x2, 4x4, 8x8)
        int cols = (int)Math.Ceiling(Math.Sqrt(count));
        int rows = (int)Math.Ceiling((double)count / cols);

        // Power of two texture size
        int width = Math.Max(minSize, NextPowerOfTwo(cols * 16));
        int height = Math.Max(minSize, NextPowerOfTwo(rows * 16));

        // Make square
        int size = Math.Max(width, height);
        width = size;
        height = size;

        int cellW = width / Math.Max(1, cols);
        int cellH = height / Math.Max(1, rows);

        byte[] rgbaData = new byte[width * height * 4];
        var swatchUvs = new Dictionary<string, Vector2D>(StringComparer.OrdinalIgnoreCase);

        int index = 0;
        foreach (var (key, color) in colorMap)
        {
            int col = index % cols;
            int row = index / cols;

            int startX = col * cellW;
            int startY = row * cellH;

            byte r = (byte)Math.Clamp((int)(color.X * 255f), 0, 255);
            byte g = (byte)Math.Clamp((int)(color.Y * 255f), 0, 255);
            byte b = (byte)Math.Clamp((int)(color.Z * 255f), 0, 255);
            byte a = (byte)Math.Clamp((int)(color.W * 255f), 0, 255);

            for (int y = startY; y < startY + cellH && y < height; y++)
            {
                for (int x = startX; x < startX + cellW && x < width; x++)
                {
                    int offset = (y * width + x) * 4;
                    rgbaData[offset] = r;
                    rgbaData[offset + 1] = g;
                    rgbaData[offset + 2] = b;
                    rgbaData[offset + 3] = a;
                }
            }

            // Top-left pixel UV in Bedrock pixel space
            swatchUvs[key] = new Vector2D(startX, startY);
            index++;
        }

        // Fill remaining cells with subtle dark checkerboard if count is 0
        if (colorMap.Count == 0)
        {
            Array.Fill(rgbaData, (byte)180);
            for (int i = 3; i < rgbaData.Length; i += 4)
            {
                rgbaData[i] = 255;
            }
        }

        byte[] pngBytes = EncodeRgbaToPng(rgbaData, width, height);
        return (pngBytes, swatchUvs, width, height);
    }

    /// <summary>
    /// Creates a single solid-color RGBA PNG file of given dimensions.
    /// </summary>
    public static byte[] CreateSolidColorPng(int width, int height, Vector4 color)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);

        byte r = (byte)Math.Clamp((int)(color.X * 255f), 0, 255);
        byte g = (byte)Math.Clamp((int)(color.Y * 255f), 0, 255);
        byte b = (byte)Math.Clamp((int)(color.Z * 255f), 0, 255);
        byte a = (byte)Math.Clamp((int)(color.W * 255f), 0, 255);

        byte[] rgba = new byte[width * height * 4];
        for (int i = 0; i < rgba.Length; i += 4)
        {
            rgba[i] = r;
            rgba[i + 1] = g;
            rgba[i + 2] = b;
            rgba[i + 3] = a;
        }

        return EncodeRgbaToPng(rgba, width, height);
    }

    /// <summary>
    /// Encodes a raw 32-bit RGBA buffer into a fully-compliant PNG file using ZLib compression.
    /// </summary>
    public static byte[] EncodeRgbaToPng(byte[] rgbaData, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(rgbaData);
        if (rgbaData.Length != width * height * 4)
        {
            throw new ArgumentException($"RGBA buffer length ({rgbaData.Length}) does not match dimensions ({width}x{height}x4 = {width * height * 4}).");
        }

        using var output = new MemoryStream();

        // 1. PNG Header (8 bytes)
        ReadOnlySpan<byte> pngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        output.Write(pngHeader);

        // 2. IHDR Chunk
        byte[] ihdrData = new byte[13];
        WriteBigEndianInt(ihdrData, 0, width);
        WriteBigEndianInt(ihdrData, 4, height);
        ihdrData[8] = 8;  // bit depth: 8
        ihdrData[9] = 6;  // color type: RGBA (6)
        ihdrData[10] = 0; // compression: deflate
        ihdrData[11] = 0; // filter: standard
        ihdrData[12] = 0; // interlace: none
        WriteChunk(output, "IHDR"u8, ihdrData);

        // 3. IDAT Chunk (Filter scanlines with filter byte 0, then ZLib compress)
        byte[] rawScanlines = new byte[height * (1 + width * 4)];
        int srcOffset = 0;
        int dstOffset = 0;

        for (int y = 0; y < height; y++)
        {
            rawScanlines[dstOffset++] = 0; // Filter byte: 0 (None)
            int rowBytes = width * 4;
            Buffer.BlockCopy(rgbaData, srcOffset, rawScanlines, dstOffset, rowBytes);
            srcOffset += rowBytes;
            dstOffset += rowBytes;
        }

        using (var compressedStream = new MemoryStream())
        {
            using (var zlib = new ZLibStream(compressedStream, CompressionLevel.Optimal, leaveOpen: true))
            {
                zlib.Write(rawScanlines, 0, rawScanlines.Length);
            }
            byte[] idatData = compressedStream.ToArray();
            WriteChunk(output, "IDAT"u8, idatData);
        }

        // 4. IEND Chunk
        WriteChunk(output, "IEND"u8, []);

        return output.ToArray();
    }

    private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, byte[] data)
    {
        // 4 bytes Length
        byte[] lenBytes = new byte[4];
        WriteBigEndianInt(lenBytes, 0, data.Length);
        output.Write(lenBytes);

        // Type
        output.Write(type);

        // Data
        if (data.Length > 0)
        {
            output.Write(data);
        }

        // 4 bytes CRC
        uint crc = CalculateCrc(type, data);
        byte[] crcBytes = new byte[4];
        WriteBigEndianUInt(crcBytes, 0, crc);
        output.Write(crcBytes);
    }

    private static uint CalculateCrc(ReadOnlySpan<byte> type, byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        for (int i = 0; i < type.Length; i++)
        {
            crc = CrcTable[(crc ^ type[i]) & 0xFF] ^ (crc >> 8);
        }
        for (int i = 0; i < data.Length; i++)
        {
            crc = CrcTable[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
        }
        return ~crc;
    }

    private static uint[] InitializeCrcTable()
    {
        uint[] table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }
            table[i] = c;
        }
        return table;
    }

    private static void WriteBigEndianInt(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)((value >> 24) & 0xFF);
        buffer[offset + 1] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 3] = (byte)(value & 0xFF);
    }

    private static void WriteBigEndianUInt(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)((value >> 24) & 0xFF);
        buffer[offset + 1] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 3] = (byte)(value & 0xFF);
    }

    private static int NextPowerOfTwo(int val)
    {
        int p = 16;
        while (p < val)
        {
            p *= 2;
        }
        return p;
    }
}

