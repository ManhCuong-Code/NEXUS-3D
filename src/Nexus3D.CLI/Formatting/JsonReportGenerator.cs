using System.Text.Json;
using System.Text.Json.Serialization;
using NEXUS3D.Conversion.Abstractions.Models;

namespace Nexus3D.CLI.Formatting;

/// <summary>
/// Serializes batch conversion results into structured JSON execution reports.
/// </summary>
public static class JsonReportGenerator
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string GenerateJson(BatchJobResult result)
    {
        var report = new
        {
            JobId = result.JobId,
            JobName = result.JobName,
            Status = result.Status.ToString(),
            TotalFiles = result.TotalFiles,
            SucceededFiles = result.SucceededFiles,
            FailedFiles = result.FailedFiles,
            SkippedFiles = result.SkippedFiles,
            DurationMs = result.DurationMs,
            ErrorMessage = result.ErrorMessage,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Items = result.Items.Select(i => new
            {
                ItemId = i.ItemId,
                SourceFilePath = i.SourceFilePath,
                TargetFilePath = i.TargetFilePath,
                SourceFormat = i.SourceFormat.ToString(),
                TargetFormat = i.TargetFormat.ToString(),
                Status = i.Status.ToString(),
                ErrorCode = i.ErrorCode,
                ErrorMessage = i.ErrorMessage,
                RetryCount = i.RetryCount,
                DurationMs = i.DurationMs,
                SourceSizeBytes = i.SourceSizeBytes,
                TargetSizeBytes = i.TargetSizeBytes
            })
        };

        return JsonSerializer.Serialize(report, Options);
    }

    public static async Task SaveToFileAsync(BatchJobResult result, string filePath, CancellationToken cancellationToken = default)
    {
        string json = GenerateJson(result);
        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllTextAsync(filePath, json, cancellationToken).ConfigureAwait(false);
    }
}

