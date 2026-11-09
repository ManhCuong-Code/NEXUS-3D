namespace NEXUS3D.Formats.Schematic.Nbt.Tags;

/// <summary>
/// Represents TAG_List (9), an ordered list of unnamed homogeneous <see cref="NbtTag"/> elements.
/// </summary>
public sealed class NbtListTag : NbtTag
{
    private readonly List<NbtTag> _items = [];

    public NbtTagType ElementType { get; private set; }

    public IReadOnlyList<NbtTag> Items => _items;

    public int Count => _items.Count;

    public override NbtTagType Type => NbtTagType.List;

    public NbtListTag(string name, NbtTagType elementType = NbtTagType.End)
        : base(name)
    {
        ElementType = elementType;
    }

    public NbtListTag(string name, NbtTagType elementType, IEnumerable<NbtTag> items)
        : base(name)
    {
        ElementType = elementType;
        ArgumentNullException.ThrowIfNull(items);
        foreach (var item in items)
        {
            Add(item);
        }
    }

    public void Add(NbtTag item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Type == NbtTagType.End)
        {
            throw new NbtException(NbtErrorCodes.InvalidNbt, "Cannot add TAG_End to an NbtListTag.");
        }

        if (ElementType == NbtTagType.End && _items.Count == 0)
        {
            ElementType = item.Type;
        }
        else if (item.Type != ElementType)
        {
            throw new NbtException(
                NbtErrorCodes.InvalidNbt,
                $"NbtListTag '{Name}' requires elements of type '{ElementType}', but received '{item.Type}'.");
        }

        item.Name = string.Empty;
        _items.Add(item);
    }

    public NbtTag this[int index] => _items[index];

    public override NbtTag DeepClone()
    {
        var clone = new NbtListTag(Name, ElementType);
        for (int i = 0; i < _items.Count; i++)
        {
            clone.Add(_items[i].DeepClone());
        }
        return clone;
    }

    public override bool ValueEquals(NbtTag? other)
    {
        if (other is not NbtListTag list || list.Count != Count)
        {
            return false;
        }

        if (Count > 0 && list.ElementType != ElementType)
        {
            return false;
        }

        for (int i = 0; i < _items.Count; i++)
        {
            if (!_items[i].ValueEquals(list._items[i]))
            {
                return false;
            }
        }

        return true;
    }
}

