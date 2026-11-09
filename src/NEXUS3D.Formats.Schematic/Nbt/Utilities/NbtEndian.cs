using System.Buffers.Binary;

namespace NEXUS3D.Formats.Schematic.Nbt.Utilities;

/// <summary>
/// Byte order convention for Named Binary Tag (NBT) streams.
/// Java Edition Schematics (Sponge v2, Sponge v3, Legacy MCEdit) strictly use <see cref="BigEndian"/>.
/// </summary>
public enum NbtEndianness
{
    BigEndian = 0,
    LittleEndian = 1
}

/// <summary>
/// Zero-allocation endian-aware primitive conversion helpers for NBT binary streams.
/// </summary>
public static class NbtEndian
{
    public static short ReadInt16(ReadOnlySpan<byte> buffer, NbtEndianness endian) =>
        endian == NbtEndianness.BigEndian
            ? BinaryPrimitives.ReadInt16BigEndian(buffer)
            : BinaryPrimitives.ReadInt16LittleEndian(buffer);

    public static ushort ReadUInt16(ReadOnlySpan<byte> buffer, NbtEndianness endian) =>
        endian == NbtEndianness.BigEndian
            ? BinaryPrimitives.ReadUInt16BigEndian(buffer)
            : BinaryPrimitives.ReadUInt16LittleEndian(buffer);

    public static int ReadInt32(ReadOnlySpan<byte> buffer, NbtEndianness endian) =>
        endian == NbtEndianness.BigEndian
            ? BinaryPrimitives.ReadInt32BigEndian(buffer)
            : BinaryPrimitives.ReadInt32LittleEndian(buffer);

    public static long ReadInt64(ReadOnlySpan<byte> buffer, NbtEndianness endian) =>
        endian == NbtEndianness.BigEndian
            ? BinaryPrimitives.ReadInt64BigEndian(buffer)
            : BinaryPrimitives.ReadInt64LittleEndian(buffer);

    public static float ReadSingle(ReadOnlySpan<byte> buffer, NbtEndianness endian) =>
        endian == NbtEndianness.BigEndian
            ? BinaryPrimitives.ReadSingleBigEndian(buffer)
            : BinaryPrimitives.ReadSingleLittleEndian(buffer);

    public static double ReadDouble(ReadOnlySpan<byte> buffer, NbtEndianness endian) =>
        endian == NbtEndianness.BigEndian
            ? BinaryPrimitives.ReadDoubleBigEndian(buffer)
            : BinaryPrimitives.ReadDoubleLittleEndian(buffer);

    public static void WriteInt16(Span<byte> buffer, short value, NbtEndianness endian)
    {
        if (endian == NbtEndianness.BigEndian)
            BinaryPrimitives.WriteInt16BigEndian(buffer, value);
        else
            BinaryPrimitives.WriteInt16LittleEndian(buffer, value);
    }

    public static void WriteUInt16(Span<byte> buffer, ushort value, NbtEndianness endian)
    {
        if (endian == NbtEndianness.BigEndian)
            BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
        else
            BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
    }

    public static void WriteInt32(Span<byte> buffer, int value, NbtEndianness endian)
    {
        if (endian == NbtEndianness.BigEndian)
            BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        else
            BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
    }

    public static void WriteInt64(Span<byte> buffer, long value, NbtEndianness endian)
    {
        if (endian == NbtEndianness.BigEndian)
            BinaryPrimitives.WriteInt64BigEndian(buffer, value);
        else
            BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
    }

    public static void WriteSingle(Span<byte> buffer, float value, NbtEndianness endian)
    {
        if (endian == NbtEndianness.BigEndian)
            BinaryPrimitives.WriteSingleBigEndian(buffer, value);
        else
            BinaryPrimitives.WriteSingleLittleEndian(buffer, value);
    }

    public static void WriteDouble(Span<byte> buffer, double value, NbtEndianness endian)
    {
        if (endian == NbtEndianness.BigEndian)
            BinaryPrimitives.WriteDoubleBigEndian(buffer, value);
        else
            BinaryPrimitives.WriteDoubleLittleEndian(buffer, value);
    }
}

