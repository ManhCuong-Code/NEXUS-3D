namespace Nexus3D.CLI;

/// <summary>
/// Standard exit codes for the NEXUS-3D CLI tool.
/// </summary>
public static class CliExitCodes
{
    /// <summary>Execution completed with 100% success.</summary>
    public const int Success = 0;

    /// <summary>Unexpected or unhandled runtime failure.</summary>
    public const int UnknownError = 1;

    /// <summary>Command line flags or arguments are invalid, missing, or malformed.</summary>
    public const int InvalidArguments = 2;

    /// <summary>Source file or directory does not exist or has an unreadable format.</summary>
    public const int InvalidInputFile = 3;

    /// <summary>Single conversion job or entire batch failed.</summary>
    public const int ConversionFailure = 4;

    /// <summary>Batch conversion completed with one or more failed items.</summary>
    public const int PartialBatchFailure = 5;

    /// <summary>Process was interrupted or cancelled by the user (e.g. Ctrl+C).</summary>
    public const int CancelledByUser = 6;
}

