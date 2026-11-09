using System.Diagnostics;
using NEXUS3D.Infrastructure.Caching;

namespace NEXUS3D.Performance.Tests;

public sealed class FoundationPerformanceTests
{
    [Fact]
    public void InMemoryCacheService_ShouldHandleTenThousandOperationsWithinThreshold()
    {
        var cache = new InMemoryCacheService();
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < 10_000; i++)
        {
            cache.Set($"model_meta_{i}", i, TimeSpan.FromMinutes(5));
        }

        int hits = 0;
        for (int i = 0; i < 10_000; i++)
        {
            if (cache.TryGet<int>($"model_meta_{i}", out _))
            {
                hits++;
            }
        }

        sw.Stop();
        Assert.Equal(10_000, hits);
        Assert.True(sw.ElapsedMilliseconds < 2000, $"Cache benchmark took {sw.ElapsedMilliseconds}ms");
    }
}

