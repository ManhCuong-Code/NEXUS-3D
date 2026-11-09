namespace NEXUS3D.Formats.Schematic.Common;

/// <summary>
/// Standardized Schematic Engine error codes (Stage 03 Section 14.1).
/// </summary>
public static class SchematicErrorCodes
{
    public const string UnrecognizedSchematic = "SCH001";
    public const string UnsupportedVersion = "SCH002";
    public const string InvalidDimensions = "SCH003";
    public const string InvalidBlockData = "SCH004";
    public const string InvalidPalette = "SCH005";
    public const string InvalidVarInt = "SCH006";
    public const string InvalidBlockState = "SCH007";
    public const string InvalidBlockEntity = "SCH008";
    public const string UnmappedLegacyId = "SCH009";
    public const string InvalidLegacyMetadata = "SCH010";
    public const string InvalidAddBlocks = "SCH011";
    public const string InvalidOffset = "SCH012";
    public const string MissingRequiredData = "SCH013";
    public const string LimitExceeded = "SCH014";
    public const string ExportFailed = "SCH015";
    public const string DataPreservationFailed = "SCH016";
}

/// <summary>
/// Exception thrown when a schematic file or operation violates schematic format invariants.
/// </summary>
public sealed class SchematicException : Exception
{
    public string ErrorCode { get; }

    public SchematicException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? SchematicErrorCodes.UnrecognizedSchematic : errorCode;
    }

    public SchematicException(string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? SchematicErrorCodes.UnrecognizedSchematic : errorCode;
    }
}
