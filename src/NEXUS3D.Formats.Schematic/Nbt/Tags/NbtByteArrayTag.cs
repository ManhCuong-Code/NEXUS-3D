namespace NEXUS3D.Formats.Schematic.Nbt.Tags;

/// <summary>
/// Represents TAG_Byte_Array (7), a length-prefixed array of bytes.
/// </summary>
public sealed class NbtByteArrayTag : NbtTag
{
    public byte[] Value { get; set; }

    public override NbtTagType Type => NbtTagType.ByteArray;

    public NbtByteArrayTag(string name, byte[] value)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    public override NbtTag DeepClone()
    {
        var copy = new byte[Value.Length];
        Buffer.BlockCopy(Value, 0, copy, 0, Value.Length);
        return new NbtByteArrayTag(Name, copy);
    }

    public override bool ValueEquals(NbtTag? other) =>
        other is NbtByteArrayTag ba && Value.AsSpan().SequenceEqual(ba.Value);
}

