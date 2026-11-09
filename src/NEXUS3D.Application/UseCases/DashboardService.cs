using Microsoft.Extensions.Logging;
using NEXUS3D.Application.Interfaces;
using NEXUS3D.Application.Models;
using NEXUS3D.Domain.Repositories;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Shared.Guards;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Application.UseCases;

/// <summary>
/// Aggregates operational telemetry, recent files, and supported 3D formats for the Dashboard.
/// </summary>
public sealed class DashboardService : IDashboardService
{
    private readonly IConversionHistoryRepository _historyRepository;
    private readonly IRecentFileRepository _recentFileRepository;
    private readonly IConversionProfileRepository _profileRepository;
    private readonly IPluginRegistrationRepository _pluginRepository;
    private readonly IFormatRegistry _formatRegistry;
    private readonly IDatabaseInitializer _databaseInitializer;
    private readonly IApplicationPathsService _pathsService;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(
        IConversionHistoryRepository historyRepository,
        IRecentFileRepository recentFileRepository,
        IConversionProfileRepository profileRepository,
        IPluginRegistrationRepository pluginRepository,
        IFormatRegistry formatRegistry,
        IDatabaseInitializer databaseInitializer,
        IApplicationPathsService pathsService,
        ILogger<DashboardService> logger)
    {
        _historyRepository = Guard.NotNull(historyRepository);
        _recentFileRepository = Guard.NotNull(recentFileRepository);
        _profileRepository = Guard.NotNull(profileRepository);
        _pluginRepository = Guard.NotNull(pluginRepository);
        _formatRegistry = Guard.NotNull(formatRegistry);
        _databaseInitializer = Guard.NotNull(databaseInitializer);
        _pathsService = Guard.NotNull(pathsService);
        _logger = Guard.NotNull(logger);
    }

    public async Task<OperationResult<DashboardSummaryDto>> GetDashboardSummaryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            bool dbHealthy = await _databaseInitializer.CheckHealthAsync(cancellationToken).ConfigureAwait(false);
            var totalConversions = await _historyRepository.GetTotalCountAsync(cancellationToken).ConfigureAwait(false);
            var recentHistory = await _historyRepository.GetRecentAsync(10, cancellationToken).ConfigureAwait(false);
            var recentFiles = await _recentFileRepository.GetAllAsync(10, cancellationToken).ConfigureAwait(false);
            var profiles = await _profileRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
            var plugins = await _pluginRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
            var formats = _formatRegistry.GetAllFormats();

            var summary = new DashboardSummaryDto(
                TotalConversionsCount: totalConversions,
                RecentFilesCount: recentFiles.Count,
                ConversionProfilesCount: profiles.Count,
                SupportedFormatsCount: formats.Count,
                ActivePluginsCount: plugins.Count(p => p.IsEnabled),
                IsDatabaseConnected: dbHealthy,
                DatabaseFilePath: _pathsService.DatabaseFilePath,
                LogsDirectoryPath: _pathsService.LogsDirectory,
                SupportedFormats: formats,
                RecentFiles: recentFiles,
                RecentHistory: recentHistory);

            return OperationResult<DashboardSummaryDto>.Success(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load dashboard summary.");
            return OperationResult<DashboardSummaryDto>.Failure("DASHBOARD_LOAD_ERR", ex.Message);
        }
    }
}

