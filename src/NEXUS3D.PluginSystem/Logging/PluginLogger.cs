using Microsoft.Extensions.Logging;
using NEXUS3D.Plugin.SDK.Contracts;

namespace NEXUS3D.PluginSystem.Logging;

/// <summary>
/// Scoped logger implementation forwarding plugin logs to both the host logger and a dedicated per-plugin log file.
/// </summary>
public sealed class PluginLogger : IPluginLogger
{
    private readonly string _pluginId;
    private readonly string _logFilePath;
    private readonly ILogger _hostLogger;
    private readonly object _lock = new();

    public PluginLogger(string pluginId, string logDirectory, ILogger hostLogger)
    {
        _pluginId = pluginId;
        _hostLogger = hostLogger;
        _logFilePath = Path.Combine(logDirectory, "plugin.log");

        try
        {
            Directory.CreateDirectory(logDirectory);
        }
        catch
        {
            // Ignore directory creation failure
        }
    }

    public void LogDebug(string message)
    {
        _hostLogger.LogDebug("[Plugin:{PluginId}] {Message}", _pluginId, message);
        WriteToFile("DEBUG", message);
    }

    public void LogInformation(string message)
    {
        _hostLogger.LogInformation("[Plugin:{PluginId}] {Message}", _pluginId, message);
        WriteToFile("INFO", message);
    }

    public void LogWarning(string message)
    {
        _hostLogger.LogWarning("[Plugin:{PluginId}] {Message}", _pluginId, message);
        WriteToFile("WARN", message);
    }

    public void LogError(string message, Exception? exception = null)
    {
        _hostLogger.LogError(exception, "[Plugin:{PluginId}] {Message}", _pluginId, message);
        string details = exception is null ? message : $"{message} - Exception: {exception.Message}\n{exception.StackTrace}";
        WriteToFile("ERROR", details);
    }

    private void WriteToFile(string level, string message)
    {
        try
        {
            lock (_lock)
            {
                string line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}";
                File.AppendAllText(_logFilePath, line);
            }
        }
        catch
        {
            // Do not fault host if disk log fails
        }
    }
}

