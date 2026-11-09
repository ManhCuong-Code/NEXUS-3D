using NEXUS3D.Domain.Interfaces;

namespace NEXUS3D.Formats.Schematic.Nbt.Tags;

/// <summary>
/// Standard Minecraft Named Binary Tag (NBT) identifiers (0..12).
/// </summary>
public enum NbtTagType : byte
{
    End = 0,
    Byte = 1,
    Short = 2,
    Int = 3,
    Long = 4,
    Float = 5,
    Double = 6,
    ByteArray = 7,
    String = 8,
    List = 9,
    Compound = 10,
    IntArray = 11,
    LongArray = 12
}

/// <summary>
/// Abstract base class for all 13 Named Binary Tag (NBT) nodes.
/// </summary>
public abstract class NbtTag : IDeepCloneable<NbtTag>
{
    public string Name { get; set; }

    public abstract NbtTagType Type { get; }

    protected NbtTag(string name)
    {
        Name = name ?? string.Empty;
    }

    public abstract NbtTag DeepClone();

    public abstract bool ValueEquals(NbtTag? other);
}

