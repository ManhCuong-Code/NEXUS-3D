using NEXUS3D.Conversion.Abstractions.Interfaces;
using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.Conversion.Batch.Services;

/// <summary>
/// Thread-safe priority queue scheduling batch jobs according to urgency with starvation protection.
/// Uses dynamic aging promotion: waiting jobs are promoted by 1 priority level every aging interval.
/// </summary>
public sealed class JobPriorityQueue : IJobPriorityQueue
{
    private readonly List<BatchJob> _queue = [];
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly TimeSpan _agingInterval;

    public int Count
    {
        get
        {
            _lock.Wait();
            try
            {
                return _queue.Count;
            }
            finally
            {
                _lock.Release();
            }
        }
    }

    public JobPriorityQueue(TimeSpan? agingInterval = null)
    {
        _agingInterval = agingInterval ?? TimeSpan.FromSeconds(15);
    }

    public async Task EnqueueAsync(BatchJob job, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(job);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            int existingIndex = _queue.FindIndex(j => j.JobId == job.JobId);
            if (existingIndex >= 0)
            {
                _queue[existingIndex] = job;
                return;
            }

            job.EnqueuedAtUtc ??= DateTimeOffset.UtcNow;
            job.Status = BatchJobStatus.Queued;
            _queue.Add(job);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<BatchJob?> DequeueAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_queue.Count == 0)
            {
                return null;
            }

            var now = DateTimeOffset.UtcNow;

            // Find job with minimum effective priority score (0 = highest urgency)
            // Effective score = raw priority - (elapsed seconds / aging interval)
            BatchJob bestJob = _queue[0];
            int bestIndex = 0;
            double bestScore = CalculateEffectiveScore(bestJob, now);

            for (int i = 1; i < _queue.Count; i++)
            {
                var candidate = _queue[i];
                double score = CalculateEffectiveScore(candidate, now);

                if (score < bestScore)
                {
                    bestScore = score;
                    bestJob = candidate;
                    bestIndex = i;
                }
                else if (Math.Abs(score - bestScore) < 0.001)
                {
                    // Tie-breaker: FIFO by EnqueuedAtUtc
                    var bestEnqueue = bestJob.EnqueuedAtUtc ?? DateTimeOffset.MinValue;
                    var candEnqueue = candidate.EnqueuedAtUtc ?? DateTimeOffset.MinValue;
                    if (candEnqueue < bestEnqueue)
                    {
                        bestScore = score;
                        bestJob = candidate;
                        bestIndex = i;
                    }
                }
            }

            _queue.RemoveAt(bestIndex);
            bestJob.Status = BatchJobStatus.Running;
            bestJob.StartedAtUtc ??= now;
            return bestJob;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> RemoveAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            int index = _queue.FindIndex(j => j.JobId == jobId);
            if (index >= 0)
            {
                _queue.RemoveAt(index);
                return true;
            }

            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> UpdatePriorityAsync(Guid jobId, BatchJobPriority newPriority, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var job = _queue.FirstOrDefault(j => j.JobId == jobId);
            if (job is not null)
            {
                job.Priority = newPriority;
                return true;
            }

            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    public IReadOnlyList<BatchJob> GetQueuedJobs()
    {
        _lock.Wait();
        try
        {
            return _queue.ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    private double CalculateEffectiveScore(BatchJob job, DateTimeOffset now)
    {
        int raw = (int)job.Priority;
        var enqueued = job.EnqueuedAtUtc ?? now;
        double elapsedSeconds = Math.Max(0, (now - enqueued).TotalSeconds);
        double agingBoost = elapsedSeconds / _agingInterval.TotalSeconds;

        // Effective score: 0 = Critical, lower is more urgent
        return Math.Max(0.0, raw - agingBoost);
    }
}

