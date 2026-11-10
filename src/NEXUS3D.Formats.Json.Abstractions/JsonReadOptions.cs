namespace NEXUS3D.Formats.Json.Abstractions;

/// <summary>
/// Common safety limits and parsing configuration for JSON-based 3D model engines.
/// </summary>
public record JsonReadOptions
{
    public static JsonReadOptions Default { get; } = new();

    /// <summary>
    /// When true, unknown properties or out-of-range warnings are treated as hard errors.
    /// When false (compatibility mode), unknown properties emit a warning and are preserved in <see cref="JsonExtensionData"/>.
    /// </summary>
    public bool StrictMode { get; init; }

    /// <summary>Whether to preserve unrecognized JSON properties in <see cref="JsonExtensionData"/> (default: true).</summary>
    public bool PreserveExtensionData { get; init; } = true;

    /// <summary>Whether to allow trailing commas in JSON objects and arrays (default: true).</summary>
    public bool AllowTrailingCommas { get; init; } = true;

    /// <summary>Whether to allow <c>//</c> and <c>/* */</c> comments in JSON files (default: true).</summary>
    public bool AllowComments { get; init; } = true;

    /// <summary>Maximum allowed input stream size in bytes (default: 128 MB).</summary>
    public long MaxInputBytes { get; init; } = 128L * 1024L * 1024L;

    /// <summary>Maximum allowed JSON nesting depth (default: 64).</summary>
    public int MaxDepth { get; init; } = 64;

    /// <summary>Maximum allowed elements in any JSON array (default: 2,000,000).</summary>
    public int MaxArrayLength { get; init; } = 2_000_000;

    /// <summary>Optional source file name for diagnostics and provenance.</summary>
    public string? SourceFileName { get; init; }
}
