using NEXUS3D.Plugin.SDK.Manifest;
using NEXUS3D.PluginSystem.Dependencies;
using Xunit;

namespace NEXUS3D.Plugin.Tests;

public sealed class PluginDependencyResolverTests
{
    [Fact]
    public void ResolveOrder_IndependentPlugins_ReturnsAll()
    {
        var manifests = new[]
        {
            new PluginManifest { Id = "com.nexus.alpha", Name = "Alpha" },
            new PluginManifest { Id = "com.nexus.beta", Name = "Beta" }
        };

        var result = PluginDependencyResolver.ResolveOrder(manifests);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.SortedManifests.Count);
    }

    [Fact]
    public void ResolveOrder_LinearChain_ResolvesInTopologicalOrder()
    {
        // A depends on B, B depends on C -> Load order must initialize C before B, and B before A
        var manifests = new[]
        {
            new PluginManifest
            {
                Id = "com.nexus.a",
                Name = "A",
                Dependencies = [new PluginDependencyInfo { PluginId = "com.nexus.b" }]
            },
            new PluginManifest
            {
                Id = "com.nexus.b",
                Name = "B",
                Dependencies = [new PluginDependencyInfo { PluginId = "com.nexus.c" }]
            },
            new PluginManifest
            {
                Id = "com.nexus.c",
                Name = "C"
            }
        };

        var result = PluginDependencyResolver.ResolveOrder(manifests);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.SortedManifests.Count);

        var ids = result.SortedManifests.Select(p => p.Id).ToList();
        Assert.True(ids.IndexOf("com.nexus.c") < ids.IndexOf("com.nexus.b"));
        Assert.True(ids.IndexOf("com.nexus.b") < ids.IndexOf("com.nexus.a"));
    }

    [Fact]
    public void ResolveOrder_DirectCycle_ReturnsCycleError()
    {
        // A depends on B, B depends on A
        var manifests = new[]
        {
            new PluginManifest
            {
                Id = "com.nexus.a",
                Name = "A",
                Dependencies = [new PluginDependencyInfo { PluginId = "com.nexus.b" }]
            },
            new PluginManifest
            {
                Id = "com.nexus.b",
                Name = "B",
                Dependencies = [new PluginDependencyInfo { PluginId = "com.nexus.a" }]
            }
        };

        var result = PluginDependencyResolver.ResolveOrder(manifests);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.CircularDependencyCycle);
        Assert.Contains("com.nexus.a", result.CircularDependencyCycle);
        Assert.Contains("com.nexus.b", result.CircularDependencyCycle);
    }

    [Fact]
    public void ResolveOrder_IndirectCycle_ReturnsCycleError()
    {
        // A -> B -> C -> A
        var manifests = new[]
        {
            new PluginManifest
            {
                Id = "com.nexus.a",
                Name = "A",
                Dependencies = [new PluginDependencyInfo { PluginId = "com.nexus.b" }]
            },
            new PluginManifest
            {
                Id = "com.nexus.b",
                Name = "B",
                Dependencies = [new PluginDependencyInfo { PluginId = "com.nexus.c" }]
            },
            new PluginManifest
            {
                Id = "com.nexus.c",
                Name = "C",
                Dependencies = [new PluginDependencyInfo { PluginId = "com.nexus.a" }]
            }
        };

        var result = PluginDependencyResolver.ResolveOrder(manifests);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.CircularDependencyCycle);
        Assert.Contains("com.nexus.a", result.CircularDependencyCycle);
    }

    [Fact]
    public void ResolveOrder_MissingRequiredDependency_Fails()
    {
        var manifests = new[]
        {
            new PluginManifest
            {
                Id = "com.nexus.a",
                Name = "A",
                Dependencies = [new PluginDependencyInfo { PluginId = "com.nexus.nonexistent", IsOptional = false }]
            }
        };

        var result = PluginDependencyResolver.ResolveOrder(manifests);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.MissingDependencies, d => d.Contains("com.nexus.nonexistent"));
    }

    [Fact]
    public void ResolveOrder_MissingOptionalDependency_Succeeds()
    {
        var manifests = new[]
        {
            new PluginManifest
            {
                Id = "com.nexus.a",
                Name = "A",
                Dependencies = [new PluginDependencyInfo { PluginId = "com.nexus.optional", IsOptional = true }]
            }
        };

        var result = PluginDependencyResolver.ResolveOrder(manifests);

        Assert.True(result.IsSuccess);
        Assert.Single(result.SortedManifests);
        Assert.Equal("com.nexus.a", result.SortedManifests[0].Id);
    }
}
