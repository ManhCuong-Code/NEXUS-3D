using NEXUS3D.Domain.Metadata;
using NEXUS3D.Domain.Validation;

namespace NEXUS3D.Formats.Obj.Conversion;

/// <summary>
/// Detailed conversion metrics and data fidelity report for OBJ &lt;-&gt; USM conversions.
/// </summary>
public sealed class ObjConversionReport
{
    public int SharedPositionCount { get; init; }
    public int SharedTexCoordCount { get; init; }
    public int SharedNormalCount { get; init; }
    public int ParameterVertexCount { get; init; }
    public int ExpandedUsmVertexCount { get; init; }
    public int FaceCount { get; init; }
    public int PolylineCount { get; init; }
    public int PointElementCount { get; init; }
    public int ObjectCount { get; init; }
    public int GroupCount { get; init; }
    public int SmoothingGroupCount { get; init; }
    public int MaterialCount { get; init; }
    public int TextureCount { get; init; }
    public IReadOnlyList<ValidationIssue> Issues { get; init; } = [];
    public IReadOnlyList<WarningRecord> Warnings { get; init; } = [];
    public LossReport LossReport { get; init; } = new();
    public TimeSpan Elapsed { get; init; }
}

