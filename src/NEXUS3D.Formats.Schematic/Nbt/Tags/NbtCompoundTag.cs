namespace NEXUS3D.Formats.Schematic.Nbt.Tags;

/// <summary>
/// Represents TAG_Compound (10), an ordered dictionary of named <see cref="NbtTag"/> entries.
/// Preserves all recognized and unknown tags in insertion order while providing O(1) lookup by name.
/// </summary>
public sealed class NbtCompoundTag : NbtTag
{
    private readonly List<NbtTag> _orderedTags = [];
    private readonly Dictionary<string, int> _nameToIndex = new(StringComparer.Ordinal);

    public int Count => _orderedTags.Count;

    public override NbtTagType Type => NbtTagType.Compound;

    public NbtCompoundTag(string name = "")
        : base(name)
    {
    }

    public NbtCompoundTag(string name, IEnumerable<NbtTag> tags)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(tags);
        foreach (var tag in tags)
        {
            SetTag(tag);
        }
    }

    public bool Contains(string name) =>
        name is not null && _nameToIndex.ContainsKey(name);

    public NbtTag? GetTag(string name)
    {
        if (name is not null && _nameToIndex.TryGetValue(name, out int idx))
        {
            return _orderedTags[idx];
        }

        return null;
    }

    public T? GetTag<T>(string name)
        where T : NbtTag =>
        GetTag(name) as T;

    public void SetTag(NbtTag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        if (tag.Type == NbtTagType.End)
        {
            throw new NbtException(NbtErrorCodes.InvalidNbt, "Cannot add TAG_End as a named entry in NbtCompoundTag.");
        }

        string key = tag.Name ?? string.Empty;
        if (_nameToIndex.TryGetValue(key, out int existingIdx))
        {
            _orderedTags[existingIdx] = tag;
        }
        else
        {
            _nameToIndex[key] = _orderedTags.Count;
            _orderedTags.Add(tag);
        }
    }

    public bool RemoveTag(string name)
    {
        if (name is null || !_nameToIndex.TryGetValue(name, out int idx))
        {
            return false;
        }

        _orderedTags.RemoveAt(idx);
        _nameToIndex.Remove(name);
        for (int i = idx; i < _orderedTags.Count; i++)
        {
            _nameToIndex[_orderedTags[i].Name] = i;
        }

        return true;
    }

    public IEnumerable<NbtTag> GetAllTags() => _orderedTags;

    public override NbtTag DeepClone() => CloneCompound();

    public NbtCompoundTag CloneCompound()
    {
        var clone = new NbtCompoundTag(Name);
        for (int i = 0; i < _orderedTags.Count; i++)
        {
            clone.SetTag(_orderedTags[i].DeepClone());
        }
        return clone;
    }

    public override bool ValueEquals(NbtTag? other)
    {
        if (other is not NbtCompoundTag comp || comp.Count != Count)
        {
            return false;
        }

        foreach (var tag in _orderedTags)
        {
            var otherTag = comp.GetTag(tag.Name);
            if (otherTag is null || otherTag.Type != tag.Type || !tag.ValueEquals(otherTag))
            {
                return false;
            }
        }

        return true;
    }
}
