using NEXUS3D.Formats.Schematic.BlockStates;
using NEXUS3D.Formats.Schematic.Common;
using NEXUS3D.Formats.Schematic.Nbt.Tags;

namespace NEXUS3D.Formats.Schematic.Palette;

/// <summary>
/// Decodes an NBT <c>Palette</c> compound tag and VarInt <c>BlockData</c> byte array into a validated
/// <see cref="SchematicPalette"/> and linear block index array.
/// </summary>
public sealed class PaletteDecoder
{
    private readonly IVarIntDecoder _varIntDecoder;

    public PaletteDecoder(IVarIntDecoder? varIntDecoder = null)
    {
        _varIntDecoder = varIntDecoder ?? VarIntDecoder.Instance;
    }

    public SchematicPalette DecodePaletteCompound(NbtCompoundTag paletteCompound)
    {
        ArgumentNullException.ThrowIfNull(paletteCompound);

        if (paletteCompound.Count == 0)
        {
            throw new SchematicException(
                SchematicErrorCodes.InvalidPalette,
                "Schematic Palette compound tag is empty.");
        }

        var palette = new SchematicPalette();
        var usedIndices = new HashSet<int>();

        foreach (var tag in paletteCompound.GetAllTags())
        {
            if (tag is not NbtIntTag intTag)
            {
                throw new SchematicException(
                    SchematicErrorCodes.InvalidPalette,
                    $"Palette entry '{tag.Name}' must be TAG_Int, but found '{tag.Type}'.");
            }

            if (intTag.Value < 0)
            {
                throw new SchematicException(
                    SchematicErrorCodes.InvalidPalette,
                    $"Palette entry '{tag.Name}' has negative index {intTag.Value}.");
            }

            if (!usedIndices.Add(intTag.Value))
            {
                throw new SchematicException(
                    SchematicErrorCodes.InvalidPalette,
                    $"Duplicate palette index {intTag.Value} in Palette compound.");
            }

            var blockState = BlockStateParser.Parse(intTag.Name);
            palette.SetEntry(intTag.Value, intTag.Name, blockState);
        }

        return palette;
    }

    public int[] DecodeBlockData(ReadOnlySpan<byte> blockDataBytes, SchematicDimensions dimensions, SchematicPalette palette)
    {
        int expectedCount = dimensions.GetCheckedVolume();
        if (blockDataBytes.Length == 0 && expectedCount > 0)
        {
            throw new SchematicException(
                SchematicErrorCodes.InvalidBlockData,
                "BlockData byte array is empty for non-empty schematic dimensions.");
        }

        var indices = new int[expectedCount];
        int offset = 0;

        for (int i = 0; i < expectedCount; i++)
        {
            if (offset >= blockDataBytes.Length)
            {
                throw new SchematicException(
                    SchematicErrorCodes.InvalidBlockData,
                    $"BlockData ended prematurely at block {i} of {expectedCount}.");
            }

            int paletteIdx = _varIntDecoder.ReadVarInt(blockDataBytes, ref offset);
            if (!palette.ContainsIndex(paletteIdx))
            {
                throw new SchematicException(
                    SchematicErrorCodes.InvalidPalette,
                    $"Block at linear index {i} references non-existent palette index {paletteIdx}.");
            }

            indices[i] = paletteIdx;
        }

        if (offset < blockDataBytes.Length)
        {
            throw new SchematicException(
                SchematicErrorCodes.InvalidBlockData,
                $"BlockData contains {blockDataBytes.Length - offset} unexpected trailing bytes after reading all {expectedCount} blocks.");
        }

        return indices;
    }
}

