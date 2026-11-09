namespace NEXUS3D.Formats.Schematic.Nbt.Tags;

/// <summary>
/// Represents TAG_Long (4), a signed 64-bit integer.
/// </summary>
public sealed class NbtLongTag : NbtTag
{
    public long Value { get; set; }

    public override NbtTagType Type => NbtTagType.Long;

    public NbtLongTag(string name, long value)
        : base(name)
    {
        Value = value;
    }

    public override NbtTag DeepClone() => new NbtLongTag(Name, Value);

    public override bool ValueEquals(NbtTag? other) =>
        other is NbtLongTag l && l.Value == Value;
}

