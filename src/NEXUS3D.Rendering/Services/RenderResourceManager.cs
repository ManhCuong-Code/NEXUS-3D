using System.Collections.Concurrent;
using NEXUS3D.Rendering.Contracts;

namespace NEXUS3D.Rendering.Services;

/// <summary>
/// Thread-safe tracker and lifecycle manager for GPU/rendering resources, buffers, and textures.
/// </summary>
public sealed class RenderResourceManager : IRenderResourceManager
{
    private readonly ConcurrentDictionary<string, object> _resources = new(StringComparer.OrdinalIgnoreCase);
    private bool _isDisposed;

    public int ActiveResourceCount => _resources.Count;

    public void TrackResource(string key, object resource)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(resource);

        _resources.AddOrUpdate(key, resource, (_, old) =>
        {
            if (old is IDisposable disposable && !ReferenceEquals(old, resource))
            {
                disposable.Dispose();
            }
            return resource;
        });
    }

    public bool TryGetResource<T>(string key, out T? resource) where T : class
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (_resources.TryGetValue(key, out var val) && val is T typed)
        {
            resource = typed;
            return true;
        }

        resource = null;
        return false;
    }

    public void ReleaseResource(string key)
    {
        if (_resources.TryRemove(key, out var resource) && resource is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    public void ClearAll()
    {
        foreach (var kvp in _resources)
        {
            if (kvp.Value is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
            }
        }
        _resources.Clear();
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            ClearAll();
            _isDisposed = true;
        }
    }
}

