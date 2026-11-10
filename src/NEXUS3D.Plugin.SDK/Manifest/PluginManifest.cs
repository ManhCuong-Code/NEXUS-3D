using System.Text.Json.Serialization;

namespace NEXUS3D.Plugin.SDK.Manifest;

/// <summary>
/// Declares dependency information on another NEXUS-3D plugin.
/// </summary>
public sealed record PluginDependencyInfo
{
    [JsonPropertyName("pluginId")]
    public string PluginId { get; init; } = string.Empty;

    [JsonPropertyName("minimumVersion")]
    public string MinimumVersion { get; init; } = "1.0.0";

    [JsonPropertyName("isOptional")]
    public bool IsOptional { get; init; }
}

/// <summary>
/// Declarative manifest specification describing a NEXUS-3D plugin package.
/// </summary>
public sealed record PluginManifest
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0.0";

    [JsonPropertyName("author")]
    public string Author { get; init; } = "Unknown";

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("assembly")]
    public string Assembly { get; init; } = string.Empty;

    [JsonPropertyName("entryPoint")]
    public string EntryPoint { get; init; } = string.Empty;

    [JsonPropertyName("pluginApiVersion")]
    public string PluginApiVersion { get; init; } = "1.0.0";

    [JsonPropertyName("minimumHostVersion")]
    public string MinimumHostVersion { get; init; } = "1.0.0";

    [JsonPropertyName("supportedFormats")]
    public IReadOnlyList<string> SupportedFormats { get; init; } = [];

    [JsonPropertyName("dependencies")]
    public IReadOnlyList<PluginDependencyInfo> Dependencies { get; init; } = [];

    [JsonPropertyName("capabilities")]
    public IReadOnlyList<string> Capabilities { get; init; } = [];

    [JsonPropertyName("license")]
    public string? License { get; init; }

    [JsonPropertyName("website")]
    public string? Website { get; init; }

    [JsonPropertyName("sha256Checksum")]
    public string? Sha256Checksum { get; init; }
}

