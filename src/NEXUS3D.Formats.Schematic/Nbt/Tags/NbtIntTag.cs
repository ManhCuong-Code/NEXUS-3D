namespace NEXUS3D.Formats.Schematic.Nbt.Tags;

/// <summary>
/// Represents TAG_Int (3), a signed 32-bit integer.
/// </summary>
public sealed class NbtIntTag : NbtTag
{
    public int Value { get; set; }

    public override NbtTagType Type => NbtTagType.Int;

    public NbtIntTag(string name, int value)
        : base(name)
    {
        Value = value;
    }

    public override NbtTag DeepClone() => new NbtIntTag(Name, Value);

    public override bool ValueEquals(NbtTag? other) =>
        other is NbtIntTag i && i.Value == Value;
}
