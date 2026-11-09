using NEXUS3D.Formats.Schematic.Common;

namespace NEXUS3D.Formats.Schematic.Palette;

/// <summary>
/// Contract for encoding non-negative palette indices as base-128 VarInt bytes.
/// </summary>
public interface IVarIntEncoder
{
    void WriteVarInt(int value, Stream destination);
}

/// <summary>
/// Default implementation of <see cref="IVarIntEncoder"/>.
/// </summary>
public sealed class VarIntEncoder : IVarIntEncoder
{
    public static VarIntEncoder Instance { get; } = new();

    public void WriteVarInt(int value, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (value < 0)
        {
            throw new SchematicException(
                SchematicErrorCodes.InvalidVarInt,
                $"Cannot encode negative palette index ({value}) as schematic VarInt.");
        }

        uint unsignedVal = (uint)value;
        while ((unsignedVal & ~0x7Fu) != 0)
        {
            destination.WriteByte((byte)((unsignedVal & 0x7Fu) | 0x80u));
            unsignedVal >>= 7;
        }

        destination.WriteByte((byte)(unsignedVal & 0x7Fu));
    }

    public static byte[] EncodeSingle(int value)
    {
        using var ms = new MemoryStream(5);
        Instance.WriteVarInt(value, ms);
        return ms.ToArray();
    }

    public static byte[] EncodeArray(ReadOnlySpan<int> values)
    {
        using var ms = new MemoryStream(values.Length);
        for (int i = 0; i < values.Length; i++)
        {
            Instance.WriteVarInt(values[i], ms);
        }
        return ms.ToArray();
    }
}
