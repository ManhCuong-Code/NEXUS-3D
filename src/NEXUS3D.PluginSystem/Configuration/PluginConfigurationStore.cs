using System.Collections.Concurrent;
using System.Text.Json;
using NEXUS3D.Plugin.SDK.Contracts;

namespace NEXUS3D.PluginSystem.Configuration;

/// <summary>
/// Thread-safe per-plugin configuration storage serialized to a JSON file.
/// </summary>
public sealed class PluginConfigurationStore : IPluginConfiguration
{
    private readonly string _configFilePath;
    private readonly ConcurrentDictionary<string, JsonElement> _data = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public PluginConfigurationStore(string configFilePath)
    {
        _configFilePath = configFilePath;
        LoadInitial();
    }

    public T GetValue<T>(string key, T defaultValue = default!)
    {
        if (string.IsNullOrWhiteSpace(key)) return defaultValue;

        if (_data.TryGetValue(key, out var element))
        {
            try
            {
                return JsonSerializer.Deserialize<T>(element.GetRawText()) ?? defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        return defaultValue;
    }

    public void SetValue<T>(string key, T value)
    {
        if (string.IsNullOrWhiteSpace(key)) return;

        string json = JsonSerializer.Serialize(value);
        using var doc = JsonDocument.Parse(json);
        _data[key] = doc.RootElement.Clone();
    }

    public bool HasKey(string key) =>
        !string.IsNullOrWhiteSpace(key) && _data.ContainsKey(key);

    public void RemoveKey(string key)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            _data.TryRemove(key, out _);
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string dir = Path.GetDirectoryName(Path.GetFullPath(_configFilePath)) ?? Environment.CurrentDirectory;
            Directory.CreateDirectory(dir);

            var dict = _data.ToDictionary(k => k.Key, v => v.Value);
            string json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_configFilePath, json, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            LoadInitial();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private void LoadInitial()
    {
        _data.Clear();
        if (File.Exists(_configFilePath))
        {
            try
            {
                string json = File.ReadAllText(_configFilePath);
                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                if (dict is not null)
                {
                    foreach (var kvp in dict)
                    {
                        _data[kvp.Key] = kvp.Value;
                    }
                }
            }
            catch
            {
                // Start with empty store if file is corrupt
            }
        }
    }
}
