using NEXUS3D.Domain.Voxels;

namespace NEXUS3D.Formats.Schematic.BlockStates;

/// <summary>
/// Registry of known canonical <see cref="BlockState"/> definitions and air aliases.
/// </summary>
public sealed class BlockStateRegistry
{
    public static BlockState Air => new("minecraft", "air");
    public static BlockState Stone => new("minecraft", "stone");

    public static bool IsAirState(BlockState? state) =>
        state is null || state.IsAir;
}

