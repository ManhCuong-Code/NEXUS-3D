using NEXUS3D.Plugin.SDK.Manifest;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.PluginSystem.Dependencies;

/// <summary>
/// Result of resolving plugin dependencies, including ordered execution sequence and diagnostics.
/// </summary>
public sealed class DependencyResolutionResult
{
    public bool IsSuccess { get; init; }
    public IReadOnlyList<PluginManifest> SortedManifests { get; init; } = [];
    public IReadOnlyList<string> MissingDependencies { get; init; } = [];
    public string? CircularDependencyCycle { get; init; }
    public string? ErrorMessage { get; init; }

    public static DependencyResolutionResult Success(IReadOnlyList<PluginManifest> sorted) =>
        new() { IsSuccess = true, SortedManifests = sorted };

    public static DependencyResolutionResult Failure(string message, IReadOnlyList<string>? missing = null, string? cycle = null) =>
        new()
        {
            IsSuccess = false,
            ErrorMessage = message,
            MissingDependencies = missing ?? [],
            CircularDependencyCycle = cycle
        };
}

/// <summary>
/// Resolves plugin interdependencies, detects circular graphs, checks versions, and produces topological execution order.
/// </summary>
public static class PluginDependencyResolver
{
    /// <summary>
    /// Resolves dependencies and returns plugins in topological initialization order.
    /// </summary>
    public static DependencyResolutionResult ResolveOrder(IReadOnlyCollection<PluginManifest> manifests)
    {
        if (manifests.Count == 0)
        {
            return DependencyResolutionResult.Success([]);
        }

        var manifestMap = manifests.ToDictionary(m => m.Id, m => m, StringComparer.OrdinalIgnoreCase);
        var missingErrors = new List<string>();

        // 1. Check for missing dependencies and version constraints
        foreach (var manifest in manifests)
        {
            if (manifest.Dependencies is null) continue;

            foreach (var dep in manifest.Dependencies)
            {
                if (!manifestMap.TryGetValue(dep.PluginId, out var targetManifest))
                {
                    if (!dep.IsOptional)
                    {
                        missingErrors.Add($"Plugin '{manifest.Id}' requires missing dependency '{dep.PluginId}' (min v{dep.MinimumVersion}).");
                    }
                }
                else
                {
                    if (Version.TryParse(dep.MinimumVersion, out var minVersion) &&
                        Version.TryParse(targetManifest.Version, out var actualVersion))
                    {
                        if (actualVersion < minVersion)
                        {
                            missingErrors.Add($"Plugin '{manifest.Id}' requires dependency '{dep.PluginId}' >= v{minVersion}, but found v{actualVersion}.");
                        }
                    }
                }
            }
        }

        if (missingErrors.Count > 0)
        {
            return DependencyResolutionResult.Failure(
                string.Join("; ", missingErrors),
                missing: missingErrors);
        }

        // 2. Build graph for topological sorting & cycle detection
        // Adjacency: plugin -> list of plugins that depend on it OR plugin -> dependencies.
        // For startup ordering: if A depends on B, B must start before A. (Edge B -> A)
        var inDegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var outgoing = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var id in manifestMap.Keys)
        {
            inDegree[id] = 0;
            outgoing[id] = [];
        }

        foreach (var manifest in manifests)
        {
            if (manifest.Dependencies is null) continue;

            foreach (var dep in manifest.Dependencies)
            {
                if (manifestMap.ContainsKey(dep.PluginId))
                {
                    // dep.PluginId must come before manifest.Id
                    outgoing[dep.PluginId].Add(manifest.Id);
                    inDegree[manifest.Id]++;
                }
            }
        }

        // Kahn's algorithm
        var queue = new Queue<string>(inDegree.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key));
        var sorted = new List<PluginManifest>();

        while (queue.Count > 0)
        {
            string currentId = queue.Dequeue();
            sorted.Add(manifestMap[currentId]);

            foreach (var dependent in outgoing[currentId])
            {
                inDegree[dependent]--;
                if (inDegree[dependent] == 0)
                {
                    queue.Enqueue(dependent);
                }
            }
        }

        // 3. Cycle Detection
        if (sorted.Count < manifests.Count)
        {
            var cycleNodes = inDegree.Where(kvp => kvp.Value > 0).Select(kvp => kvp.Key).ToList();
            string cyclePath = FindCycleDescription(manifestMap, cycleNodes);
            return DependencyResolutionResult.Failure(
                $"Circular dependency detected among plugins: {cyclePath}",
                cycle: cyclePath);
        }

        return DependencyResolutionResult.Success(sorted);
    }

    private static string FindCycleDescription(
        Dictionary<string, PluginManifest> manifestMap,
        List<string> cycleNodes)
    {
        // Simple cycle path trace using DFS
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var recursionStack = new List<string>();

        foreach (var startNode in cycleNodes)
        {
            if (TraceCycle(startNode, manifestMap, visited, recursionStack, out string cycle))
            {
                return cycle;
            }
        }

        return string.Join(" <-> ", cycleNodes);
    }

    private static bool TraceCycle(
        string current,
        Dictionary<string, PluginManifest> manifestMap,
        HashSet<string> visited,
        List<string> stack,
        out string cycle)
    {
        int index = stack.IndexOf(current);
        if (index >= 0)
        {
            var loop = stack.Skip(index).Append(current);
            cycle = string.Join(" -> ", loop);
            return true;
        }

        if (visited.Contains(current))
        {
            cycle = string.Empty;
            return false;
        }

        visited.Add(current);
        stack.Add(current);

        if (manifestMap.TryGetValue(current, out var manifest) && manifest.Dependencies is not null)
        {
            foreach (var dep in manifest.Dependencies)
            {
                if (manifestMap.ContainsKey(dep.PluginId))
                {
                    if (TraceCycle(dep.PluginId, manifestMap, visited, stack, out cycle))
                    {
                        return true;
                    }
                }
            }
        }

        stack.RemoveAt(stack.Count - 1);
        cycle = string.Empty;
        return false;
    }
}
