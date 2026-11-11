using NEXUS3D.Domain.Entities;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Formats.Abstractions;

namespace NEXUS3D.Application.Models;

public enum NotificationSeverity
{
    Info = 0,
    Success = 1,
    Warning = 2,
    Error = 3
}

/// <summary>
/// Application notification item displayed in the Notification Panel and Status Bar.
/// </summary>
public sealed record NotificationMessage(
    Guid Id,
    string Title,
    string Message,
    NotificationSeverity Severity,
    string SourceModule,
    DateTimeOffset CreatedAtUtc,
    bool IsRead = false);

/// <summary>
/// Aggregated overview data for the NEXUS 3D Dashboard.
/// </summary>
public sealed record DashboardSummaryDto(
    int TotalConversionsCount,
    int RecentFilesCount,
    int ConversionProfilesCount,
    int SupportedFormatsCount,
    int ActivePluginsCount,
    bool IsDatabaseConnected,
    string DatabaseFilePath,
    string LogsDirectoryPath,
    IReadOnlyList<IFormatDescriptor> SupportedFormats,
    IReadOnlyList<RecentFileEntry> RecentFiles,
    IReadOnlyList<ConversionHistoryRecord> RecentHistory);

/// <summary>
/// Request to validate and register a conversion job in the workspace (Foundation Stage).
/// </summary>
public sealed record PrepareConversionRequest(
    string SourceFilePath,
    string OutputDirectory,
    ModelFormatType SourceFormat,
    ModelFormatType TargetFormat,
    Guid? ProfileId,
    double ScaleFactor,
    bool OptimizeMesh,
    bool GreedyMeshing,
    bool PreserveMaterials);

/// <summary>
/// Result of preparing a conversion task in the foundation workspace.
/// </summary>
public sealed record PrepareConversionResult(
    ConversionHistoryRecord HistoryRecord,
    ModelDocumentSummary DocumentSummary,
    string TargetFilePath,
    string StatusNote);

/// <summary>
/// User-configurable application preferences managed in the Settings module.
/// </summary>
public sealed record ApplicationPreferencesDto(
    string ThemeMode,
    string DefaultOutputDirectory,
    ModelFormatType DefaultTargetFormat,
    CoordinateAxisUp DefaultAxisUp,
    bool AutoOptimizeMesh,
    bool EnableDetailedFileLogging,
    int MaxRecentFiles,
    int DecimalPrecision,
    bool CreateDesktopShortcut = false);

