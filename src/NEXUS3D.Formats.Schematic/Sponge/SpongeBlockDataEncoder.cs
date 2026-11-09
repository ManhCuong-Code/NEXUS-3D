using NEXUS3D.Formats.Schematic.Palette;

namespace NEXUS3D.Formats.Schematic.Sponge;

/// <summary>
/// Encodes linear block index arrays into VarInt byte arrays for Sponge v2 (<c>BlockData</c>) and Sponge v3 (<c>Blocks.Data</c>).
/// </summary>
public sealed class SpongeBlockDataEncoder
{
    private readonly PaletteEncoder _paletteEncoder;

    public SpongeBlockDataEncoder(PaletteEncoder? paletteEncoder = null)
    {
        _paletteEncoder = paletteEncoder ?? new PaletteEncoder();
    }

    public byte[] Encode(ReadOnlySpan<int> blockIndices, SchematicPalette palette) =>
        _paletteEncoder.EncodeBlockData(blockIndices, palette);
}
