using NEXUS3D.Formats.Schematic.Common;

namespace NEXUS3D.Formats.Schematic.Palette;

/// <summary>
/// Contract for decoding base-128 LEB128 unsigned VarInt values from Sponge BlockData byte arrays.
/// </summary>
public interface IVarIntDecoder
{
    int ReadVarInt(ReadOnlySpan<byte> data, ref int offset);
}

/// <summary>
/// High-performance, bounds-checked VarInt decoder enforcing a maximum 5-byte length and non-negative palette indices.
/// </summary>
public sealed class VarIntDecoder : IVarIntDecoder
{
    public static VarIntDecoder Instance { get; } = new();

    public int ReadVarInt(ReadOnlySpan<byte> data, ref int offset)
    {
        if (offset < 0 || offset >= data.Length)
        {
            throw new SchematicException(
                SchematicErrorCodes.InvalidVarInt,
                $"Unexpected end of BlockData buffer at offset {offset} (length={data.Length}).");
        }

        int value = 0;
        int bitOffset = 0;

        while (true)
        {
            if (offset >= data.Length)
            {
                throw new SchematicException(
                    SchematicErrorCodes.InvalidVarInt,
                    $"Truncated VarInt in BlockData buffer at end of stream (length={data.Length}).");
            }

            byte currentByte = data[offset++];
            value |= (currentByte & 0x7F) << bitOffset;

            if ((currentByte & 0x80) == 0)
            {
                break;
            }

            bitOffset += 7;
            if (bitOffset >= 35)
            {
                throw new SchematicException(
                    SchematicErrorCodes.InvalidVarInt,
                    "VarInt in BlockData exceeds maximum 5-byte length (35 bits).");
            }
        }

        if (value < 0)
        {
            throw new SchematicException(
                SchematicErrorCodes.InvalidVarInt,
                $"Decoded negative VarInt palette index ({value}), which is invalid for schematic palettes.");
        }

        return value;
    }
}
