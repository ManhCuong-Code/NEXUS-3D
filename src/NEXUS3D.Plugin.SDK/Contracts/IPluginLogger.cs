namespace NEXUS3D.Plugin.SDK.Contracts;

/// <summary>
/// Scoped logging contract provided to plugins, redirecting to the host application logger.
/// </summary>
public interface IPluginLogger
{
    void LogDebug(string message);
    void LogInformation(string message);
    void LogWarning(string message);
    void LogError(string message, Exception? exception = null);
}

