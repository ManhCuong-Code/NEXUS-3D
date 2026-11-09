using NEXUS3D.Domain.Voxels;

namespace NEXUS3D.Formats.Schematic.Palette;

/// <summary>
/// Represents a single entry in a <see cref="SchematicPalette"/> mapping a palette index to a <see cref="BlockState"/>.
/// </summary>
public sealed record SchematicPaletteEntry(
    int Index,
    string RawStateString,
    BlockState BlockState,
    int? LegacyBlockId = null,
    int? LegacyMetadata = null);

