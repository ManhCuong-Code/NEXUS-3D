using NEXUS3D.Formats.Schematic.Nbt.Tags;

namespace NEXUS3D.Formats.Schematic.Nbt.Utilities;

/// <summary>
/// Fluent builder extensions for populating <see cref="NbtCompoundTag"/> instances.
/// </summary>
public static class NbtTagWriterExtensions
{
    public static NbtCompoundTag PutByte(this NbtCompoundTag compound, string name, sbyte value)
    {
        compound.SetTag(new NbtByteTag(name, value));
        return compound;
    }

    public static NbtCompoundTag PutShort(this NbtCompoundTag compound, string name, short value)
    {
        compound.SetTag(new NbtShortTag(name, value));
        return compound;
    }

    public static NbtCompoundTag PutInt(this NbtCompoundTag compound, string name, int value)
    {
        compound.SetTag(new NbtIntTag(name, value));
        return compound;
    }

    public static NbtCompoundTag PutLong(this NbtCompoundTag compound, string name, long value)
    {
        compound.SetTag(new NbtLongTag(name, value));
        return compound;
    }

    public static NbtCompoundTag PutFloat(this NbtCompoundTag compound, string name, float value)
    {
        compound.SetTag(new NbtFloatTag(name, value));
        return compound;
    }

    public static NbtCompoundTag PutDouble(this NbtCompoundTag compound, string name, double value)
    {
        compound.SetTag(new NbtDoubleTag(name, value));
        return compound;
    }

    public static NbtCompoundTag PutString(this NbtCompoundTag compound, string name, string value)
    {
        compound.SetTag(new NbtStringTag(name, value));
        return compound;
    }

    public static NbtCompoundTag PutByteArray(this NbtCompoundTag compound, string name, byte[] value)
    {
        compound.SetTag(new NbtByteArrayTag(name, value));
        return compound;
    }

    public static NbtCompoundTag PutIntArray(this NbtCompoundTag compound, string name, int[] value)
    {
        compound.SetTag(new NbtIntArrayTag(name, value));
        return compound;
    }

    public static NbtCompoundTag PutLongArray(this NbtCompoundTag compound, string name, long[] value)
    {
        compound.SetTag(new NbtLongArrayTag(name, value));
        return compound;
    }
}
