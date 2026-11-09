using System.Text;
using NEXUS3D.Formats.Schematic.Nbt.Tags;
using NEXUS3D.Formats.Schematic.Nbt.Utilities;

namespace NEXUS3D.Formats.Schematic.Nbt.IO;

/// <summary>
/// High-performance endian-aware binary reader for decompressed NBT buffers with strict structural and size guards.
/// </summary>
public sealed class NbtBinaryReader
{
    private readonly byte[] _buffer;
    private readonly NbtReadOptions _options;
    private int _position;
    private int _totalTagsRead;

    public int Position => _position;
    public int Length => _buffer.Length;

    public NbtBinaryReader(byte[] buffer, NbtReadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        _buffer = buffer;
        _options = options ?? NbtReadOptions.Default;
    }

    public NbtCompoundTag ReadRootCompound()
    {
        byte rootTypeByte = ReadByteUnsigned();
        if (rootTypeByte != (byte)NbtTagType.Compound)
        {
            throw new NbtException(
                NbtErrorCodes.InvalidNbt,
                $"Root NBT tag must be TAG_Compound (10), but found tag ID {rootTypeByte}.");
        }

        string rootName = ReadString();
        return ReadCompoundPayload(rootName, currentDepth: 1);
    }

    public byte ReadByteUnsigned()
    {
        EnsureAvailable(1);
        return _buffer[_position++];
    }

    public sbyte ReadSByte() => unchecked((sbyte)ReadByteUnsigned());

    public short ReadInt16()
    {
        EnsureAvailable(2);
        short val = NbtEndian.ReadInt16(_buffer.AsSpan(_position, 2), _options.Endianness);
        _position += 2;
        return val;
    }

    public ushort ReadUInt16()
    {
        EnsureAvailable(2);
        ushort val = NbtEndian.ReadUInt16(_buffer.AsSpan(_position, 2), _options.Endianness);
        _position += 2;
        return val;
    }

    public int ReadInt32()
    {
        EnsureAvailable(4);
        int val = NbtEndian.ReadInt32(_buffer.AsSpan(_position, 4), _options.Endianness);
        _position += 4;
        return val;
    }

    public long ReadInt64()
    {
        EnsureAvailable(8);
        long val = NbtEndian.ReadInt64(_buffer.AsSpan(_position, 8), _options.Endianness);
        _position += 8;
        return val;
    }

    public float ReadSingle()
    {
        EnsureAvailable(4);
        float val = NbtEndian.ReadSingle(_buffer.AsSpan(_position, 4), _options.Endianness);
        _position += 4;
        return val;
    }

    public double ReadDouble()
    {
        EnsureAvailable(8);
        double val = NbtEndian.ReadDouble(_buffer.AsSpan(_position, 8), _options.Endianness);
        _position += 8;
        return val;
    }

    public string ReadString()
    {
        ushort byteLen = ReadUInt16();
        if (byteLen > _options.MaxStringLength)
        {
            throw new NbtException(
                NbtErrorCodes.LimitExceeded,
                $"NBT string byte length ({byteLen}) exceeds MaxStringLength ({_options.MaxStringLength}).");
        }

        if (byteLen == 0)
        {
            return string.Empty;
        }

        EnsureAvailable(byteLen);
        string text = Encoding.UTF8.GetString(_buffer, _position, byteLen);
        _position += byteLen;
        return text;
    }

    private NbtTag ReadTagPayload(NbtTagType type, string name, int currentDepth)
    {
        IncrementAndCheckTagCount();

        if (currentDepth > _options.MaxNestingDepth)
        {
            throw new NbtException(
                NbtErrorCodes.LimitExceeded,
                $"NBT nesting depth ({currentDepth}) exceeds MaxNestingDepth ({_options.MaxNestingDepth}).");
        }

        return type switch
        {
            NbtTagType.Byte => new NbtByteTag(name, ReadSByte()),
            NbtTagType.Short => new NbtShortTag(name, ReadInt16()),
            NbtTagType.Int => new NbtIntTag(name, ReadInt32()),
            NbtTagType.Long => new NbtLongTag(name, ReadInt64()),
            NbtTagType.Float => new NbtFloatTag(name, ReadSingle()),
            NbtTagType.Double => new NbtDoubleTag(name, ReadDouble()),
            NbtTagType.ByteArray => ReadByteArrayPayload(name),
            NbtTagType.String => new NbtStringTag(name, ReadString()),
            NbtTagType.List => ReadListPayload(name, currentDepth + 1),
            NbtTagType.Compound => ReadCompoundPayload(name, currentDepth + 1),
            NbtTagType.IntArray => ReadIntArrayPayload(name),
            NbtTagType.LongArray => ReadLongArrayPayload(name),
            _ => throw new NbtException(NbtErrorCodes.UnsupportedTagType, $"Unsupported NBT tag type '{(byte)type}'.")
        };
    }

    private NbtByteArrayTag ReadByteArrayPayload(string name)
    {
        int length = ReadInt32();
        ValidateArrayLength(length, 1, "TAG_Byte_Array");

        var bytes = new byte[length];
        if (length > 0)
        {
            Buffer.BlockCopy(_buffer, _position, bytes, 0, length);
            _position += length;
        }
        return new NbtByteArrayTag(name, bytes);
    }

    private NbtIntArrayTag ReadIntArrayPayload(string name)
    {
        int length = ReadInt32();
        ValidateArrayLength(length, 4, "TAG_Int_Array");

        var values = new int[length];
        for (int i = 0; i < length; i++)
        {
            values[i] = ReadInt32();
        }
        return new NbtIntArrayTag(name, values);
    }

    private NbtLongArrayTag ReadLongArrayPayload(string name)
    {
        int length = ReadInt32();
        ValidateArrayLength(length, 8, "TAG_Long_Array");

        var values = new long[length];
        for (int i = 0; i < length; i++)
        {
            values[i] = ReadInt64();
        }
        return new NbtLongArrayTag(name, values);
    }

    private NbtListTag ReadListPayload(string name, int currentDepth)
    {
        if (currentDepth > _options.MaxNestingDepth)
        {
            throw new NbtException(
                NbtErrorCodes.LimitExceeded,
                $"NBT nesting depth ({currentDepth}) exceeds MaxNestingDepth ({_options.MaxNestingDepth}).");
        }

        byte elementTypeByte = ReadByteUnsigned();
        if (elementTypeByte > (byte)NbtTagType.LongArray)
        {
            throw new NbtException(
                NbtErrorCodes.UnsupportedTagType,
                $"Invalid TAG_List element type byte '{elementTypeByte}'.");
        }

        var elementType = (NbtTagType)elementTypeByte;
        int count = ReadInt32();
        if (count < 0 || count > _options.MaxListLength)
        {
            throw new NbtException(
                NbtErrorCodes.LimitExceeded,
                $"TAG_List length ({count}) is invalid or exceeds MaxListLength ({_options.MaxListLength}).");
        }

        if (count > 0 && elementType == NbtTagType.End)
        {
            throw new NbtException(
                NbtErrorCodes.InvalidNbt,
                $"TAG_List '{name}' has non-zero count ({count}) with element type TAG_End.");
        }

        var list = new NbtListTag(name, elementType);
        for (int i = 0; i < count; i++)
        {
            list.Add(ReadTagPayload(elementType, string.Empty, currentDepth));
        }

        return list;
    }

    private NbtCompoundTag ReadCompoundPayload(string name, int currentDepth)
    {
        if (currentDepth > _options.MaxNestingDepth)
        {
            throw new NbtException(
                NbtErrorCodes.LimitExceeded,
                $"NBT nesting depth ({currentDepth}) exceeds MaxNestingDepth ({_options.MaxNestingDepth}).");
        }

        var compound = new NbtCompoundTag(name);
        while (true)
        {
            byte tagTypeByte = ReadByteUnsigned();
            if (tagTypeByte == (byte)NbtTagType.End)
            {
                break;
            }

            if (tagTypeByte > (byte)NbtTagType.LongArray)
            {
                throw new NbtException(
                    NbtErrorCodes.UnsupportedTagType,
                    $"Unsupported NBT tag type '{tagTypeByte}' inside compound '{name}'.");
            }

            string childName = ReadString();
            var childTag = ReadTagPayload((NbtTagType)tagTypeByte, childName, currentDepth);
            compound.SetTag(childTag);
        }

        return compound;
    }

    private void ValidateArrayLength(int length, int elementSizeBytes, string tagLabel)
    {
        if (length < 0 || length > _options.MaxArrayLength)
        {
            throw new NbtException(
                NbtErrorCodes.LimitExceeded,
                $"{tagLabel} length ({length}) is negative or exceeds MaxArrayLength ({_options.MaxArrayLength}).");
        }

        long requiredBytes = (long)length * elementSizeBytes;
        if (requiredBytes > _buffer.Length - _position)
        {
            throw new NbtException(
                NbtErrorCodes.InvalidNbt,
                $"Unexpected end of NBT stream while reading {tagLabel} of {length} elements ({requiredBytes} bytes required, {_buffer.Length - _position} available).");
        }
    }

    private void IncrementAndCheckTagCount()
    {
        _totalTagsRead++;
        if (_totalTagsRead > _options.MaxTagCount)
        {
            throw new NbtException(
                NbtErrorCodes.LimitExceeded,
                $"Total NBT tag count ({_totalTagsRead}) exceeds MaxTagCount ({_options.MaxTagCount}).");
        }
    }

    private void EnsureAvailable(int byteCount)
    {
        if (_position + byteCount > _buffer.Length)
        {
            throw new NbtException(
                NbtErrorCodes.InvalidNbt,
                $"Unexpected end of NBT stream at offset {_position} (needed {byteCount} bytes, remaining {_buffer.Length - _position}).");
        }
    }
}

