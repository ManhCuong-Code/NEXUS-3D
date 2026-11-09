using NEXUS3D.Formats.Schematic.BlockStates;
using NEXUS3D.Formats.Schematic.Common;
using NEXUS3D.Formats.Schematic.Nbt.Tags;

namespace NEXUS3D.Formats.Schematic.Palette;

/// <summary>
/// Encodes a <see cref="SchematicPalette"/> and linear block index array into an NBT <c>Palette</c> compound
/// and a VarInt-compressed <c>BlockData</c> byte array.
/// </summary>
public sealed class PaletteEncoder
{
    private readonly IVarIntEncoder _varIntEncoder;

    public PaletteEncoder(IVarIntEncoder? varIntEncoder = null)
    {
        _varIntEncoder = varIntEncoder ?? VarIntEncoder.Instance;
    }

    public NbtCompoundTag EncodePaletteCompound(SchematicPalette palette, string tagName = "Palette")
    {
        ArgumentNullException.ThrowIfNull(palette);
        if (palette.Count == 0)
        {
            throw new SchematicException(
                SchematicErrorCodes.InvalidPalette,
                "Cannot encode an empty SchematicPalette.");
        }

        var compound = new NbtCompoundTag(tagName);
        foreach (var entry in palette.EntriesByIndex.Values.OrderBy(e => e.Index))
        {
            string stateKey = !string.IsNullOrWhiteSpace(entry.RawStateString)
                ? entry.RawStateString
                : BlockStateFormatter.Format(entry.BlockState);
            compound.SetTag(new NbtIntTag(stateKey, entry.Index));
        }

        return compound;
    }

    public byte[] EncodeBlockData(ReadOnlySpan<int> blockIndices, SchematicPalette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        using var memory = new MemoryStream(Math.Max(16, blockIndices.Length));

        for (int i = 0; i < blockIndices.Length; i++)
        {
            int idx = blockIndices[i];
            if (idx < 0 || !palette.ContainsIndex(idx))
            {
                throw new SchematicException(
                    SchematicErrorCodes.InvalidPalette,
                    $"Block at linear index {i} references undefined palette index {idx}.");
            }

            _varIntEncoder.WriteVarInt(idx, memory);
        }

        return memory.ToArray();
    }
}

