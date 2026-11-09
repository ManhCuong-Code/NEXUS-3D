using NEXUS3D.Domain.Validation;

namespace NEXUS3D.Formats.Obj.Materials;

/// <summary>
/// Represents a parsed Wavefront Material Template Library (<c>.mtl</c>) document containing one or more materials.
/// </summary>
public sealed class MtlDocument
{
    public string? SourcePath { get; set; }
    public List<string> HeaderComments { get; set; } = [];
    public List<MtlMaterial> Materials { get; set; } = [];

    public MtlMaterial? FindByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return Materials.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public MtlDocument DeepClone() => new()
    {
        SourcePath = SourcePath,
        HeaderComments = [.. HeaderComments],
        Materials = Materials.Select(m => m.DeepClone()).ToList()
    };
}

/// <summary>
/// Options controlling Wavefront MTL parsing behavior.
/// </summary>
public sealed class MtlParseOptions
{
    public bool StrictMode { get; init; }
    public int MaxLineLength { get; init; } = 65_536;
    public bool PreserveComments { get; init; } = true;
    public bool PreserveCustomProperties { get; init; } = true;

    public static MtlParseOptions Default { get; } = new();
}

/// <summary>
/// Outcome of parsing a Wavefront MTL document.
/// </summary>
public sealed class MtlParseResult
{
    public MtlDocument Document { get; init; } = new();
    public IReadOnlyList<ValidationIssue> Issues { get; init; } = [];
    public TimeSpan Elapsed { get; init; }

    public bool HasErrors => Issues.Any(i => i.Severity >= ValidationSeverity.Error);
}
