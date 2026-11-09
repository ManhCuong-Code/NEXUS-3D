using System.Text.Json;
using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Coordinates;
using NEXUS3D.Domain.Interfaces;

namespace NEXUS3D.Domain.Metadata;

/// <summary>
/// Captures provenance of the source file from which a scene was imported.
/// Avoids storing absolute user machine paths to protect privacy.
/// </summary>
public sealed class SourceMetadata : IDeepCloneable<SourceMetadata>
{
    public string? OriginalFileName { get; set; }
    public string? OriginalExtension { get; set; }
    public string? OriginalFormatId { get; set; }
    public string? OriginalFormatVersion { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTimeOffset ImportedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? SourceChecksumSha256 { get; set; }
    public string? SourceApplication { get; set; }
    public Dictionary<string, string> ImportOptions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public CoordinateSystem OriginalCoordinateSystem { get; set; } = CoordinateSystem.DefaultUsm;

    public SourceMetadata DeepClone() => new()
    {
        OriginalFileName = OriginalFileName,
        OriginalExtension = OriginalExtension,
        OriginalFormatId = OriginalFormatId,
        OriginalFormatVersion = OriginalFormatVersion,
        FileSizeBytes = FileSizeBytes,
        ImportedAtUtc = ImportedAtUtc,
        SourceChecksumSha256 = SourceChecksumSha256,
        SourceApplication = SourceApplication,
        ImportOptions = new Dictionary<string, string>(ImportOptions, StringComparer.OrdinalIgnoreCase),
        OriginalCoordinateSystem = OriginalCoordinateSystem.DeepClone()
    };
}

/// <summary>
/// Stores format-specific header or version properties without coupling Domain to format libraries.
/// </summary>
public sealed class FormatMetadata : IDeepCloneable<FormatMetadata>
{
    public string FormatId { get; set; } = string.Empty;
    public string? Version { get; set; }
    public Dictionary<string, JsonElement> Properties { get; set; } = new(StringComparer.Ordinal);

    public FormatMetadata()
    {
    }

    public FormatMetadata(string formatId, string? version = null)
    {
        FormatId = DomainGuard.NotNullOrWhiteSpace(formatId);
        Version = version;
    }

    public FormatMetadata DeepClone()
    {
        var clone = new FormatMetadata
        {
            FormatId = FormatId,
            Version = Version
        };

        foreach (var kvp in Properties)
        {
            clone.Properties[kvp.Key] = kvp.Value.Clone();
        }

        return clone;
    }
}

/// <summary>
/// Namespaced extension bag allowing external formats and plugins to attach custom JSON properties.
/// Examples: "minecraft:sponge_schematic", "minecraft:bedrock_geometry", "wavefront:obj", "custom:studio".
/// </summary>
public sealed class ExtensionData : IDeepCloneable<ExtensionData>
{
    public string Namespace { get; set; } = string.Empty;
    public Dictionary<string, JsonElement> Values { get; set; } = new(StringComparer.Ordinal);

    public ExtensionData()
    {
    }

    public ExtensionData(string @namespace)
    {
        Namespace = DomainGuard.NotNullOrWhiteSpace(@namespace);
    }

    public bool IsValidNamespace =>
        !string.IsNullOrWhiteSpace(Namespace) &&
        Namespace.Contains(':', StringComparison.Ordinal) &&
        !Namespace.StartsWith(':') &&
        !Namespace.EndsWith(':');

    public ExtensionData DeepClone()
    {
        var clone = new ExtensionData { Namespace = Namespace };
        foreach (var kvp in Values)
        {
            clone.Values[kvp.Key] = kvp.Value.Clone();
        }

        return clone;
    }
}

