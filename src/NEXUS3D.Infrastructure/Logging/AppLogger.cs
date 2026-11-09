using Microsoft.Extensions.Logging;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.Infrastructure.Logging;

/// <summary>
/// Categorized logger implementation backed by <see cref="LogFileProvider"/>.
/// </summary>
public sealed class AppLogger : ILogger
{
    private readonly string _categoryName;
    private readonly LogFileProvider _provider;

    public AppLogger(string categoryName, LogFileProvider provider)
    {
        _categoryName = Guard.NotNullOrWhiteSpace(categoryName);
        _provider = Guard.NotNull(provider);
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) =>
        logLevel != LogLevel.None && logLevel >= _provider.Configuration.MinimumLevel;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        Guard.NotNull(formatter);
        string message = formatter(state, exception);

        if (string.IsNullOrWhiteSpace(message) && exception is null)
        {
            return;
        }

        _provider.WriteEntry(logLevel, _categoryName, eventId, message, exception);
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}

