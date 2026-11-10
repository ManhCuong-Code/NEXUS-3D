namespace NEXUS3D.PluginSystem.Models;

/// <summary>
/// Lifecycle state of an installed plugin inside the runtime host.
/// </summary>
public enum PluginLifecycleStatus
{
    Unloaded = 0,
    Loaded = 1,
    Initialized = 2,
    Enabled = 3,
    Disabled = 4,
    Faulted = 5
}

