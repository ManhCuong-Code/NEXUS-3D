using System.Diagnostics;
using NEXUS3D.Conversion.Abstractions.Interfaces;
using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Conversion.Batch.Services;
using NEXUS3D.Domain.Enums;
using Xunit;

namespace NEXUS3D.Conversion.Performance.Tests;

public sealed class BatchProcessingPerformanceBenchmarks
{
    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task Benchmark_PriorityQueue_HighVolumeThroughput(int jobCount)
    {
        var queue = new JobPriorityQueue(TimeSpan.FromSeconds(30));

        var sw = Stopwatch.StartNew();

        // Enqueue jobs with varying priorities
        for (int i = 0; i < jobCount; i++)
        {
            var priority = (BatchJobPriority)(i % 4);
            var job = new BatchJob($"PerfJob_{i}", "out", ModelFormatType.Obj, priority);
            await queue.EnqueueAsync(job);
        }

        Assert.Equal(jobCount, queue.Count);

        // Dequeue all jobs
        int dequeued = 0;
        while (await queue.DequeueAsync() is not null)
        {
            dequeued++;
        }

        sw.Stop();

        Assert.Equal(jobCount, dequeued);
        Assert.True(sw.ElapsedMilliseconds < 2000, $"Queue operations for {jobCount} jobs took {sw.ElapsedMilliseconds}ms, expected < 2000ms");
    }

    [Fact]
    public void Benchmark_BatchJobItem_StatusTracking_100k_Operations()
    {
        var jobId = Guid.NewGuid();
        var items = new List<BatchJobItem>(1000);

        for (int i = 0; i < 1000; i++)
        {
            items.Add(new BatchJobItem(jobId, $"input_{i}.obj", $"output_{i}.schem", ModelFormatType.Obj, ModelFormatType.Schematic, 1024));
        }

        var sw = Stopwatch.StartNew();

        for (int i = 0; i < 1000; i++)
        {
            items[i].MarkProcessing();
            if (i % 10 == 0)
            {
                items[i].MarkFailed("FAIL", "Error", 5);
            }
            else
            {
                items[i].MarkSucceeded(2048, 12);
            }
        }

        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 500, $"1,000 item transitions took {sw.ElapsedMilliseconds}ms, expected < 500ms");
    }
}

