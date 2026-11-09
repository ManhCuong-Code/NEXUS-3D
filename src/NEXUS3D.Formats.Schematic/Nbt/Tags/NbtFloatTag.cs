namespace NEXUS3D.Formats.Schematic.Nbt.Tags;

/// <summary>
/// Represents TAG_Float (5), a 32-bit IEEE 754 floating-point number.
/// </summary>
public sealed class NbtFloatTag : NbtTag
{
    public float Value { get; set; }

    public override NbtTagType Type => NbtTagType.Float;

    public NbtFloatTag(string name, float value)
        : base(name)
    {
        Value = value;
    }

    public override NbtTag DeepClone() => new NbtFloatTag(Name, Value);

    public override bool ValueEquals(NbtTag? other) =>
        other is NbtFloatTag f && BitConverter.SingleToInt32Bits(f.Value) == BitConverter.SingleToInt32Bits(Value);
}

