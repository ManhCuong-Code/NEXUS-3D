using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Domain.Enums;

namespace NEXUS3D.Conversion.Abstractions.Interfaces;

/// <summary>
/// Thread-safe priority queue scheduling batch jobs according to urgency with starvation protection.
/// </summary>
public interface IJobPriorityQueue
{
    int Count { get; }

    Task EnqueueAsync(BatchJob job, CancellationToken cancellationToken = default);

    Task<BatchJob?> DequeueAsync(CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<bool> UpdatePriorityAsync(Guid jobId, BatchJobPriority newPriority, CancellationToken cancellationToken = default);

    IReadOnlyList<BatchJob> GetQueuedJobs();
}

/// <summary>
/// Worker engine executing individual conversion tasks within a batch job under bounded concurrency.
/// </summary>
public interface IBatchWorker
{
    Task<BatchJobResult> ProcessJobAsync(
        BatchJob job,
        IProgress<BatchProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Service managing the lifecycle, queuing, parallel execution, and cancellation of multi-file batch conversion jobs.
/// </summary>
public interface IBatchProcessingService
{
    Task<BatchJob> CreateJobAsync(BatchJobRequest request, CancellationToken cancellationToken = default);

    Task<BatchJobResult> StartJobAsync(Guid jobId, IProgress<BatchProgress>? progress = null, CancellationToken cancellationToken = default);

    Task CancelJobAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task PauseJobAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task ResumeJobAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<BatchJob?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BatchJob>> GetJobsAsync(CancellationToken cancellationToken = default);

    Task<BatchJobResult> RetryFailedItemsAsync(Guid jobId, IProgress<BatchProgress>? progress = null, CancellationToken cancellationToken = default);

    Task DeleteJobAsync(Guid jobId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Real-time progress reporter dispatching batch status updates to UI and console listeners.
/// </summary>
public interface IBatchProgressReporter
{
    void Report(BatchProgress progress);

    event EventHandler<BatchProgress>? ProgressChanged;
}

