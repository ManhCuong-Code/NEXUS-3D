using NEXUS3D.Conversion.Abstractions.Models;

namespace Nexus3D.CLI.Formatting;

/// <summary>
/// Console utility rendering an animated progress bar and throughput status.
/// </summary>
public static class ConsoleProgressBar
{
    private static readonly object _consoleLock = new();

    public static void Render(BatchProgress progress, int barWidth = 30)
    {
        lock (_consoleLock)
        {
            double pct = Math.Clamp(progress.PercentComplete, 0.0, 100.0);
            int filled = (int)(pct / 100.0 * barWidth);
            string bar = new string('=', filled) + (filled < barWidth ? ">" : "") + new string(' ', Math.Max(0, barWidth - filled - 1));

            string file = string.IsNullOrEmpty(progress.CurrentFileName)
                ? progress.Status.ToString()
                : progress.CurrentFileName;

            if (file.Length > 25)
            {
                file = "..." + file[^22..];
            }

            string text = $"\r[{bar}] {pct,5:F1}% ({progress.CompletedFiles}/{progress.TotalFiles}) S:{progress.SucceededFiles} F:{progress.FailedFiles} | {file,-25}";

            try
            {
                Console.Write(text);
            }
            catch
            {
                // In non-interactive or redirected standard output, skip writing '\r'
            }
        }
    }

    public static void Finish()
    {
        lock (_consoleLock)
        {
            try
            {
                Console.WriteLine();
            }
            catch
            {
            }
        }
    }
}
