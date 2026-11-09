using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.ValueObjects;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.Domain.Entities;

/// <summary>
/// Lightweight domain representation of a 3D model document's metadata.
/// Does not hold raw geometry buffers in memory or persistence.
/// </summary>
public sealed class ModelDocumentSummary
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; private set; }
    public string FilePath { get; private set; }
    public ModelFormatType Format { get; private set; }
    public GeometryKind GeometryKind { get; private set; }
    public long FileSizeBytes { get; private set; }
    public int ElementCount { get; private set; }
    public BoundingBox3D Bounds { get; private set; }
    public DateTimeOffset InspectedAtUtc { get; private set; }

    public ModelDocumentSummary(
        string name,
        string filePath,
        ModelFormatType format,
        GeometryKind geometryKind,
        long fileSizeBytes,
        int elementCount = 0,
        BoundingBox3D? bounds = null)
    {
        Name = Guard.NotNullOrWhiteSpace(name);
        FilePath = Guard.NotNullOrWhiteSpace(filePath);
        Format = format;
        GeometryKind = geometryKind;
        FileSizeBytes = Guard.NonNegative(fileSizeBytes);
        ElementCount = Math.Max(0, elementCount);
        Bounds = bounds ?? BoundingBox3D.Empty;
        InspectedAtUtc = DateTimeOffset.UtcNow;
    }
}

