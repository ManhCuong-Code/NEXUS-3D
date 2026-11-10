using NEXUS3D.Rendering.Services;
using Xunit;

namespace NEXUS3D.Rendering.Tests;

public sealed class RenderResourceManagerTests
{
    private sealed class DisposableTestObject : IDisposable
    {
        public bool IsDisposed { get; private set; }
        public void Dispose() => IsDisposed = true;
    }

    [Fact]
    public void TrackAndRetrieveResource_WorksProperly()
    {
        using var manager = new RenderResourceManager();
        var obj = new DisposableTestObject();

        manager.TrackResource("res-1", obj);

        Assert.Equal(1, manager.ActiveResourceCount);
        Assert.True(manager.TryGetResource<DisposableTestObject>("res-1", out var retrieved));
        Assert.Same(obj, retrieved);
    }

    [Fact]
    public void ReleaseResource_DisposesTrackedObject()
    {
        using var manager = new RenderResourceManager();
        var obj = new DisposableTestObject();

        manager.TrackResource("res-1", obj);
        manager.ReleaseResource("res-1");

        Assert.Equal(0, manager.ActiveResourceCount);
        Assert.True(obj.IsDisposed);
    }

    [Fact]
    public void ClearAll_DisposesAllResources()
    {
        using var manager = new RenderResourceManager();
        var obj1 = new DisposableTestObject();
        var obj2 = new DisposableTestObject();

        manager.TrackResource("res-1", obj1);
        manager.TrackResource("res-2", obj2);
        manager.ClearAll();

        Assert.Equal(0, manager.ActiveResourceCount);
        Assert.True(obj1.IsDisposed);
        Assert.True(obj2.IsDisposed);
    }

    [Fact]
    public void ConcurrentOperations_RemainThreadSafe()
    {
        using var manager = new RenderResourceManager();

        Parallel.For(0, 100, i =>
        {
            var key = $"item_{i}";
            var obj = new DisposableTestObject();
            manager.TrackResource(key, obj);
            manager.TryGetResource<DisposableTestObject>(key, out _);
            if (i % 2 == 0)
            {
                manager.ReleaseResource(key);
            }
        });

        Assert.Equal(50, manager.ActiveResourceCount);
    }
}

