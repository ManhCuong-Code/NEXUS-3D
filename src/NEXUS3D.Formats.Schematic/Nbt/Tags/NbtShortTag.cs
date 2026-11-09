namespace NEXUS3D.Formats.Schematic.Nbt.Tags;

/// <summary>
/// Represents TAG_Short (2), a signed 16-bit integer.
/// </summary>
public sealed class NbtShortTag : NbtTag
{
    public short Value { get; set; }

    public override NbtTagType Type => NbtTagType.Short;

    public NbtShortTag(string name, short value)
        : base(name)
    {
        Value = value;
    }

    public override NbtTag DeepClone() => new NbtShortTag(Name, Value);

    public override bool ValueEquals(NbtTag? other) =>
        other is NbtShortTag s && s.Value == Value;
}

