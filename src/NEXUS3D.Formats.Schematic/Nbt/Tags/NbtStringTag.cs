namespace NEXUS3D.Formats.Schematic.Nbt.Tags;

/// <summary>
/// Represents TAG_String (8), a UTF-8 encoded string prefixed with a 16-bit unsigned byte length.
/// </summary>
public sealed class NbtStringTag : NbtTag
{
    public string Value { get; set; }

    public override NbtTagType Type => NbtTagType.String;

    public NbtStringTag(string name, string value)
        : base(name)
    {
        Value = value ?? string.Empty;
    }

    public override NbtTag DeepClone() => new NbtStringTag(Name, Value);

    public override bool ValueEquals(NbtTag? other) =>
        other is NbtStringTag s && string.Equals(Value, s.Value, StringComparison.Ordinal);
}
