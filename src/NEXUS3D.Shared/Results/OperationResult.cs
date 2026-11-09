namespace NEXUS3D.Shared.Results;

/// <summary>
/// Represents the outcome of an operation without throwing exceptions for expected business failures.
/// </summary>
public class OperationResult
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string ErrorCode { get; }
    public string ErrorMessage { get; }
    public DateTimeOffset TimestampUtc { get; }

    protected OperationResult(bool isSuccess, string errorCode, string errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        TimestampUtc = DateTimeOffset.UtcNow;
    }

    public static OperationResult Success() => new(true, string.Empty, string.Empty);

    public static OperationResult Failure(string errorCode, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
        {
            errorCode = "ERR_UNKNOWN";
        }

        return new OperationResult(false, errorCode, errorMessage ?? "An unexpected error occurred.");
    }
}

/// <summary>
/// Represents the typed outcome of an operation.
/// </summary>
public sealed class OperationResult<T> : OperationResult
{
    public T? Value { get; }

    private OperationResult(bool isSuccess, T? value, string errorCode, string errorMessage)
        : base(isSuccess, errorCode, errorMessage)
    {
        Value = value;
    }

    public static OperationResult<T> Success(T value) =>
        new(true, value, string.Empty, string.Empty);

    public new static OperationResult<T> Failure(string errorCode, string errorMessage) =>
        new(false, default, errorCode, errorMessage);
}

