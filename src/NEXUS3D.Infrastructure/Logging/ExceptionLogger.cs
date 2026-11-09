using Microsoft.Extensions.Logging;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.Infrastructure.Logging;

/// <summary>
/// Centralized exception logging service for unhandled UI, TaskScheduler, and AppDomain exceptions.
/// Ensures all fatal and non-fatal errors are recorded with source, timestamp, and stack trace.
/// </summary>
public sealed class ExceptionLogger
{
    private readonly ILogger<ExceptionLogger> _logger;

    public ExceptionLogger(ILogger<ExceptionLogger> logger)
    {
        _logger = Guard.NotNull(logger);
    }

    public void LogHandledException(Exception exception, string sourceComponent, string? contextMessage = null)
    {
        Guard.NotNull(exception);
        string source = string.IsNullOrWhiteSpace(sourceComponent) ? "UnknownComponent" : sourceComponent;

        _logger.LogError(
            exception,
            "[{Source}] Handled exception: {ContextMessage} | ExceptionMessage: {ExceptionMessage}",
            source,
            contextMessage ?? "Operation failed",
            exception.Message);
    }

    public void LogUnhandledException(Exception exception, string sourceChannel, bool isTerminating)
    {
        Guard.NotNull(exception);
        string channel = string.IsNullOrWhiteSpace(sourceChannel) ? "GlobalExceptionHandler" : sourceChannel;

        if (isTerminating)
        {
            _logger.LogCritical(
                exception,
                "[{Channel}] CRITICAL UNHANDLED EXCEPTION (Terminating={IsTerminating}): {Message}",
                channel,
                isTerminating,
                exception.Message);
        }
        else
        {
            _logger.LogError(
                exception,
                "[{Channel}] Unhandled runtime exception intercepted (Terminating={IsTerminating}): {Message}",
                channel,
                isTerminating,
                exception.Message);
        }
    }
}

