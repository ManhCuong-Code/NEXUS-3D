using System.Text;
using NEXUS3D.Formats.Schematic.Nbt.Tags;
using NEXUS3D.Formats.Schematic.Nbt.Utilities;

namespace NEXUS3D.Formats.Schematic.Nbt.IO;

/// <summary>
/// High-performance endian-aware binary writer for serializing <see cref="NbtCompoundTag"/> trees into raw NBT bytes.
/// </summary>
public sealed class NbtBinaryWriter
{
    private readonly Stream _output;
    private readonly NbtWriteOptions _options;
    private readonly byte[] _scratch = new byte[8];

    public NbtBinaryWriter(Stream output, NbtWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(output);
        _output = output;
        _options = options ?? NbtWriteOptions.Default;
    }

    public void WriteRootCompound(NbtCompoundTag root)
    {
        ArgumentNullException.ThrowIfNull(root);
        WriteByte((byte)NbtTagType.Compound);
        WriteString(root.Name);
        WriteCompoundPayload(root, currentDepth: 1);
    }

    public void WriteByte(byte value) => _output.WriteByte(value);

    public void WriteSByte(sbyte value) => _output.WriteByte(unchecked((byte)value));

    public void WriteInt16(short value)
    {
        NbtEndian.WriteInt16(_scratch.AsSpan(0, 2), value, _options.Endianness);
        _output.Write(_scratch, 0, 2);
    }

    public void WriteUInt16(ushort value)
    {
        NbtEndian.WriteUInt16(_scratch.AsSpan(0, 2), value, _options.Endianness);
        _output.Write(_scratch, 0, 2);
    }

    public void WriteInt32(int value)
    {
        NbtEndian.WriteInt32(_scratch.AsSpan(0, 4), value, _options.Endianness);
        _output.Write(_scratch, 0, 4);
    }

    public void WriteInt64(long value)
    {
        NbtEndian.WriteInt64(_scratch.AsSpan(0, 8), value, _options.Endianness);
        _output.Write(_scratch, 0, 8);
    }

    public void WriteSingle(float value)
    {
        NbtEndian.WriteSingle(_scratch.AsSpan(0, 4), value, _options.Endianness);
        _output.Write(_scratch, 0, 4);
    }

    public void WriteDouble(double value)
    {
        NbtEndian.WriteDouble(_scratch.AsSpan(0, 8), value, _options.Endianness);
        _output.Write(_scratch, 0, 8);
    }

    public void WriteString(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            WriteUInt16(0);
            return;
        }

        byte[] utf8 = Encoding.UTF8.GetBytes(value);
        if (utf8.Length > ushort.MaxValue)
        {
            throw new NbtException(
                NbtErrorCodes.LimitExceeded,
                $"NBT string UTF-8 byte length ({utf8.Length}) exceeds maximum 65535 bytes.");
        }

        WriteUInt16((ushort)utf8.Length);
        _output.Write(utf8, 0, utf8.Length);
    }

    private void WriteTagPayload(NbtTag tag, int currentDepth)
    {
        if (currentDepth > _options.MaxNestingDepth)
        {
            throw new NbtException(
                NbtErrorCodes.LimitExceeded,
                $"NBT nesting depth ({currentDepth}) exceeds MaxNestingDepth ({_options.MaxNestingDepth}).");
        }

        switch (tag)
        {
            case NbtByteTag b:
                WriteSByte(b.Value);
                break;
            case NbtShortTag s:
                WriteInt16(s.Value);
                break;
            case NbtIntTag i:
                WriteInt32(i.Value);
                break;
            case NbtLongTag l:
                WriteInt64(l.Value);
                break;
            case NbtFloatTag f:
                WriteSingle(f.Value);
                break;
            case NbtDoubleTag d:
                WriteDouble(d.Value);
                break;
            case NbtByteArrayTag ba:
                WriteInt32(ba.Value.Length);
                if (ba.Value.Length > 0)
                {
                    _output.Write(ba.Value, 0, ba.Value.Length);
                }
                break;
            case NbtStringTag str:
                WriteString(str.Value);
                break;
            case NbtListTag list:
                WriteListPayload(list, currentDepth + 1);
                break;
            case NbtCompoundTag comp:
                WriteCompoundPayload(comp, currentDepth + 1);
                break;
            case NbtIntArrayTag ia:
                WriteInt32(ia.Value.Length);
                for (int idx = 0; idx < ia.Value.Length; idx++)
                {
                    WriteInt32(ia.Value[idx]);
                }
                break;
            case NbtLongArrayTag la:
                WriteInt32(la.Value.Length);
                for (int idx = 0; idx < la.Value.Length; idx++)
                {
                    WriteInt64(la.Value[idx]);
                }
                break;
            default:
                throw new NbtException(
                    NbtErrorCodes.UnsupportedTagType,
                    $"Cannot serialize unsupported NBT tag type '{tag.Type}'.");
        }
    }

    private void WriteListPayload(NbtListTag list, int currentDepth)
    {
        if (currentDepth > _options.MaxNestingDepth)
        {
            throw new NbtException(
                NbtErrorCodes.LimitExceeded,
                $"NBT nesting depth ({currentDepth}) exceeds MaxNestingDepth ({_options.MaxNestingDepth}).");
        }

        NbtTagType elemType = list.Count == 0 ? NbtTagType.End : list.ElementType;
        WriteByte((byte)elemType);
        WriteInt32(list.Count);

        for (int i = 0; i < list.Count; i++)
        {
            WriteTagPayload(list[i], currentDepth);
        }
    }

    private void WriteCompoundPayload(NbtCompoundTag compound, int currentDepth)
    {
        if (currentDepth > _options.MaxNestingDepth)
        {
            throw new NbtException(
                NbtErrorCodes.LimitExceeded,
                $"NBT nesting depth ({currentDepth}) exceeds MaxNestingDepth ({_options.MaxNestingDepth}).");
        }

        foreach (var child in compound.GetAllTags())
        {
            WriteByte((byte)child.Type);
            WriteString(child.Name);
            WriteTagPayload(child, currentDepth);
        }

        WriteByte((byte)NbtTagType.End);
    }
}
