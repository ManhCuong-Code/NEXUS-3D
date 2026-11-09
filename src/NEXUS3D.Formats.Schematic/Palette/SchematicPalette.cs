using NEXUS3D.Domain.Voxels;
using NEXUS3D.Formats.Schematic.BlockStates;
using NEXUS3D.Formats.Schematic.Common;

namespace NEXUS3D.Formats.Schematic.Palette;

/// <summary>
/// Bidirectional lookup table between integer palette indices and <see cref="BlockState"/> definitions.
/// Supports non-contiguous indices (as permitted in Sponge v2/v3) as well as contiguous re-indexing on export.
/// </summary>
public sealed class SchematicPalette
{
    private readonly Dictionary<int, SchematicPaletteEntry> _byIndex = [];
    private readonly Dictionary<string, int> _byCanonicalState = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<int, SchematicPaletteEntry> EntriesByIndex => _byIndex;
    public int Count => _byIndex.Count;
    public int MaxIndex => _byIndex.Count == 0 ? 0 : _byIndex.Keys.Max();

    public void SetEntry(int index, string rawStateString, BlockState state, int? legacyId = null, int? legacyMeta = null)
    {
        if (index < 0)
        {
            throw new SchematicException(
                SchematicErrorCodes.InvalidPalette,
                $"Palette index ({index}) for '{rawStateString}' cannot be negative.");
        }

        string canonical = BlockStateFormatter.Format(state);
        var entry = new SchematicPaletteEntry(index, rawStateString, state.DeepClone(), legacyId, legacyMeta);
        _byIndex[index] = entry;
        _byCanonicalState[canonical] = index;
    }

    public int GetOrAdd(BlockState state, int? legacyId = null, int? legacyMeta = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        string canonical = BlockStateFormatter.Format(state);
        if (_byCanonicalState.TryGetValue(canonical, out int existingIdx))
        {
            return existingIdx;
        }

        int nextIdx = _byIndex.Count == 0 ? 0 : MaxIndex + 1;
        while (_byIndex.ContainsKey(nextIdx))
        {
            nextIdx++;
        }

        SetEntry(nextIdx, canonical, state, legacyId, legacyMeta);
        return nextIdx;
    }

    public bool TryGetByIndex(int index, out SchematicPaletteEntry? entry) =>
        _byIndex.TryGetValue(index, out entry);

    public bool ContainsIndex(int index) =>
        _byIndex.ContainsKey(index);

    public SchematicPalette DeepClone()
    {
        var clone = new SchematicPalette();
        foreach (var entry in _byIndex.Values)
        {
            clone.SetEntry(entry.Index, entry.RawStateString, entry.BlockState.DeepClone(), entry.LegacyBlockId, entry.LegacyMetadata);
        }
        return clone;
    }
}
