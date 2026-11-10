using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NEXUS3D.Domain.Entities;
using NEXUS3D.Domain.Repositories;
using NEXUS3D.Domain.Validation;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Plugin.SDK.Contracts;
using NEXUS3D.Plugin.SDK.Manifest;
using NEXUS3D.Plugin.SDK.Validation;
using NEXUS3D.PluginSystem.Configuration;
using NEXUS3D.PluginSystem.Dependencies;
using NEXUS3D.PluginSystem.Loading;
using NEXUS3D.PluginSystem.Logging;
using NEXUS3D.PluginSystem.Models;
using NEXUS3D.PluginSystem.Packages;
using NEXUS3D.PluginSystem.Versioning;
using NEXUS3D.Shared.Guards;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.PluginSystem.Services;

/// <summary>
/// Core implementation of the NEXUS-3D Plugin Lifecycle & Extension Host.
/// </summary>
public sealed class PluginManager : IPluginManager
{
    private readonly IPluginRegistrationRepository _pluginRepository;
    private readonly IFormatRegistry _formatRegistry;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PluginManager> _logger;
    private readonly string _pluginsDirectory;
    private readonly string _pluginsDataDirectory;
    private readonly ConcurrentDictionary<string, LoadedPluginInstance> _plugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);

    public PluginManager(
        IPluginRegistrationRepository pluginRepository,
        IFormatRegistry formatRegistry,
        IServiceProvider serviceProvider,
        ILogger<PluginManager> logger,
        string? customPluginsDirectory = null,
        string? customDataDirectory = null)
    {
        _pluginRepository = Guard.NotNull(pluginRepository);
        _formatRegistry = Guard.NotNull(formatRegistry);
        _serviceProvider = Guard.NotNull(serviceProvider);
        _logger = Guard.NotNull(logger);

        string root = customPluginsDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NEXUS-3D",
            "plugins");

        _pluginsDirectory = Path.GetFullPath(root);
        _pluginsDataDirectory = customDataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NEXUS-3D",
            "PluginData");

        Directory.CreateDirectory(_pluginsDirectory);
        Directory.CreateDirectory(_pluginsDataDirectory);
    }

    public async Task DiscoverAndLoadPluginsAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _logger.LogInformation("Scanning plugins in directory '{Directory}'...", _pluginsDirectory);

            var discoveredDirectories = new List<string>();
            if (Directory.Exists(_pluginsDirectory))
            {
                discoveredDirectories.AddRange(Directory.GetDirectories(_pluginsDirectory));
            }

            // Also check local ./plugins directory if different
            string localPlugins = Path.GetFullPath("plugins");
            if (Directory.Exists(localPlugins) && !string.Equals(localPlugins, _pluginsDirectory, StringComparison.OrdinalIgnoreCase))
            {
                discoveredDirectories.AddRange(Directory.GetDirectories(localPlugins));
            }

            var discoveredManifests = new List<(string Directory, PluginManifest Manifest)>();

            foreach (var dir in discoveredDirectories)
            {
                string manifestPath = Path.Combine(dir, PluginPackageManager.ManifestFileName);
                if (!File.Exists(manifestPath)) continue;

                try
                {
                    string json = await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false);
                    var manifest = JsonSerializer.Deserialize<PluginManifest>(json);
                    if (manifest is null) continue;

                    var validation = PluginManifestValidator.Validate(manifest);
                    if (!validation.IsValid)
                    {
                        _logger.LogWarning("Plugin in '{Dir}' has invalid manifest: {Errors}", dir, string.Join("; ", validation.Issues.Select(i => i.Message)));
                        var faulted = new LoadedPluginInstance(manifest, string.Empty, dir, PluginLifecycleStatus.Faulted)
                        {
                            FaultMessage = $"Manifest invalid: {validation.Issues.First().Message}"
                        };
                        _plugins[manifest.Id] = faulted;
                        continue;
                    }

                    if (!PluginApiVersionChecker.IsCompatible(manifest.PluginApiVersion, manifest.MinimumHostVersion, out string? compReason))
                    {
                        _logger.LogWarning("Plugin '{Id}' incompatible: {Reason}", manifest.Id, compReason);
                        var faulted = new LoadedPluginInstance(manifest, string.Empty, dir, PluginLifecycleStatus.Faulted)
                        {
                            FaultMessage = compReason
                        };
                        _plugins[manifest.Id] = faulted;
                        continue;
                    }

                    discoveredManifests.Add((dir, manifest));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading manifest in '{Dir}'.", dir);
                }
            }

            // Resolve dependencies and determine load order
            var resolution = PluginDependencyResolver.ResolveOrder(discoveredManifests.Select(d => d.Manifest).ToList());
            if (!resolution.IsSuccess)
            {
                _logger.LogWarning("Plugin dependency resolution failure: {Error}", resolution.ErrorMessage);

                // Mark circular or missing plugins as faulted
                foreach (var (dir, manifest) in discoveredManifests)
                {
                    if (resolution.MissingDependencies.Any(m => m.Contains(manifest.Id, StringComparison.OrdinalIgnoreCase)) ||
                        (resolution.CircularDependencyCycle is not null && resolution.CircularDependencyCycle.Contains(manifest.Id, StringComparison.OrdinalIgnoreCase)))
                    {
                        var faulted = new LoadedPluginInstance(manifest, string.Empty, dir, PluginLifecycleStatus.Faulted)
                        {
                            FaultMessage = resolution.ErrorMessage
                        };
                        _plugins[manifest.Id] = faulted;
                    }
                }
            }

            // Load repositories state to see which plugins are enabled
            var storedRegistrations = (await _pluginRepository.GetAllAsync(cancellationToken).ConfigureAwait(false))
                .ToDictionary(r => r.PluginId, r => r.IsEnabled, StringComparer.OrdinalIgnoreCase);

            var manifestDirMap = discoveredManifests.ToDictionary(d => d.Manifest.Id, d => d.Directory, StringComparer.OrdinalIgnoreCase);

            // Initialize in resolved dependency order
            var orderedManifests = resolution.IsSuccess ? resolution.SortedManifests : discoveredManifests.Select(d => d.Manifest).ToList();

            foreach (var manifest in orderedManifests)
            {
                if (!manifestDirMap.TryGetValue(manifest.Id, out var dir)) continue;

                // Check stored enabled preference (default to true)
                bool isEnabled = storedRegistrations.TryGetValue(manifest.Id, out bool storedState) ? storedState : true;

                await LoadAndInitializePluginAsync(manifest, dir, isEnabled, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task<OperationResult<LoadedPluginInstance>> InstallPluginAsync(
        string packageFilePath,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(packageFilePath);

        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(packageFilePath))
            {
                return OperationResult<LoadedPluginInstance>.Failure("PACKAGE_NOT_FOUND", $"Package file '{packageFilePath}' does not exist.");
            }

            // Use package file name or temp inspection to get target directory
            string tempInspectionDir = Path.Combine(Path.GetTempPath(), "nexus3d_unpack_" + Guid.NewGuid().ToString("N"));
            var unpackResult = PluginPackageManager.UnpackPlugin(packageFilePath, tempInspectionDir);
            if (unpackResult.IsFailure || unpackResult.Value is null)
            {
                return OperationResult<LoadedPluginInstance>.Failure(unpackResult.ErrorCode ?? "UNPACK_FAILED", unpackResult.ErrorMessage ?? "Failed to extract package.");
            }

            var manifest = unpackResult.Value;
            string targetPluginDir = Path.Combine(_pluginsDirectory, manifest.Id);

            // Move or copy to target plugin directory
            if (Directory.Exists(targetPluginDir))
            {
                // If plugin already exists, unload it first
                if (_plugins.TryGetValue(manifest.Id, out var existing))
                {
                    await ShutdownPluginInstanceAsync(existing, cancellationToken).ConfigureAwait(false);
                }
                Directory.Delete(targetPluginDir, recursive: true);
            }

            Directory.Move(tempInspectionDir, targetPluginDir);

            // Initialize plugin
            var instance = await LoadAndInitializePluginAsync(manifest, targetPluginDir, isEnabled: true, cancellationToken).ConfigureAwait(false);

            if (instance.Status == PluginLifecycleStatus.Faulted)
            {
                return OperationResult<LoadedPluginInstance>.Failure("PLUGIN_INIT_FAILED", instance.FaultMessage ?? "Plugin failed to initialize.");
            }

            return OperationResult<LoadedPluginInstance>.Success(instance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error installing plugin from '{PackagePath}'.", packageFilePath);
            return OperationResult<LoadedPluginInstance>.Failure("INSTALL_EXCEPTION", ex.Message);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task<OperationResult> EnablePluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(pluginId);

        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_plugins.TryGetValue(pluginId, out var instance))
            {
                return OperationResult.Failure("PLUGIN_NOT_FOUND", $"Plugin '{pluginId}' is not installed.");
            }

            if (instance.Status == PluginLifecycleStatus.Enabled)
            {
                return OperationResult.Success();
            }

            if (instance.Status == PluginLifecycleStatus.Faulted)
            {
                return OperationResult.Failure("PLUGIN_FAULTED", $"Cannot enable faulted plugin: {instance.FaultMessage}");
            }

            // If plugin was disabled, re-register descriptors
            foreach (var descriptor in instance.RegisteredDescriptors)
            {
                _formatRegistry.RegisterDescriptor(descriptor);
            }

            instance.Status = PluginLifecycleStatus.Enabled;
            await _pluginRepository.SetEnabledAsync(pluginId, isEnabled: true, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Plugin '{PluginId}' enabled.", pluginId);
            return OperationResult.Success();
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task<OperationResult> DisablePluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(pluginId);

        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_plugins.TryGetValue(pluginId, out var instance))
            {
                return OperationResult.Failure("PLUGIN_NOT_FOUND", $"Plugin '{pluginId}' is not installed.");
            }

            if (instance.Status == PluginLifecycleStatus.Disabled)
            {
                return OperationResult.Success();
            }

            // Unregister formats from central registry
            foreach (var descriptor in instance.RegisteredDescriptors)
            {
                _formatRegistry.UnregisterDescriptor(descriptor.ShortCode);
            }

            instance.Status = PluginLifecycleStatus.Disabled;
            await _pluginRepository.SetEnabledAsync(pluginId, isEnabled: false, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Plugin '{PluginId}' disabled.", pluginId);
            return OperationResult.Success();
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task<OperationResult> UninstallPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(pluginId);

        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_plugins.TryGetValue(pluginId, out var instance))
            {
                return OperationResult.Failure("PLUGIN_NOT_FOUND", $"Plugin '{pluginId}' is not installed.");
            }

            await ShutdownPluginInstanceAsync(instance, cancellationToken).ConfigureAwait(false);

            // Remove from dictionary
            _plugins.TryRemove(pluginId, out _);

            // Remove from database
            await _pluginRepository.DeleteAsync(pluginId, cancellationToken).ConfigureAwait(false);

            // Delete plugin directory
            if (Directory.Exists(instance.PluginDirectory))
            {
                try
                {
                    Directory.Delete(instance.PluginDirectory, recursive: true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not completely delete plugin folder '{Dir}' immediately (assembly might still be locked).", instance.PluginDirectory);
                }
            }

            _logger.LogInformation("Plugin '{PluginId}' uninstalled successfully.", pluginId);
            return OperationResult.Success();
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task<OperationResult> ReloadPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(pluginId);

        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_plugins.TryGetValue(pluginId, out var instance))
            {
                return OperationResult.Failure("PLUGIN_NOT_FOUND", $"Plugin '{pluginId}' is not installed.");
            }

            string dir = instance.PluginDirectory;
            var manifest = instance.Manifest;
            bool wasEnabled = instance.Status == PluginLifecycleStatus.Enabled;

            await ShutdownPluginInstanceAsync(instance, cancellationToken).ConfigureAwait(false);
            _plugins.TryRemove(pluginId, out _);

            await LoadAndInitializePluginAsync(manifest, dir, wasEnabled, cancellationToken).ConfigureAwait(false);
            return OperationResult.Success();
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public IReadOnlyList<LoadedPluginInstance> GetAllPlugins() =>
        _plugins.Values.ToList();

    public LoadedPluginInstance? GetPlugin(string pluginId) =>
        string.IsNullOrWhiteSpace(pluginId) ? null : _plugins.GetValueOrDefault(pluginId);

    public IReadOnlyList<IFormatDescriptor> GetActiveFormatDescriptors() =>
        _plugins.Values
            .Where(p => p.Status == PluginLifecycleStatus.Enabled)
            .SelectMany(p => p.RegisteredDescriptors)
            .ToList();

    public IReadOnlyList<IFormatParser> GetActiveParsers() =>
        _plugins.Values
            .Where(p => p.Status == PluginLifecycleStatus.Enabled)
            .SelectMany(p => p.RegisteredParsers)
            .ToList();

    public IReadOnlyList<IFormatExporter> GetActiveExporters() =>
        _plugins.Values
            .Where(p => p.Status == PluginLifecycleStatus.Enabled)
            .SelectMany(p => p.RegisteredExporters)
            .ToList();

    public IReadOnlyList<IFormatDetector> GetActiveDetectors() =>
        _plugins.Values
            .Where(p => p.Status == PluginLifecycleStatus.Enabled)
            .SelectMany(p => p.RegisteredDetectors)
            .ToList();

    public IReadOnlyList<IFormatValidator> GetActiveValidators() =>
        _plugins.Values
            .Where(p => p.Status == PluginLifecycleStatus.Enabled)
            .SelectMany(p => p.RegisteredValidators)
            .ToList();

    IReadOnlyList<IFormatParser> IDynamicFormatProvider.GetParsers() => GetActiveParsers();
    IReadOnlyList<IFormatExporter> IDynamicFormatProvider.GetExporters() => GetActiveExporters();

    private async Task<LoadedPluginInstance> LoadAndInitializePluginAsync(
        PluginManifest manifest,
        string pluginDirectory,
        bool isEnabled,
        CancellationToken cancellationToken)
    {
        string assemblyPath = Path.Combine(pluginDirectory, manifest.Assembly);
        if (!File.Exists(assemblyPath))
        {
            string releaseFallback = Path.Combine(pluginDirectory, "bin", "Release", "net10.0", manifest.Assembly);
            string debugFallback = Path.Combine(pluginDirectory, "bin", "Debug", "net10.0", manifest.Assembly);
            if (File.Exists(releaseFallback))
            {
                assemblyPath = releaseFallback;
            }
            else if (File.Exists(debugFallback))
            {
                assemblyPath = debugFallback;
            }
            else
            {
                var missingAssemblyInstance = new LoadedPluginInstance(manifest, assemblyPath, pluginDirectory, PluginLifecycleStatus.Faulted)
                {
                    FaultMessage = $"Assembly file '{manifest.Assembly}' not found in '{pluginDirectory}'."
                };
                _plugins[manifest.Id] = missingAssemblyInstance;
                return missingAssemblyInstance;
            }
        }

        PluginLoadContext? loadContext = null;
        try
        {
            loadContext = new PluginLoadContext(assemblyPath, isCollectible: true);
            var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);

            Type? entryType = null;
            if (!string.IsNullOrWhiteSpace(manifest.EntryPoint))
            {
                entryType = assembly.GetType(manifest.EntryPoint);
            }

            if (entryType is null)
            {
                entryType = assembly.GetTypes().FirstOrDefault(t => typeof(INexusPlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);
            }

            if (entryType is null || !typeof(INexusPlugin).IsAssignableFrom(entryType))
            {
                var entryFaultInstance = new LoadedPluginInstance(manifest, assemblyPath, pluginDirectory, PluginLifecycleStatus.Faulted)
                {
                    FaultMessage = $"EntryPoint '{manifest.EntryPoint}' does not implement INexusPlugin."
                };
                _plugins[manifest.Id] = entryFaultInstance;
                return entryFaultInstance;
            }

            var plugin = (INexusPlugin)Activator.CreateInstance(entryType)!;
            var instance = new LoadedPluginInstance(manifest, assemblyPath, pluginDirectory, PluginLifecycleStatus.Loaded)
            {
                Plugin = plugin,
                LoadContext = loadContext
            };

            // Setup isolated directories and context
            string pluginDataDir = Path.Combine(_pluginsDataDirectory, manifest.Id);
            string logsDir = Path.Combine(pluginDataDir, "logs");
            string configFile = Path.Combine(pluginDataDir, "config.json");

            Directory.CreateDirectory(pluginDataDir);
            Directory.CreateDirectory(logsDir);

            var pluginLogger = new PluginLogger(manifest.Id, logsDir, _logger);
            var pluginConfig = new PluginConfigurationStore(configFile);
            var registrationContext = new PluginRegistrationContext(instance);

            var context = new PluginContext(
                _serviceProvider,
                pluginLogger,
                pluginConfig,
                registrationContext,
                pluginDirectory,
                pluginDataDir);

            instance.Context = context;

            // Execute Plugin InitializeAsync
            var initResult = await plugin.InitializeAsync(context, cancellationToken).ConfigureAwait(false);
            if (initResult.IsFailure)
            {
                instance.Status = PluginLifecycleStatus.Faulted;
                instance.FaultMessage = $"InitializeAsync failed: {initResult.ErrorMessage}";
                _plugins[manifest.Id] = instance;
                return instance;
            }

            instance.Status = isEnabled ? PluginLifecycleStatus.Enabled : PluginLifecycleStatus.Disabled;

            // Register with central format registry if enabled
            if (isEnabled)
            {
                foreach (var descriptor in instance.RegisteredDescriptors)
                {
                    _formatRegistry.RegisterDescriptor(descriptor);
                }
            }

            // Sync with domain repository
            var dbRecord = new PluginRegistration(
                pluginId: manifest.Id,
                name: manifest.Name,
                version: manifest.Version,
                author: manifest.Author,
                description: manifest.Description,
                assemblyPath: assemblyPath,
                supportedFormats: string.Join(", ", manifest.SupportedFormats),
                isEnabled: isEnabled);

            await _pluginRepository.UpsertAsync(dbRecord, cancellationToken).ConfigureAwait(false);

            _plugins[manifest.Id] = instance;
            _logger.LogInformation("Plugin '{Name}' (v{Version}) initialized with status {Status}.", manifest.Name, manifest.Version, instance.Status);
            return instance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed loading plugin '{Id}' from '{Path}'.", manifest.Id, assemblyPath);
            var faultInstance = new LoadedPluginInstance(manifest, assemblyPath, pluginDirectory, PluginLifecycleStatus.Faulted)
            {
                FaultMessage = ex.Message,
                FaultException = ex
            };
            _plugins[manifest.Id] = faultInstance;
            return faultInstance;
        }
    }

    private async Task ShutdownPluginInstanceAsync(LoadedPluginInstance instance, CancellationToken cancellationToken)
    {
        try
        {
            // Unregister descriptors from FormatRegistry
            foreach (var descriptor in instance.RegisteredDescriptors)
            {
                _formatRegistry.UnregisterDescriptor(descriptor.ShortCode);
            }

            if (instance.Plugin is not null)
            {
                await instance.Plugin.ShutdownAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception shutting down plugin '{Id}'.", instance.Manifest.Id);
        }
        finally
        {
            instance.Status = PluginLifecycleStatus.Unloaded;
            var ctx = instance.LoadContext;
            instance.LoadContext = null;
            instance.Plugin = null;
            ctx?.Unload();
        }
    }
}
