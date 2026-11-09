using NEXUS3D.Formats.Schematic.Common;

namespace NEXUS3D.Formats.Schematic.Legacy;

/// <summary>
/// Encodes 12-bit block IDs (<c>0..4095</c>) into Legacy <c>Blocks</c> (<c>byte[]</c>)
/// and optional <c>AddBlocks</c> (<c>byte[]</c> packed 4-bit nibbles when any ID exceeds 255).
/// </summary>
public sealed class LegacyBlockEncoder
{
    public (byte[] Blocks, byte[]? AddBlocks) EncodeBlocks(ReadOnlySpan<int> blockIds)
    {
        byte[] blocks = new byte[blockIds.Length];
        byte[]? addBlocks = null;

        for (int index = 0; index < blockIds.Length; index++)
        {
            int id = blockIds[index];
            if (id < 0 || id > 4095)
            {
                throw new SchematicException(
                    SchematicErrorCodes.UnmappedLegacyId,
                    $"Block ID {id} at index {index} is outside the 12-bit Legacy range [0..4095].");
            }

            blocks[index] = (byte)(id & 0xFF);
            int highNibble = (id >> 8) & 0x0F;

            if (highNibble != 0)
            {
                addBlocks ??= new byte[(blockIds.Length + 1) >> 1];
                int addIndex = index >> 1;

                if ((index & 1) == 0)
                {
                    addBlocks[addIndex] = (byte)((addBlocks[addIndex] & 0xF0) | highNibble);
                }
                else
                {
                    addBlocks[addIndex] = (byte)((addBlocks[addIndex] & 0x0F) | (highNibble << 4));
                }
            }
        }

        return (blocks, addBlocks);
    }
}

