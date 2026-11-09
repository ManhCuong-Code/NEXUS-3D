using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Interfaces;

namespace NEXUS3D.Domain.Voxels;

/// <summary>
/// Entry in a <see cref="VoxelPalette"/> mapping an integer index to a <see cref="BlockState"/> and optional Material ID.
/// </summary>
public sealed class VoxelPaletteEntry : IDeepCloneable<VoxelPaletteEntry>
{
    public int Index { get; internal set; }
    public BlockState State { get; set; } = new();
    public Guid? MaterialId { get; set; }
    public string? DisplayName { get; set; }

    public VoxelPaletteEntry()
    {
    }

    public VoxelPaletteEntry(BlockState state, Guid? materialId = null, string? displayName = null)
    {
        State = DomainGuard.NotNull(state);
        MaterialId = materialId;
        DisplayName = displayName;
    }

    public VoxelPaletteEntry DeepClone() => new(State.DeepClone(), MaterialId, DisplayName)
    {
        Index = Index
    };
}

/// <summary>
/// Deduplicated lookup table of <see cref="BlockState"/> entries used by a <see cref="VoxelGrid"/>.
/// Never merges distinct block states solely because of visual color similarity.
/// </summary>
public sealed class VoxelPalette : IDeepCloneable<VoxelPalette>
{
    private readonly List<VoxelPaletteEntry> _entries = [];
    private readonly Dictionary<string, int> _canonicalKeyToIndex = new(StringComparer.Ordinal);

    public IReadOnlyList<VoxelPaletteEntry> Entries => _entries;
    public int Count => _entries.Count;

    public int Add(VoxelPaletteEntry entry)
    {
        DomainGuard.NotNull(entry);
        DomainGuard.NotNull(entry.State);

        string canonicalKey = entry.State.GetCanonicalStateKey();
        if (_canonicalKeyToIndex.TryGetValue(canonicalKey, out int existingIndex))
        {
            return existingIndex;
        }

        int newIndex = _entries.Count;
        entry.Index = newIndex;
        _entries.Add(entry);
        _canonicalKeyToIndex[canonicalKey] = newIndex;
        return newIndex;
    }

    public int GetOrAdd(BlockState state, Guid? materialId = null)
    {
        DomainGuard.NotNull(state);
        int existing = FindIndex(state);
        if (existing >= 0)
        {
            return existing;
        }

        return Add(new VoxelPaletteEntry(state, materialId));
    }

    public bool TryGet(int index, out VoxelPaletteEntry? entry)
    {
        if (index >= 0 && index < _entries.Count)
        {
            entry = _entries[index];
            return true;
        }

        entry = null;
        return false;
    }

    public int FindIndex(BlockState state)
    {
        if (state is null)
        {
            return -1;
        }

        return _canonicalKeyToIndex.TryGetValue(state.GetCanonicalStateKey(), out int index)
            ? index
            : -1;
    }

    public VoxelPalette DeepClone()
    {
        var clone = new VoxelPalette();
        foreach (var entry in _entries)
        {
            clone.Add(entry.DeepClone());
        }

        return clone;
    }
}

/// <summary>
/// Represents an individual populated voxel within a <see cref="VoxelGrid"/>.
/// Avoids duplicating <see cref="BlockState"/> when <see cref="PaletteIndex"/> references the grid palette.
/// </summary>
public sealed class Voxel : IDeepCloneable<Voxel>
{
    public VoxelPosition Position { get; set; }
    public int PaletteIndex { get; set; }
    public BlockState? State { get; set; }
    public Guid? MaterialId { get; set; }
    public BlockEntityData? BlockEntity { get; set; }

    public bool IsAir => State?.IsAir ?? false;

    public Voxel()
    {
    }

    public Voxel(
        VoxelPosition position,
        int paletteIndex,
        BlockState? state = null,
        Guid? materialId = null,
        BlockEntityData? blockEntity = null)
    {
        Position = position;
        PaletteIndex = paletteIndex;
        State = state;
        MaterialId = materialId;
        BlockEntity = blockEntity;
    }

    public Voxel DeepClone() => new(
        Position,
        PaletteIndex,
        State?.DeepClone(),
        MaterialId,
        BlockEntity?.DeepClone());
}

/// <summary>
/// Logical layer grouping voxel positions by editing group, region, or material pass.
/// Separate from the physical chunk/sparse storage backend.
/// </summary>
public sealed class VoxelLayer : Entity, ISceneComponent, IDeepCloneable<VoxelLayer>
{
    public string Name { get; set; } = "Default Layer";
    public bool IsVisible { get; set; } = true;
    public bool IsLocked { get; set; }
    public double Opacity { get; set; } = 1.0;
    public HashSet<VoxelPosition> Positions { get; set; } = [];

    public VoxelLayer()
    {
    }

    public VoxelLayer(string name)
    {
        Name = DomainGuard.NotNullOrWhiteSpace(name);
    }

    public VoxelLayer DeepClone() => new()
    {
        Id = Id,
        Name = Name,
        IsVisible = IsVisible,
        IsLocked = IsLocked,
        Opacity = Opacity,
        Positions = new HashSet<VoxelPosition>(Positions)
    };
}

