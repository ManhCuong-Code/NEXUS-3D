using NEXUS3D.Domain.Voxels;
using NEXUS3D.Formats.Schematic.BlockStates;

namespace NEXUS3D.Formats.Schematic.Palette;

/// <summary>
/// Maps between <see cref="SchematicPalette"/> and USM <see cref="VoxelPalette"/> without losing block state properties.
/// </summary>
public sealed class PaletteMappingService
{
    public (VoxelPalette UsmPalette, Dictionary<int, int> SchematicToUsmIndexMap) MapToUsmPalette(SchematicPalette schematicPalette)
    {
        ArgumentNullException.ThrowIfNull(schematicPalette);

        var usmPalette = new VoxelPalette();
        var indexMap = new Dictionary<int, int>();

        foreach (var entry in schematicPalette.EntriesByIndex.Values.OrderBy(e => e.Index))
        {
            int usmIdx = usmPalette.GetOrAdd(entry.BlockState.DeepClone());
            indexMap[entry.Index] = usmIdx;
        }

        return (usmPalette, indexMap);
    }

    public (SchematicPalette SchematicPalette, Dictionary<int, int> UsmToSchematicIndexMap) MapFromUsmPalette(VoxelPalette usmPalette)
    {
        ArgumentNullException.ThrowIfNull(usmPalette);

        var schematicPalette = new SchematicPalette();
        var indexMap = new Dictionary<int, int>();

        // Ensure air (index 0) is present as the default background entry
        int airIdx = schematicPalette.GetOrAdd(BlockStateRegistry.Air);

        foreach (var entry in usmPalette.Entries)
        {
            int schemIdx = schematicPalette.GetOrAdd(entry.State.DeepClone());
            indexMap[entry.Index] = schemIdx;
        }

        return (schematicPalette, indexMap);
    }
}
