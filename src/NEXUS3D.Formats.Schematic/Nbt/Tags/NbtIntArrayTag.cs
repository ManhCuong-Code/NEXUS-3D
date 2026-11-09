namespace NEXUS3D.Formats.Schematic.Nbt.Tags;

/// <summary>
/// Represents TAG_Int_Array (11), a length-prefixed array of signed 32-bit integers.
/// </summary>
public sealed class NbtIntArrayTag : NbtTag
{
    public int[] Value { get; set; }

    public override NbtTagType Type => NbtTagType.IntArray;

    public NbtIntArrayTag(string name, int[] value)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    public override NbtTag DeepClone()
    {
        var copy = new int[Value.Length];
        Array.Copy(Value, copy, Value.Length);
        return new NbtIntArrayTag(Name, copy);
    }

    public override bool ValueEquals(NbtTag? other) =>
        other is NbtIntArrayTag ia && Value.AsSpan().SequenceEqual(ia.Value);
}
