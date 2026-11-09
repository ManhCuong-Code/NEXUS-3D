using Microsoft.Extensions.Logging;

namespace NEXUS3D.Infrastructure.Logging;

/// <summary>
/// Configuration options for the NEXUS 3D file logging subsystem.
/// </summary>
public sealed class LogConfiguration
{
    public string LogDirectory { get; set; } = string.Empty;
    public string FileNamePrefix { get; set; } = "nexus3d";
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024; // 10 MB
    public int RetainedFileCountLimit { get; set; } = 14;
    public bool RedactSensitiveData { get; set; } = true;

    public string GetCurrentLogFilePath(DateTimeOffset timestampUtc)
    {
        string datePart = timestampUtc.ToString("yyyyMMdd");
        return Path.Combine(LogDirectory, $"{FileNamePrefix}-{datePart}.log");
    }
}

