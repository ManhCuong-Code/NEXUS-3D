using System.Reflection;
using System.Runtime.Loader;

namespace NEXUS3D.PluginSystem.Loading;

/// <summary>
/// Isolated <see cref="AssemblyLoadContext"/> that manages the lifetime and isolated dependency resolution
/// of a third-party plugin assembly.
/// </summary>
public sealed class PluginLoadContext : AssemblyLoadContext
{
    private static readonly HashSet<string> SharedAssemblyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "NEXUS3D.Plugin.SDK",
        "NEXUS3D.Domain",
        "NEXUS3D.Formats.Abstractions",
        "NEXUS3D.Conversion.Abstractions",
        "NEXUS3D.Shared",
        "Microsoft.Extensions.Logging.Abstractions",
        "System.Runtime",
        "System.Collections",
        "System.Threading"
    };

    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _pluginDirectory;

    public PluginLoadContext(string pluginMainAssemblyPath, bool isCollectible = true)
        : base(name: Path.GetFileNameWithoutExtension(pluginMainAssemblyPath), isCollectible: isCollectible)
    {
        _resolver = new AssemblyDependencyResolver(pluginMainAssemblyPath);
        _pluginDirectory = Path.GetDirectoryName(Path.GetFullPath(pluginMainAssemblyPath)) ?? string.Empty;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name is not null && SharedAssemblyNames.Contains(assemblyName.Name))
        {
            // Delegate core framework and domain types to the default context for type identity compatibility
            return null;
        }

        string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath is not null && File.Exists(assemblyPath))
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        // Fallback: check directly in the plugin directory
        if (!string.IsNullOrEmpty(_pluginDirectory) && assemblyName.Name is not null)
        {
            string localFallback = Path.Combine(_pluginDirectory, $"{assemblyName.Name}.dll");
            if (File.Exists(localFallback))
            {
                return LoadFromAssemblyPath(localFallback);
            }
        }

        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath is not null && File.Exists(libraryPath))
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }
}

