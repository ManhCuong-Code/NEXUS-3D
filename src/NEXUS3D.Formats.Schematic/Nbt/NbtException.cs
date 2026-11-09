namespace NEXUS3D.Formats.Schematic.Nbt;

/// <summary>
/// Standardized NBT Engine error codes (Stage 03 Section 14.1).
/// </summary>
public static class NbtErrorCodes
{
    public const string InvalidNbt = "NBT001";
    public const string UnsupportedTagType = "NBT002";
    public const string LimitExceeded = "NBT003";
    public const string CompressionError = "NBT004";
    public const string InvalidEndianness = "NBT005";
}

/// <summary>
/// Exception thrown when an NBT stream violates structural rules, type constraints, or security limits.
/// </summary>
public sealed class NbtException : Exception
{
    public string ErrorCode { get; }

    public NbtException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? NbtErrorCodes.InvalidNbt : errorCode;
    }

    public NbtException(string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? NbtErrorCodes.InvalidNbt : errorCode;
    }
}

