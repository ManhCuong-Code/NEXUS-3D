namespace NEXUS3D.Domain.Common;

/// <summary>
/// Standardized error codes for the Universal Scene Model (USM) in Stage 02.
/// </summary>
public static class UsmErrorCodes
{
    public const string InvalidScene = "USM001";
    public const string DuplicateId = "USM002";
    public const string SceneGraphCycle = "USM003";
    public const string NodeNotFound = "USM004";
    public const string InvalidTransform = "USM005";
    public const string InvalidCoordinateSystem = "USM006";
    public const string InvalidVoxelGrid = "USM007";
    public const string InvalidPaletteIndex = "USM008";
    public const string InvalidMesh = "USM009";
    public const string InvalidVertexIndex = "USM010";
    public const string MaterialReferenceNotFound = "USM011";
    public const string TextureReferenceNotFound = "USM012";
    public const string InvalidSkeleton = "USM013";
    public const string InvalidAnimation = "USM014";
    public const string InvalidMetadata = "USM015";
    public const string UnsupportedSchemaVersion = "USM016";
    public const string SerializationFailed = "USM017";
    public const string DataLimitExceeded = "USM018";
    public const string InvalidRawData = "USM019";
}

/// <summary>
/// Represents a domain error with a standardized code and message.
/// </summary>
public readonly record struct Error(string Code, string Message)
{
    public static Error None => new(string.Empty, string.Empty);
    public bool IsNone => string.IsNullOrEmpty(Code);
}

/// <summary>
/// Domain result type for operations that can fail with a domain <see cref="Error"/>.
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    protected Result(bool isSuccess, Error error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
    public static Result Failure(string code, string message) => new(false, new Error(code, message));
}

/// <summary>
/// Typed domain result.
/// </summary>
public sealed class Result<T> : Result
{
    public T? Value { get; }

    private Result(bool isSuccess, T? value, Error error)
        : base(isSuccess, error)
    {
        Value = value;
    }

    public static Result<T> Success(T value) => new(true, value, Error.None);
    public new static Result<T> Failure(Error error) => new(false, default, error);
    public new static Result<T> Failure(string code, string message) => new(false, default, new Error(code, message));
}

/// <summary>
/// Exception thrown only when an unrecoverable domain invariant is violated.
/// </summary>
public sealed class DomainException : Exception
{
    public string ErrorCode { get; }

    public DomainException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? UsmErrorCodes.InvalidScene : errorCode;
    }

    public DomainException(string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? UsmErrorCodes.InvalidScene : errorCode;
    }
}

