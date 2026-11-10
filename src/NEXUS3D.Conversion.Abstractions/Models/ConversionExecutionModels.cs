using System.Diagnostics;
using NEXUS3D.Domain.Entities;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Scene;

namespace NEXUS3D.Conversion.Abstractions.Models;

/// <summary>
/// Request payload initiating a single model format conversion.
/// </summary>
public sealed record ConversionRequest(
    string SourceFilePath,
    string TargetFilePath,
    ModelFormatType SourceFormat,
    ModelFormatType TargetFormat,
    ConversionOptions? Options = null,
    Guid? ProfileId = null);

/// <summary>
/// Runtime context holding execution state, intermediate USM scene, telemetry, and diagnostics.
/// </summary>
public sealed class ConversionContext
{
    public Guid JobId { get; init; } = Guid.NewGuid();
    public ConversionRequest Request { get; init; }
    public ConversionOptions Options { get; init; }
    public Scene3D? Scene { get; set; }
    public ConversionPlan? Plan { get; set; }
    public Stopwatch Stopwatch { get; } = new();
    public List<string> Logs { get; } = [];
    public List<string> Warnings { get; } = [];
    public CancellationToken CancellationToken { get; init; }

    public ConversionContext(ConversionRequest request, CancellationToken cancellationToken = default)
    {
        Request = request;
        Options = request.Options ?? ConversionOptions.Default;
        CancellationToken = cancellationToken;
    }

    public void Log(string message) => Logs.Add($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
    public void Warn(string warning) => Warnings.Add($"[{DateTime.UtcNow:HH:mm:ss.fff}] {warning}");
}

/// <summary>
/// Result returned upon completion or failure of a model conversion job.
/// </summary>
public sealed record ConversionResult(
    Guid JobId,
    bool IsSuccess,
    string? ErrorMessage,
    string? ErrorCode,
    string SourceFilePath,
    string TargetFilePath,
    ModelFormatType SourceFormat,
    ModelFormatType TargetFormat,
    long SourceSizeBytes,
    long TargetSizeBytes,
    long DurationMs,
    ConversionLossReport LossReport,
    IReadOnlyList<string> Logs,
    IReadOnlyList<string> Warnings)
{
    public static ConversionResult Failure(
        Guid jobId,
        string sourcePath,
        string targetPath,
        ModelFormatType sourceFormat,
        ModelFormatType targetFormat,
        string errorCode,
        string errorMessage,
        long durationMs,
        ConversionLossReport? lossReport = null,
        IReadOnlyList<string>? logs = null,
        IReadOnlyList<string>? warnings = null) =>
        new(
            JobId: jobId,
            IsSuccess: false,
            ErrorMessage: errorMessage,
            ErrorCode: errorCode,
            SourceFilePath: sourcePath,
            TargetFilePath: targetPath,
            SourceFormat: sourceFormat,
            TargetFormat: targetFormat,
            SourceSizeBytes: 0,
            TargetSizeBytes: 0,
            DurationMs: durationMs,
            LossReport: lossReport ?? ConversionLossReport.Empty(sourceFormat, targetFormat),
            Logs: logs ?? [],
            Warnings: warnings ?? []);
}

/// <summary>
/// Request for batch converting multiple files or an entire folder.
/// </summary>
public sealed record BatchConversionRequest(
    IReadOnlyList<ConversionRequest> Jobs,
    int MaxConcurrency = 4,
    bool ContinueOnError = true);

/// <summary>
/// Aggregated result of a batch conversion run.
/// </summary>
public sealed record BatchConversionResult(
    int TotalJobs,
    int SucceededJobs,
    int FailedJobs,
    long TotalDurationMs,
    IReadOnlyList<ConversionResult> Results);

/// <summary>
/// Progress reporting payload during batch conversion.
/// </summary>
public sealed record BatchProgressReport(
    int CompletedJobs,
    int TotalJobs,
    double PercentComplete,
    string CurrentFileName,
    bool LastJobSucceeded);
