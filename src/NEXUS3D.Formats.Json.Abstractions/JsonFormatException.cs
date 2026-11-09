namespace NEXUS3D.Formats.Json.Abstractions;

/// <summary>
/// Standardized error and warning codes for the Stage 05 JSON Engine (Voxel JSON &amp; Bedrock Geometry JSON).
/// </summary>
public static class JsonFormatErrorCodes
{
    public const string EmptyOrInvalidRoot = "JSON001";
    public const string SyntaxError = "JSON002";
    public const string TypeMismatch = "JSON003";
    public const string MissingRequiredProperty = "JSON004";
    public const string InvalidNumericValue = "JSON005";
    public const string LimitExceeded = "JSON006";
    public const string UnsupportedSchemaOrDialect = "JSON007";
    public const string DuplicateIdentifierOrCoordinate = "JSON008";
    public const string BrokenReferenceOrHierarchy = "JSON009";
    public const string UnknownExtensionProperty = "JSON010";
    public const string ExportFailed = "JSON011";
    public const string MisplacedAnimationDocument = "JSON012";
}

/// <summary>
/// Exception thrown when a Voxel JSON or Bedrock Geometry JSON document violates syntax, schema, type, or safety limits.
/// Includes exact JSON path, line number, and byte offset when available.
/// </summary>
public sealed class JsonFormatException : Exception
{
    public string ErrorCode { get; }
    public string? JsonPath { get; }
    public long? LineNumber { get; }
    public long? BytePositionInLine { get; }

    public JsonFormatException(
        string errorCode,
        string message,
        string? jsonPath = null,
        long? lineNumber = null,
        long? bytePositionInLine = null,
        Exception? innerException = null)
        : base(FormatMessage(message, jsonPath, lineNumber, bytePositionInLine), innerException)
    {
        ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? JsonFormatErrorCodes.SyntaxError : errorCode;
        JsonPath = jsonPath;
        LineNumber = lineNumber;
        BytePositionInLine = bytePositionInLine;
    }

    private static string FormatMessage(
        string message,
        string? jsonPath,
        long? lineNumber,
        long? bytePosition)
    {
        if (!string.IsNullOrWhiteSpace(jsonPath) && lineNumber.HasValue)
        {
            return $"[Path: {jsonPath}, Line: {lineNumber.Value + 1}, Pos: {bytePosition ?? 0}] {message}";
        }

        if (!string.IsNullOrWhiteSpace(jsonPath))
        {
            return $"[Path: {jsonPath}] {message}";
        }

        if (lineNumber.HasValue)
        {
            return $"[Line: {lineNumber.Value + 1}, Pos: {bytePosition ?? 0}] {message}";
        }

        return message;
    }
}
