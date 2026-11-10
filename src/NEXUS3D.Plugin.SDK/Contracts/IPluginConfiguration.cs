namespace NEXUS3D.Plugin.SDK.Contracts;

/// <summary>
/// Provides isolated, persistent key-value configuration storage for a specific plugin.
/// </summary>
public interface IPluginConfiguration
{
    T GetValue<T>(string key, T defaultValue = default!);
    void SetValue<T>(string key, T value);
    bool HasKey(string key);
    void RemoveKey(string key);
    Task SaveAsync(CancellationToken cancellationToken = default);
    Task ReloadAsync(CancellationToken cancellationToken = default);
}
