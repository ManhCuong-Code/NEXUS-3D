using NEXUS3D.Domain.Enums;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.Domain.Entities;

/// <summary>
/// Persistent configuration for exporting 3D models to disk.
/// </summary>
public sealed class ExportConfiguration
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string ProfileName { get; set; } = "Default Export";
    public ModelFormatType TargetFormat { get; set; } = ModelFormatType.Obj;
    public string OutputDirectory { get; set; } = string.Empty;
    public bool OverwriteExisting { get; set; }
    public bool GenerateMtlFile { get; set; } = true;
    public bool CompressOutput { get; set; }
    public CoordinateAxisUp CoordinateSystem { get; set; } = CoordinateAxisUp.YUp;
    public int DecimalPrecision { get; set; } = 4;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ExportConfiguration()
    {
    }

    public ExportConfiguration(
        string profileName,
        ModelFormatType targetFormat,
        string outputDirectory,
        bool overwriteExisting = false,
        bool generateMtlFile = true,
        bool compressOutput = false,
        CoordinateAxisUp coordinateSystem = CoordinateAxisUp.YUp,
        int decimalPrecision = 4)
    {
        ProfileName = Guard.NotNullOrWhiteSpace(profileName);
        TargetFormat = targetFormat;
        OutputDirectory = outputDirectory ?? string.Empty;
        OverwriteExisting = overwriteExisting;
        GenerateMtlFile = generateMtlFile;
        CompressOutput = compressOutput;
        CoordinateSystem = coordinateSystem;
        DecimalPrecision = (int)Guard.InRange(decimalPrecision, 1, 10);
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// Application configuration setting entry stored in SQLite.
/// </summary>
public sealed class AppSettingEntry
{
    public string Key { get; init; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Category { get; init; } = "General";
    public string Description { get; init; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public AppSettingEntry()
    {
    }

    public AppSettingEntry(string key, string value, string category = "General", string description = "")
    {
        Key = Guard.NotNullOrWhiteSpace(key);
        Value = value ?? string.Empty;
        Category = string.IsNullOrWhiteSpace(category) ? "General" : category;
        Description = description ?? string.Empty;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// Metadata record for an installed or discovered NEXUS 3D plugin.
/// </summary>
public sealed class PluginRegistration
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string PluginId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = "1.0.0";
    public string Author { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string AssemblyPath { get; init; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public string SupportedFormats { get; init; } = string.Empty;
    public DateTimeOffset InstalledAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public PluginRegistration()
    {
    }

    public PluginRegistration(
        string pluginId,
        string name,
        string version,
        string author,
        string description,
        string assemblyPath,
        string supportedFormats,
        bool isEnabled = true)
    {
        PluginId = Guard.NotNullOrWhiteSpace(pluginId);
        Name = Guard.NotNullOrWhiteSpace(name);
        Version = Guard.NotNullOrWhiteSpace(version);
        Author = author ?? "Unknown";
        Description = description ?? string.Empty;
        AssemblyPath = assemblyPath ?? string.Empty;
        SupportedFormats = supportedFormats ?? string.Empty;
        IsEnabled = isEnabled;
        InstalledAtUtc = DateTimeOffset.UtcNow;
    }
}

