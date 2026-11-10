using NEXUS3D.Domain.Entities;

namespace NEXUS3D.Domain.Repositories;

public interface IConversionHistoryRepository
{
    Task<IReadOnlyList<ConversionHistoryRecord>> GetRecentAsync(int limit = 50, CancellationToken cancellationToken = default);
    Task<ConversionHistoryRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(ConversionHistoryRecord record, CancellationToken cancellationToken = default);
    Task UpdateAsync(ConversionHistoryRecord record, CancellationToken cancellationToken = default);
    Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default);
    Task ClearHistoryAsync(CancellationToken cancellationToken = default);
}

public interface IRecentFileRepository
{
    Task<IReadOnlyList<RecentFileEntry>> GetAllAsync(int limit = 25, CancellationToken cancellationToken = default);
    Task UpsertAsync(RecentFileEntry entry, CancellationToken cancellationToken = default);
    Task RemoveAsync(Guid id, CancellationToken cancellationToken = default);
    Task ClearUnpinnedAsync(CancellationToken cancellationToken = default);
}

public interface IConversionProfileRepository
{
    Task<IReadOnlyList<ConversionProfile>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ConversionProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpsertAsync(ConversionProfile profile, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IExportConfigurationRepository
{
    Task<IReadOnlyList<ExportConfiguration>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ExportConfiguration?> GetDefaultAsync(CancellationToken cancellationToken = default);
    Task UpsertAsync(ExportConfiguration configuration, CancellationToken cancellationToken = default);
}

public interface IAppSettingRepository
{
    Task<IReadOnlyList<AppSettingEntry>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default);
    Task SetValueAsync(string key, string value, string category = "General", string description = "", CancellationToken cancellationToken = default);
}

public interface IPluginRegistrationRepository
{
    Task<IReadOnlyList<PluginRegistration>> GetAllAsync(CancellationToken cancellationToken = default);
    Task UpsertAsync(PluginRegistration plugin, CancellationToken cancellationToken = default);
    Task SetEnabledAsync(string pluginId, bool isEnabled, CancellationToken cancellationToken = default);
    Task DeleteAsync(string pluginId, CancellationToken cancellationToken = default);
}

public interface IBatchJobRepository
{
    Task AddJobAsync(BatchJobRecord job, CancellationToken cancellationToken = default);
    Task UpdateJobAsync(BatchJobRecord job, CancellationToken cancellationToken = default);
    Task<BatchJobRecord?> GetJobByIdAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BatchJobRecord>> GetRecentJobsAsync(int limit = 50, CancellationToken cancellationToken = default);
    Task DeleteJobAsync(Guid jobId, CancellationToken cancellationToken = default);
}


