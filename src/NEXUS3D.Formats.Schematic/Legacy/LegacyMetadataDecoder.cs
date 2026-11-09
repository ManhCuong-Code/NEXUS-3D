using NEXUS3D.Formats.Schematic.Common;

namespace NEXUS3D.Formats.Schematic.Legacy;

/// <summary>
/// Decodes and validates 4-bit Legacy block metadata values (<c>0..15</c>) from either a per-block <c>Data</c>
/// byte array or a packed 4-bit nibble byte array.
/// </summary>
public sealed class LegacyMetadataDecoder
{
    public byte[] DecodeMetadata(ReadOnlySpan<byte> dataBytes, int expectedBlockCount)
    {
        if (dataBytes.Length == expectedBlockCount)
        {
            byte[] metadata = new byte[expectedBlockCount];
            for (int i = 0; i < expectedBlockCount; i++)
            {
                byte value = dataBytes[i];
                if (value > 15)
                {
                    throw new SchematicException(
                        SchematicErrorCodes.InvalidLegacyMetadata,
                        $"Legacy metadata value {value} at index {i} exceeds 4-bit range [0..15].");
                }

                metadata[i] = (byte)(value & 0x0F);
            }

            return metadata;
        }

        int packedLength = (expectedBlockCount + 1) >> 1;
        if (dataBytes.Length == packedLength && expectedBlockCount > 1)
        {
            return DecodePackedNibbles(dataBytes, expectedBlockCount);
        }

        throw new SchematicException(
            SchematicErrorCodes.InvalidLegacyMetadata,
            $"Legacy 'Data' array length ({dataBytes.Length}) does not match expected block count ({expectedBlockCount}).");
    }

    public byte[] DecodePackedNibbles(ReadOnlySpan<byte> packedBytes, int expectedBlockCount)
    {
        int expectedPackedLength = (expectedBlockCount + 1) >> 1;
        if (packedBytes.Length != expectedPackedLength)
        {
            throw new SchematicException(
                SchematicErrorCodes.InvalidLegacyMetadata,
                $"Packed nibble array length ({packedBytes.Length}) does not match expected length ({expectedPackedLength}) for {expectedBlockCount} blocks.");
        }

        byte[] metadata = new byte[expectedBlockCount];
        for (int index = 0; index < expectedBlockCount; index++)
        {
            int b = packedBytes[index >> 1] & 0xFF;
            metadata[index] = (byte)((index & 1) == 0 ? (b & 0x0F) : ((b >> 4) & 0x0F));
        }

        return metadata;
    }
}
