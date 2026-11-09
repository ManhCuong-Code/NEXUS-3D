namespace NEXUS3D.Formats.Schematic.Nbt.Tags;

/// <summary>
/// Represents TAG_Byte (1), a signed 8-bit integer (sbyte) or boolean flag (0/1).
/// </summary>
public sealed class NbtByteTag : NbtTag
{
    public sbyte Value { get; set; }

    public byte UnsignedValue
    {
        get => unchecked((byte)Value);
        set => Value = unchecked((sbyte)value);
    }

    public override NbtTagType Type => NbtTagType.Byte;

    public NbtByteTag(string name, sbyte value)
        : base(name)
    {
        Value = value;
    }

    public NbtByteTag(string name, byte unsignedValue)
        : base(name)
    {
        Value = unchecked((sbyte)unsignedValue);
    }

    public override NbtTag DeepClone() => new NbtByteTag(Name, Value);

    public override bool ValueEquals(NbtTag? other) =>
        other is NbtByteTag b && b.Value == Value;
}
