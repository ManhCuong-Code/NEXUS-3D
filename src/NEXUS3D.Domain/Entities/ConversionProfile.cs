using NEXUS3D.Domain.Enums;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.Domain.Entities;

/// <summary>
/// Reusable preset of parameters for converting between 3D formats.
/// </summary>
public sealed class ConversionProfile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ModelFormatType SourceFormat { get; set; }
    public ModelFormatType TargetFormat { get; set; }
    public double ScaleFactor { get; set; } = 1.0;
    public bool OptimizeMesh { get; set; } = true;
    public bool GreedyMeshing { get; set; } = true;
    public bool PreserveMaterials { get; set; } = true;
    public bool FlipUVs { get; set; }
    public CoordinateAxisUp AxisUp { get; set; } = CoordinateAxisUp.YUp;
    public bool IsBuiltIn { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ConversionProfile()
    {
    }

    public ConversionProfile(
        string name,
        string description,
        ModelFormatType sourceFormat,
        ModelFormatType targetFormat,
        double scaleFactor = 1.0,
        bool optimizeMesh = true,
        bool greedyMeshing = true,
        bool preserveMaterials = true,
        bool isBuiltIn = false)
    {
        Name = Guard.NotNullOrWhiteSpace(name);
        Description = description ?? string.Empty;
        SourceFormat = sourceFormat;
        TargetFormat = targetFormat;
        ScaleFactor = Guard.InRange(scaleFactor, 0.0001, 10000.0);
        OptimizeMesh = optimizeMesh;
        GreedyMeshing = greedyMeshing;
        PreserveMaterials = preserveMaterials;
        IsBuiltIn = isBuiltIn;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}

