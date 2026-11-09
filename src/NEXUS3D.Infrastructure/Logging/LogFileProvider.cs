using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.Infrastructure.Logging;

/// <summary>
/// Custom <see cref="ILoggerProvider"/> that writes structured log entries (Information, Warning, Error, Critical)
/// to rolling daily log files with timestamp, source category, thread ID, and full exception stack traces.
/// </summary>
[ProviderAlias("NexusFile")]
public sealed partial class LogFileProvider : ILoggerProvider
{
    private readonly LogConfiguration _configuration;
    private readonly ConcurrentDictionary<string, AppLogger> _loggers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _writeLock = new();
    private bool _disposed;

    [GeneratedRegex(@"(?i)(password|passwd|secret|token|apikey|api_key|authorization)\s*[:=]\s*([^\s,;""']+)", RegexOptions.Compiled)]
    private static partial Regex SensitiveKeyValueRegex();

    public LogFileProvider(LogConfiguration configuration)
    {
        _configuration = Guard.NotNull(configuration);

        if (!string.IsNullOrWhiteSpace(_configuration.LogDirectory))
        {
            Directory.CreateDirectory(_configuration.LogDirectory);
        }
    }

    public LogConfiguration Configuration => _configuration;

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new AppLogger(name, this));

    internal void WriteEntry(
        LogLevel logLevel,
        string categoryName,
        EventId eventId,
        string message,
        Exception? exception)
    {
        if (_disposed || logLevel < _configuration.MinimumLevel)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_configuration.LogDirectory))
        {
            return;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        string sanitizedMessage = _configuration.RedactSensitiveData
            ? SanitizeSensitiveText(message)
            : message;

        var builder = new StringBuilder(256);
        builder.Append('[')
               .Append(now.ToString("yyyy-MM-dd HH:mm:ss.fff 'UTC'"))
               .Append("] [")
               .Append(FormatLogLevel(logLevel))
               .Append("] [T:")
               .Append(Environment.CurrentManagedThreadId)
               .Append("] [")
               .Append(categoryName);

        if (eventId.Id != 0)
        {
            builder.Append(':')
                   .Append(eventId.Id);
        }

        builder.Append("] ")
               .AppendLine(sanitizedMessage);

        if (exception is not null)
        {
            string exceptionText = FormatExceptionDetails(exception);
            if (_configuration.RedactSensitiveData)
            {
                exceptionText = SanitizeSensitiveText(exceptionText);
            }

            builder.AppendLine(exceptionText);
        }

        string filePath = _configuration.GetCurrentLogFilePath(now);

        lock (_writeLock)
        {
            try
            {
                Directory.CreateDirectory(_configuration.LogDirectory);
                RotateFileIfExceedsLimit(filePath);
                File.AppendAllText(filePath, builder.ToString(), Encoding.UTF8);
            }
            catch
            {
                // Avoid throwing inside logger infrastructure if file is temporarily locked by external tools.
            }
        }
    }

    public static string SanitizeSensitiveText(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        return SensitiveKeyValueRegex().Replace(input, "$1=***REDACTED***");
    }

    private static string FormatLogLevel(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "INF"
    };

    private static string FormatExceptionDetails(Exception exception)
    {
        var sb = new StringBuilder();
        Exception? current = exception;
        int depth = 0;

        while (current is not null && depth < 10)
        {
            string prefix = depth == 0 ? "Exception" : $"InnerException[{depth}]";
            sb.Append("  >>> ")
              .Append(prefix)
              .Append(": ")
              .Append(current.GetType().FullName)
              .Append(" - ")
              .AppendLine(current.Message);

            if (!string.IsNullOrWhiteSpace(current.Source))
            {
                sb.Append("      Source: ").AppendLine(current.Source);
            }

            if (!string.IsNullOrWhiteSpace(current.StackTrace))
            {
                sb.AppendLine("      StackTrace:");
                sb.AppendLine(current.StackTrace);
            }

            current = current.InnerException;
            depth++;
        }

        return sb.ToString().TrimEnd();
    }

    private void RotateFileIfExceedsLimit(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        var info = new FileInfo(filePath);
        if (info.Length < _configuration.MaxFileSizeBytes)
        {
            return;
        }

        string archivePath = Path.Combine(
            _configuration.LogDirectory,
            $"{Path.GetFileNameWithoutExtension(filePath)}-{DateTimeOffset.UtcNow:HHmmss}.bak.log");

        File.Move(filePath, archivePath, overwrite: true);
    }

    public void Dispose()
    {
        _disposed = true;
        _loggers.Clear();
    }
}

