using NEXUS3D.Application.Models;
using NEXUS3D.Domain.Entities;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Application.Interfaces;

/// <summary>
/// Resolves environment-safe directories and file paths for NEXUS 3D runtime data.
/// </summary>
public interface IApplicationPathsService
{
    string AppDataRootDirectory { get; }
    string DatabaseFilePath { get; }
    string ConfigFilePath { get; }
    string LogsDirectory { get; }
    string CacheDirectory { get; }
    string PluginsDirectory { get; }
    string ProfilesDirectory { get; }

    void EnsureDirectoriesExist();
}

/// <summary>
/// Initializes and migrates the local SQLite database schema on startup.
/// </summary>
public interface IDatabaseInitializer
{
    Task<OperationResult> InitializeAsync(CancellationToken cancellationToken = default);
    Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Publishes and tracks user-facing notifications and warnings.
/// </summary>
public interface INotificationService
{
    event EventHandler<NotificationMessage>? NotificationPublished;
    IReadOnlyList<NotificationMessage> GetNotifications();
    void Publish(string title, string message, NotificationSeverity severity = NotificationSeverity.Info, string sourceModule = "System");
    void MarkAllAsRead();
    void ClearAll();
}

/// <summary>
/// Provides aggregated metrics and quick actions for the Dashboard view.
/// </summary>
public interface IDashboardService
{
    Task<OperationResult<DashboardSummaryDto>> GetDashboardSummaryAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Orchestrates conversion workspace preparation, file inspection, and profile management.
/// </summary>
public interface IConverterPreparationService
{
    Task<OperationResult<IReadOnlyList<ConversionProfile>>> GetProfilesAsync(CancellationToken cancellationToken = default);
    Task<OperationResult<ModelDocumentSummary>> InspectModelFileAsync(string filePath, CancellationToken cancellationToken = default);
    Task<OperationResult<PrepareConversionResult>> PrepareConversionJobAsync(PrepareConversionRequest request, CancellationToken cancellationToken = default);
    Task<OperationResult> SaveProfileAsync(ConversionProfile profile, CancellationToken cancellationToken = default);
}

/// <summary>
/// Manages recent files and conversion history records.
/// </summary>
public interface IFileManagerService
{
    Task<OperationResult<IReadOnlyList<RecentFileEntry>>> GetRecentFilesAsync(int limit = 25, CancellationToken cancellationToken = default);
    Task<OperationResult<IReadOnlyList<ConversionHistoryRecord>>> GetConversionHistoryAsync(int limit = 50, CancellationToken cancellationToken = default);
    Task<OperationResult<RecentFileEntry>> RegisterFileAccessAsync(string filePath, CancellationToken cancellationToken = default);
    Task<OperationResult> TogglePinRecentFileAsync(RecentFileEntry entry, CancellationToken cancellationToken = default);
    Task<OperationResult> RemoveRecentFileAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OperationResult> ClearHistoryAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads and persists application preferences and export configurations.
/// </summary>
public interface ISettingsService
{
    Task<OperationResult<ApplicationPreferencesDto>> LoadPreferencesAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> SavePreferencesAsync(ApplicationPreferencesDto preferences, CancellationToken cancellationToken = default);
    Task<OperationResult<ApplicationPreferencesDto>> ResetToDefaultsAsync(CancellationToken cancellationToken = default);
}

