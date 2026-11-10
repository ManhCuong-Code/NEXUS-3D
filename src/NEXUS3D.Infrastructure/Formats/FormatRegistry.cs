using NEXUS3D.Domain.Enums;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Formats.BedrockGeometry;
using NEXUS3D.Formats.Obj;
using NEXUS3D.Formats.Schematic;
using NEXUS3D.Formats.VoxelJson;

namespace NEXUS3D.Infrastructure.Formats;

/// <summary>
/// Central registry of all built-in and plugin-provided 3D format descriptors.
/// </summary>
public sealed class FormatRegistry : IFormatRegistry
{
    private readonly List<IFormatDescriptor> _descriptors;
    private readonly object _syncRoot = new();

    public FormatRegistry(IEnumerable<IFormatDescriptor>? descriptors = null)
    {
        var provided = descriptors?.ToList();
        if (provided is { Count: > 0 })
        {
            _descriptors = provided;
        }
        else
        {
            _descriptors =
            [
                new SchematicFormatDescriptor(),
                new ObjFormatDescriptor(),
                new VoxelJsonFormatDescriptor(),
                new BedrockGeometryFormatDescriptor()
            ];
        }
    }

    public void RegisterDescriptor(IFormatDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        lock (_syncRoot)
        {
            // Avoid duplicate shortcodes or extensions
            if (!_descriptors.Any(d => string.Equals(d.ShortCode, descriptor.ShortCode, StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(d.PrimaryExtension, descriptor.PrimaryExtension, StringComparison.OrdinalIgnoreCase)))
            {
                _descriptors.Add(descriptor);
            }
        }
    }

    public bool UnregisterDescriptor(string shortCodeOrExtension)
    {
        if (string.IsNullOrWhiteSpace(shortCodeOrExtension)) return false;

        lock (_syncRoot)
        {
            var match = _descriptors.FirstOrDefault(d =>
                string.Equals(d.ShortCode, shortCodeOrExtension, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(d.PrimaryExtension, shortCodeOrExtension, StringComparison.OrdinalIgnoreCase) ||
                d.SupportedExtensions.Any(ext => string.Equals(ext, shortCodeOrExtension, StringComparison.OrdinalIgnoreCase)));

            if (match is not null)
            {
                return _descriptors.Remove(match);
            }

            return false;
        }
    }

    public IReadOnlyList<IFormatDescriptor> GetAllFormats()
    {
        lock (_syncRoot)
        {
            return _descriptors.ToList();
        }
    }

    public IReadOnlyList<IFormatDescriptor> GetImportableFormats()
    {
        lock (_syncRoot)
        {
            return _descriptors.Where(d => d.SupportsImport).ToList();
        }
    }

    public IReadOnlyList<IFormatDescriptor> GetExportableFormats()
    {
        lock (_syncRoot)
        {
            return _descriptors.Where(d => d.SupportsExport).ToList();
        }
    }

    public IFormatDescriptor? GetByFormatType(ModelFormatType formatType)
    {
        lock (_syncRoot)
        {
            return _descriptors.FirstOrDefault(d => d.FormatType == formatType);
        }
    }

    public IFormatDescriptor? ResolveByFilePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        lock (_syncRoot)
        {
            // Check longer compound extensions first (e.g. .geo.json before .json)
            foreach (var descriptor in _descriptors.OrderByDescending(d => d.PrimaryExtension.Length))
            {
                if (descriptor.CanHandleExtension(filePath))
                {
                    return descriptor;
                }
            }

            return null;
        }
    }

    public string BuildFileDialogFilter(bool includeAllSupported = true)
    {
        var segments = new List<string>();

        if (includeAllSupported)
        {
            var allPatterns = _descriptors
                .SelectMany(d => d.SupportedExtensions)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(ext => $"*{ext}");

            segments.Add($"All Supported 3D Models|{string.Join(";", allPatterns)}");
        }

        foreach (var descriptor in _descriptors)
        {
            var patterns = descriptor.SupportedExtensions.Select(ext => $"*{ext}");
            segments.Add($"{descriptor.DisplayName} ({string.Join(", ", patterns)})|{string.Join(";", patterns)}");
        }

        segments.Add("All Files (*.*)|*.*");
        return string.Join("|", segments);
    }
}

