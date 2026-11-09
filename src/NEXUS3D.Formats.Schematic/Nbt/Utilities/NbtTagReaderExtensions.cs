using NEXUS3D.Formats.Schematic.Nbt.Tags;

namespace NEXUS3D.Formats.Schematic.Nbt.Utilities;

/// <summary>
/// Type-safe convenience getters for <see cref="NbtCompoundTag"/>.
/// </summary>
public static class NbtTagReaderExtensions
{
    public static bool TryGetByte(this NbtCompoundTag compound, string name, out sbyte value)
    {
        if (compound.GetTag<NbtByteTag>(name) is { } tag)
        {
            value = tag.Value;
            return true;
        }
        value = 0;
        return false;
    }

    public static bool TryGetShort(this NbtCompoundTag compound, string name, out short value)
    {
        if (compound.GetTag<NbtShortTag>(name) is { } tag)
        {
            value = tag.Value;
            return true;
        }
        value = 0;
        return false;
    }

    public static bool TryGetInt(this NbtCompoundTag compound, string name, out int value)
    {
        if (compound.GetTag<NbtIntTag>(name) is { } tag)
        {
            value = tag.Value;
            return true;
        }
        value = 0;
        return false;
    }

    public static bool TryGetNumericAsInt(this NbtCompoundTag compound, string name, out int value)
    {
        var tag = compound.GetTag(name);
        switch (tag)
        {
            case NbtIntTag i:
                value = i.Value;
                return true;
            case NbtShortTag s:
                value = s.Value;
                return true;
            case NbtByteTag b:
                value = b.Value;
                return true;
            default:
                value = 0;
                return false;
        }
    }

    public static bool TryGetLong(this NbtCompoundTag compound, string name, out long value)
    {
        if (compound.GetTag<NbtLongTag>(name) is { } tag)
        {
            value = tag.Value;
            return true;
        }
        value = 0;
        return false;
    }

    public static bool TryGetString(this NbtCompoundTag compound, string name, out string value)
    {
        if (compound.GetTag<NbtStringTag>(name) is { } tag)
        {
            value = tag.Value;
            return true;
        }
        value = string.Empty;
        return false;
    }

    public static bool TryGetByteArray(this NbtCompoundTag compound, string name, out byte[] value)
    {
        if (compound.GetTag<NbtByteArrayTag>(name) is { } tag)
        {
            value = tag.Value;
            return true;
        }
        value = [];
        return false;
    }

    public static bool TryGetIntArray(this NbtCompoundTag compound, string name, out int[] value)
    {
        if (compound.GetTag<NbtIntArrayTag>(name) is { } tag)
        {
            value = tag.Value;
            return true;
        }
        value = [];
        return false;
    }

    public static bool TryGetCompound(this NbtCompoundTag compound, string name, out NbtCompoundTag? value)
    {
        value = compound.GetTag<NbtCompoundTag>(name);
        return value is not null;
    }

    public static bool TryGetList(this NbtCompoundTag compound, string name, out NbtListTag? value)
    {
        value = compound.GetTag<NbtListTag>(name);
        return value is not null;
    }
}
