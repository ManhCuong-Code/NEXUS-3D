using NEXUS3D.Formats.Schematic.Common;

namespace NEXUS3D.Formats.Schematic.Legacy;

/// <summary>
/// Decodes Legacy <c>Blocks</c> (low 8 bits) and optional <c>AddBlocks</c> (packed high 4-bit nibbles)
/// into 12-bit block IDs (<c>0..4095</c>).
/// </summary>
public sealed class LegacyBlockDecoder
{
    public int[] DecodeBlocks(
        ReadOnlySpan<byte> blocks,
        ReadOnlySpan<byte> addBlocks,
        int expectedBlockCount)
    {
        if (blocks.Length != expectedBlockCount)
        {
            throw new SchematicException(
                SchematicErrorCodes.InvalidBlockData,
                $"Legacy 'Blocks' array length ({blocks.Length}) does not match expected volume ({expectedBlockCount}).");
        }

        if (!addBlocks.IsEmpty)
        {
            int expectedAddLength = (expectedBlockCount + 1) >> 1;
            if (addBlocks.Length != expectedAddLength)
            {
                throw new SchematicException(
                    SchematicErrorCodes.InvalidAddBlocks,
                    $"Legacy 'AddBlocks' array length ({addBlocks.Length}) does not match expected nibble length ({expectedAddLength}) for {expectedBlockCount} blocks.");
            }
        }

        int[] result = new int[expectedBlockCount];
        bool hasAddBlocks = !addBlocks.IsEmpty;

        for (int index = 0; index < expectedBlockCount; index++)
        {
            int lowBits = blocks[index] & 0xFF;
            if (!hasAddBlocks)
            {
                result[index] = lowBits;
                continue;
            }

            // Even index is stored in low nibble (bits 0..3), odd index in high nibble (bits 4..7)
            int addByte = addBlocks[index >> 1] & 0xFF;
            int highNibble = (index & 1) == 0
                ? (addByte & 0x0F)
                : ((addByte >> 4) & 0x0F);

            result[index] = (highNibble << 8) | lowBits;
        }

        return result;
    }
}
