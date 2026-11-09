namespace NEXUS3D.Formats.Obj.Common;

/// <summary>
/// Standardized error and warning codes for the Wavefront OBJ &amp; MTL Engine (Stage 04).
/// </summary>
public static class ObjErrorCodes
{
    public const string EmptyOrUnrecognizedFile = "OBJ001";
    public const string SyntaxError = "OBJ002";
    public const string InvalidVertex = "OBJ003";
    public const string InvalidTexCoord = "OBJ004";
    public const string InvalidNormal = "OBJ005";
    public const string InvalidFace = "OBJ006";
    public const string IndexOutOfRange = "OBJ007";
    public const string DegeneratePolygon = "OBJ008";
    public const string LimitExceeded = "OBJ009";
    public const string InvalidEncoding = "OBJ010";
    public const string MissingMtlFile = "OBJ011";
    public const string MissingMaterial = "OBJ012";
    public const string InvalidMtlSyntax = "OBJ013";
    public const string InvalidMtlValue = "OBJ014";
    public const string MissingTextureFile = "OBJ015";
    public const string UnsupportedStatement = "OBJ016";
    public const string ExportFailed = "OBJ017";
}

/// <summary>
/// Exception thrown when an OBJ or MTL stream violates format syntax, indexing rules, or safety limits.
/// </summary>
public sealed class ObjException : Exception
{
    public string ErrorCode { get; }
    public int? LineNumber { get; }

    public ObjException(string errorCode, string message, int? lineNumber = null)
        : base(lineNumber.HasValue ? $"[Line {lineNumber.Value}] {message}" : message)
    {
        ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? ObjErrorCodes.SyntaxError : errorCode;
        LineNumber = lineNumber;
    }

    public ObjException(string errorCode, string message, Exception innerException, int? lineNumber = null)
        : base(lineNumber.HasValue ? $"[Line {lineNumber.Value}] {message}" : message, innerException)
    {
        ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? ObjErrorCodes.SyntaxError : errorCode;
        LineNumber = lineNumber;
    }
}

