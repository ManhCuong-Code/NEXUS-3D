namespace NEXUS3D.Formats.Schematic.Nbt.Tags;

/// <summary>
/// Represents TAG_Double (6), a 64-bit IEEE 754 floating-point number.
/// </summary>
public sealed class NbtDoubleTag : NbtTag
{
    public double Value { get; set; }

    public override NbtTagType Type => NbtTagType.Double;

    public NbtDoubleTag(string name, double value)
        : base(name)
    {
        Value = value;
    }

    public override NbtTag DeepClone() => new NbtDoubleTag(Name, Value);

    public override bool ValueEquals(NbtTag? other) =>
        other is NbtDoubleTag d && BitConverter.DoubleToInt64Bits(d.Value) == BitConverter.DoubleToInt64Bits(Value);
}

