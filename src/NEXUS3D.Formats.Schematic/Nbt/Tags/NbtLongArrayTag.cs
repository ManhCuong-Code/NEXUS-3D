namespace NEXUS3D.Formats.Schematic.Nbt.Tags;

/// <summary>
/// Represents TAG_Long_Array (12), a length-prefixed array of signed 64-bit integers.
/// </summary>
public sealed class NbtLongArrayTag : NbtTag
{
    public long[] Value { get; set; }

    public override NbtTagType Type => NbtTagType.LongArray;

    public NbtLongArrayTag(string name, long[] value)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    public override NbtTag DeepClone()
    {
        var copy = new long[Value.Length];
        Array.Copy(Value, copy, Value.Length);
        return new NbtLongArrayTag(Name, copy);
    }

    public override bool ValueEquals(NbtTag? other) =>
        other is NbtLongArrayTag la && Value.AsSpan().SequenceEqual(la.Value);
}

