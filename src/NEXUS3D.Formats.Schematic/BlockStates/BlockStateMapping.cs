using NEXUS3D.Domain.Voxels;

namespace NEXUS3D.Formats.Schematic.BlockStates;

/// <summary>
/// Describes a mapping between a legacy numeric ID/metadata pair and a modern <see cref="BlockState"/>.
/// </summary>
public sealed record BlockStateMapping(
    int LegacyBlockId,
    int LegacyMetadata,
    BlockState ModernBlockState,
    bool IsExactMatch = true);
