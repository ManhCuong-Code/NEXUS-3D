using System.Collections.Concurrent;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.Infrastructure.Caching;

/// <summary>
/// Thread-safe in-memory cache with expiration support for metadata and lookup results.
/// </summary>
public sealed class InMemoryCacheService
{
    private sealed record CacheItem(object? Value, DateTimeOffset ExpiresAtUtc);

    private readonly ConcurrentDictionary<string, CacheItem> _entries = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _entries.Count;

    public void Set<T>(string key, T value, TimeSpan ttl)
    {
        Guard.NotNullOrWhiteSpace(key);
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.Add(ttl <= TimeSpan.Zero ? TimeSpan.FromMinutes(5) : ttl);
        _entries[key] = new CacheItem(value, expiresAt);
    }

    public bool TryGet<T>(string key, out T? value)
    {
        Guard.NotNullOrWhiteSpace(key);

        if (_entries.TryGetValue(key, out var item))
        {
            if (DateTimeOffset.UtcNow <= item.ExpiresAtUtc && item.Value is T typed)
            {
                value = typed;
                return true;
            }

            _entries.TryRemove(key, out _);
        }

        value = default;
        return false;
    }

    public void Remove(string key)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            _entries.TryRemove(key, out _);
        }
    }

    public void Clear() => _entries.Clear();
}

