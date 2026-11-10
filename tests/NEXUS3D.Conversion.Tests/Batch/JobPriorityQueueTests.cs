using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Conversion.Batch.Services;
using NEXUS3D.Domain.Enums;
using Xunit;

namespace NEXUS3D.Conversion.Tests.Batch;

public sealed class JobPriorityQueueTests
{
    [Fact]
    public async Task EnqueueAndDequeue_ShouldRespectPriorityOrder()
    {
        var queue = new JobPriorityQueue(TimeSpan.FromMinutes(10));

        var lowJob = new BatchJob("LowJob", "out", ModelFormatType.Obj, BatchJobPriority.Low);
        var normalJob = new BatchJob("NormalJob", "out", ModelFormatType.Obj, BatchJobPriority.Normal);
        var criticalJob = new BatchJob("CriticalJob", "out", ModelFormatType.Obj, BatchJobPriority.Critical);
        var highJob = new BatchJob("HighJob", "out", ModelFormatType.Obj, BatchJobPriority.High);

        await queue.EnqueueAsync(lowJob);
        await queue.EnqueueAsync(normalJob);
        await queue.EnqueueAsync(criticalJob);
        await queue.EnqueueAsync(highJob);

        Assert.Equal(4, queue.Count);

        var first = await queue.DequeueAsync();
        Assert.NotNull(first);
        Assert.Equal("CriticalJob", first.JobName);
        Assert.Equal(BatchJobPriority.Critical, first.Priority);

        var second = await queue.DequeueAsync();
        Assert.NotNull(second);
        Assert.Equal("HighJob", second.JobName);
        Assert.Equal(BatchJobPriority.High, second.Priority);

        var third = await queue.DequeueAsync();
        Assert.NotNull(third);
        Assert.Equal("NormalJob", third.JobName);
        Assert.Equal(BatchJobPriority.Normal, third.Priority);

        var fourth = await queue.DequeueAsync();
        Assert.NotNull(fourth);
        Assert.Equal("LowJob", fourth.JobName);
        Assert.Equal(BatchJobPriority.Low, fourth.Priority);

        Assert.Equal(0, queue.Count);
        var empty = await queue.DequeueAsync();
        Assert.Null(empty);
    }

    [Fact]
    public async Task EnqueueAndDequeue_WithinSamePriority_ShouldMaintainFifoOrder()
    {
        var queue = new JobPriorityQueue(TimeSpan.FromMinutes(10));

        var job1 = new BatchJob("Job1", "out", ModelFormatType.Obj, BatchJobPriority.Normal);
        var job2 = new BatchJob("Job2", "out", ModelFormatType.Obj, BatchJobPriority.Normal);
        var job3 = new BatchJob("Job3", "out", ModelFormatType.Obj, BatchJobPriority.Normal);

        await queue.EnqueueAsync(job1);
        await Task.Delay(10);
        await queue.EnqueueAsync(job2);
        await Task.Delay(10);
        await queue.EnqueueAsync(job3);

        var dequeued1 = await queue.DequeueAsync();
        var dequeued2 = await queue.DequeueAsync();
        var dequeued3 = await queue.DequeueAsync();

        Assert.Equal("Job1", dequeued1?.JobName);
        Assert.Equal("Job2", dequeued2?.JobName);
        Assert.Equal("Job3", dequeued3?.JobName);
    }

    [Fact]
    public async Task StarvationProtection_ShouldPromoteAgedLowerPriorityJob()
    {
        // Set aging interval to 100 milliseconds
        var queue = new JobPriorityQueue(TimeSpan.FromMilliseconds(100));

        var lowJob = new BatchJob("OldLowJob", "out", ModelFormatType.Obj, BatchJobPriority.Low)
        {
            EnqueuedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-5) // 5 seconds ago = 50 aging levels boost!
        };

        var criticalJob = new BatchJob("FreshCriticalJob", "out", ModelFormatType.Obj, BatchJobPriority.Critical)
        {
            EnqueuedAtUtc = DateTimeOffset.UtcNow
        };

        await queue.EnqueueAsync(lowJob);
        await queue.EnqueueAsync(criticalJob);

        // Due to 50 aging levels, lowJob effective score is 0.0 and enqueued earlier, so it is dequeued first!
        var first = await queue.DequeueAsync();
        Assert.NotNull(first);
        Assert.Equal("OldLowJob", first.JobName);
    }

    [Fact]
    public async Task UpdatePriority_ShouldModifyJobOrder()
    {
        var queue = new JobPriorityQueue(TimeSpan.FromMinutes(10));

        var jobA = new BatchJob("JobA", "out", ModelFormatType.Obj, BatchJobPriority.Low);
        var jobB = new BatchJob("JobB", "out", ModelFormatType.Obj, BatchJobPriority.Normal);

        await queue.EnqueueAsync(jobA);
        await queue.EnqueueAsync(jobB);

        // Promote JobA to Critical
        bool updated = await queue.UpdatePriorityAsync(jobA.JobId, BatchJobPriority.Critical);
        Assert.True(updated);

        var first = await queue.DequeueAsync();
        Assert.NotNull(first);
        Assert.Equal("JobA", first.JobName);
        Assert.Equal(BatchJobPriority.Critical, first.Priority);
    }

    [Fact]
    public async Task RemoveAsync_ShouldRemoveJobFromQueue()
    {
        var queue = new JobPriorityQueue(TimeSpan.FromMinutes(10));

        var job = new BatchJob("RemovableJob", "out", ModelFormatType.Obj, BatchJobPriority.Normal);
        await queue.EnqueueAsync(job);
        Assert.Equal(1, queue.Count);

        bool removed = await queue.RemoveAsync(job.JobId);
        Assert.True(removed);
        Assert.Equal(0, queue.Count);

        bool secondRemoval = await queue.RemoveAsync(job.JobId);
        Assert.False(secondRemoval);
    }
}
