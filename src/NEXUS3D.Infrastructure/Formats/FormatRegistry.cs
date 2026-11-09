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

    public IReadOnlyList<IFormatDescriptor> GetAllFormats() => _descriptors;

    public IReadOnlyList<IFormatDescriptor> GetImportableFormats() =>
        _descriptors.Where(d => d.SupportsImport).ToList();

    public IReadOnlyList<IFormatDescriptor> GetExportableFormats() =>
        _descriptors.Where(d => d.SupportsExport).ToList();

    public IFormatDescriptor? GetByFormatType(ModelFormatType formatType) =>
        _descriptors.FirstOrDefault(d => d.FormatType == formatType);

    public IFormatDescriptor? ResolveByFilePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

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

