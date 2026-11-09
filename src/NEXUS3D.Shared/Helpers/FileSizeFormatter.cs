using System.Globalization;

namespace NEXUS3D.Shared.Helpers;

/// <summary>
/// Utility methods for formatting file sizes and durations consistently across the UI and logs.
/// </summary>
public static class FileSizeFormatter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    public static string FormatBytes(long bytes)
    {
        if (bytes < 0)
        {
            return "0 B";
        }

        if (bytes == 0)
        {
            return "0 B";
        }

        double value = bytes;
        int unitIndex = 0;

        while (value >= 1024.0 && unitIndex < Units.Length - 1)
        {
            value /= 1024.0;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{bytes} B"
            : string.Create(CultureInfo.InvariantCulture, $"{value:0.##} {Units[unitIndex]}");
    }

    public static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMilliseconds < 1000)
        {
            return $"{Math.Max(0, (int)duration.TotalMilliseconds)} ms";
        }

        if (duration.TotalMinutes < 1)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{duration.TotalSeconds:0.0} s");
        }

        return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";
    }
}

