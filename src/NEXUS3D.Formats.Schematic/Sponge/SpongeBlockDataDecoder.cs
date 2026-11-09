using NEXUS3D.Formats.Schematic.Common;
using NEXUS3D.Formats.Schematic.Palette;

namespace NEXUS3D.Formats.Schematic.Sponge;

/// <summary>
/// Decodes Sponge v2 (<c>BlockData</c>) and Sponge v3 (<c>Blocks.Data</c>) VarInt byte arrays into linear block indices.
/// </summary>
public sealed class SpongeBlockDataDecoder
{
    private readonly PaletteDecoder _paletteDecoder;

    public SpongeBlockDataDecoder(PaletteDecoder? paletteDecoder = null)
    {
        _paletteDecoder = paletteDecoder ?? new PaletteDecoder();
    }

    public int[] Decode(ReadOnlySpan<byte> blockData, SchematicDimensions dimensions, SchematicPalette palette) =>
        _paletteDecoder.DecodeBlockData(blockData, dimensions, palette);
}

