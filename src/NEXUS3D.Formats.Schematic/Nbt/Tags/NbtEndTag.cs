namespace NEXUS3D.Formats.Schematic.Nbt.Tags;

/// <summary>
/// Represents TAG_End (0) marking the end of a compound tag or an empty untyped list.
/// </summary>
public sealed class NbtEndTag : NbtTag
{
    public static NbtEndTag Instance { get; } = new();

    public override NbtTagType Type => NbtTagType.End;

    public NbtEndTag()
        : base(string.Empty)
    {
    }

    public override NbtTag DeepClone() => new NbtEndTag();

    public override bool ValueEquals(NbtTag? other) => other is NbtEndTag;
}

