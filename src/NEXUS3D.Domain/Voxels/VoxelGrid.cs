using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Interfaces;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Validation;

namespace NEXUS3D.Domain.Voxels;

/// <summary>
/// Pluggable storage backend abstraction for <see cref="VoxelGrid"/>.
/// Allows switching between Sparse Dictionary, Chunked 16x16x16, Dense Array, or Compressed storage.
/// </summary>
public interface IVoxelStorageBackend : IDeepCloneable<IVoxelStorageBackend>
{
    int Count { get; }
    IReadOnlyCollection<Voxel> Voxels { get; }
    bool TryGet(VoxelPosition position, out Voxel? voxel);
    void Set(Voxel voxel);
    bool Remove(VoxelPosition position);
    void Clear();
}

/// <summary>
/// Memory-efficient sparse storage backend for sparse and medium-density voxel structures.
/// </summary>
public sealed class SparseVoxelStorageBackend : IVoxelStorageBackend
{
    private readonly Dictionary<VoxelPosition, Voxel> _storage;

    public SparseVoxelStorageBackend(int initialCapacity = 0)
    {
        _storage = initialCapacity > 0
            ? new Dictionary<VoxelPosition, Voxel>(initialCapacity)
            : new Dictionary<VoxelPosition, Voxel>();
    }

    public int Count => _storage.Count;

    public IReadOnlyCollection<Voxel> Voxels => _storage.Values;

    public bool TryGet(VoxelPosition position, out Voxel? voxel) =>
        _storage.TryGetValue(position, out voxel);

    public void Set(Voxel voxel)
    {
        DomainGuard.NotNull(voxel);
        _storage[voxel.Position] = voxel;
    }

    public bool Remove(VoxelPosition position) =>
        _storage.Remove(position);

    public void Clear() => _storage.Clear();

    public IVoxelStorageBackend DeepClone()
    {
        var clone = new SparseVoxelStorageBackend(_storage.Count);
        foreach (var kvp in _storage)
        {
            clone._storage[kvp.Key] = kvp.Value.DeepClone();
        }

        return clone;
    }
}

/// <summary>
/// Spatial chunk-based (16x16x16) storage backend optimized for large-scale Minecraft structures,
/// dense voxelizations, and streaming spatial partitions.
/// </summary>
public sealed class ChunkedVoxelStorageBackend : IVoxelStorageBackend
{
    public const int ChunkSize = 16;
    public const int ChunkMask = ChunkSize - 1;

    private readonly record struct ChunkKey(int X, int Y, int Z);

    private readonly Dictionary<ChunkKey, Dictionary<int, Voxel>> _chunks = new();
    private int _count;

    public int Count => _count;

    public IReadOnlyCollection<Voxel> Voxels
    {
        get
        {
            var list = new List<Voxel>(_count);
            foreach (var chunk in _chunks.Values)
            {
                list.AddRange(chunk.Values);
            }
            return list;
        }
    }

    public bool TryGet(VoxelPosition position, out Voxel? voxel)
    {
        int cx = position.X >> 4;
        int cy = position.Y >> 4;
        int cz = position.Z >> 4;

        if (_chunks.TryGetValue(new ChunkKey(cx, cy, cz), out var chunk))
        {
            int localKey = (position.X & ChunkMask) | ((position.Y & ChunkMask) << 4) | ((position.Z & ChunkMask) << 8);
            return chunk.TryGetValue(localKey, out voxel);
        }

        voxel = null;
        return false;
    }

    public void Set(Voxel voxel)
    {
        DomainGuard.NotNull(voxel);
        int cx = voxel.Position.X >> 4;
        int cy = voxel.Position.Y >> 4;
        int cz = voxel.Position.Z >> 4;

        var key = new ChunkKey(cx, cy, cz);
        if (!_chunks.TryGetValue(key, out var chunk))
        {
            chunk = new Dictionary<int, Voxel>();
            _chunks[key] = chunk;
        }

        int localKey = (voxel.Position.X & ChunkMask) | ((voxel.Position.Y & ChunkMask) << 4) | ((voxel.Position.Z & ChunkMask) << 8);
        if (!chunk.ContainsKey(localKey))
        {
            _count++;
        }
        chunk[localKey] = voxel;
    }

    public bool Remove(VoxelPosition position)
    {
        int cx = position.X >> 4;
        int cy = position.Y >> 4;
        int cz = position.Z >> 4;

        var key = new ChunkKey(cx, cy, cz);
        if (_chunks.TryGetValue(key, out var chunk))
        {
            int localKey = (position.X & ChunkMask) | ((position.Y & ChunkMask) << 4) | ((position.Z & ChunkMask) << 8);
            if (chunk.Remove(localKey))
            {
                _count--;
                if (chunk.Count == 0)
                {
                    _chunks.Remove(key);
                }
                return true;
            }
        }

        return false;
    }

    public void Clear()
    {
        _chunks.Clear();
        _count = 0;
    }

    public IVoxelStorageBackend DeepClone()
    {
        var clone = new ChunkedVoxelStorageBackend();
        foreach (var (key, chunk) in _chunks)
        {
            var chunkClone = new Dictionary<int, Voxel>(chunk.Count);
            foreach (var (localKey, voxel) in chunk)
            {
                chunkClone[localKey] = voxel.DeepClone();
            }
            clone._chunks[key] = chunkClone;
        }
        clone._count = _count;
        return clone;
    }
}

/// <summary>
/// Represents a 3D grid of voxels with palette indexing, pluggable storage backend, layers, and validation.
/// </summary>
public sealed class VoxelGrid : Entity, ISceneComponent, IDeepCloneable<VoxelGrid>
{
    private IVoxelStorageBackend _storage;

    public string Name { get; set; } = "VoxelGrid";
    public VoxelPosition Origin { get; set; } = VoxelPosition.Zero;
    public VoxelSize VoxelSize { get; set; } = VoxelSize.UnitCube;
    public VoxelGridBounds Bounds { get; set; } = VoxelGridBounds.Empty;
    public VoxelPalette Palette { get; set; } = new();
    public List<VoxelLayer> Layers { get; set; } = [];

    public IReadOnlyCollection<Voxel> Voxels => _storage.Voxels;

    public VoxelGrid()
    {
        _storage = new SparseVoxelStorageBackend();
    }

    public VoxelGrid(string name, IVoxelStorageBackend? storageBackend = null)
    {
        Name = DomainGuard.NotNullOrWhiteSpace(name);
        _storage = storageBackend ?? new SparseVoxelStorageBackend();
    }

    public bool TryGetVoxel(VoxelPosition position, out Voxel? voxel) =>
        _storage.TryGet(position, out voxel);

    public void SetVoxel(Voxel voxel)
    {
        DomainGuard.NotNull(voxel);
        _storage.Set(voxel);
        Bounds = Bounds.Encapsulate(voxel.Position);
    }

    public bool RemoveVoxel(VoxelPosition position) =>
        _storage.Remove(position);

    public int GetVoxelCount() => _storage.Count;

    public void RecalculateBounds()
    {
        VoxelGridBounds computed = VoxelGridBounds.Empty;
        foreach (var voxel in _storage.Voxels)
        {
            computed = computed.Encapsulate(voxel.Position);
        }

        Bounds = computed;
    }

    public BoundingBox GetWorldBounds()
    {
        if (!Bounds.IsValid || !VoxelSize.IsValid)
        {
            return BoundingBox.Empty;
        }

        Vector3D min = (Origin + Bounds.Min).ToWorldPosition(VoxelSize);
        Vector3D max = (Origin + Bounds.Max + VoxelPosition.One).ToWorldPosition(VoxelSize);
        return new BoundingBox(Vector3D.Min(min, max), Vector3D.Max(min, max));
    }

    public ValidationResult Validate() => new VoxelGridValidator().Validate(this);

    public VoxelGrid DeepClone()
    {
        var clone = new VoxelGrid(Name, _storage.DeepClone())
        {
            Id = Id,
            Origin = Origin,
            VoxelSize = VoxelSize,
            Bounds = Bounds,
            Palette = Palette.DeepClone(),
            Layers = Layers.Select(l => l.DeepClone()).ToList()
        };

        return clone;
    }
}

