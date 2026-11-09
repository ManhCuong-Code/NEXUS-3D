using NEXUS3D.Formats.Schematic.Common;

namespace NEXUS3D.Formats.Schematic.Legacy;

/// <summary>
/// Encodes 4-bit Legacy block metadata values (<c>0..15</c>) into a standard <c>Data</c> byte array
/// or a packed 4-bit nibble array.
/// </summary>
public sealed class LegacyMetadataEncoder
{
    public byte[] EncodeMetadata(ReadOnlySpan<byte> metadata)
    {
        byte[] output = new byte[metadata.Length];
        for (int i = 0; i < metadata.Length; i++)
        {
            byte val = metadata[i];
            if (val > 15)
            {
                throw new SchematicException(
                    SchematicErrorCodes.InvalidLegacyMetadata,
                    $"Legacy metadata value {val} at index {i} exceeds 4-bit range [0..15].");
            }

            output[i] = (byte)(val & 0x0F);
        }

        return output;
    }

    public byte[] EncodePackedNibbles(ReadOnlySpan<byte> metadata)
    {
        byte[] packed = new byte[(metadata.Length + 1) >> 1];
        for (int index = 0; index < metadata.Length; index++)
        {
            byte val = metadata[index];
            if (val > 15)
            {
                throw new SchematicException(
                    SchematicErrorCodes.InvalidLegacyMetadata,
                    $"Legacy metadata value {val} at index {index} exceeds 4-bit range [0..15].");
            }

            int byteIndex = index >> 1;
            if ((index & 1) == 0)
            {
                packed[byteIndex] = (byte)((packed[byteIndex] & 0xF0) | (val & 0x0F));
            }
            else
            {
                packed[byteIndex] = (byte)((packed[byteIndex] & 0x0F) | ((val & 0x0F) << 4));
            }
        }

        return packed;
    }
}

