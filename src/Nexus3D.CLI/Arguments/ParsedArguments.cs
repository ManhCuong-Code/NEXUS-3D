using NEXUS3D.Domain.Enums;

namespace Nexus3D.CLI.Arguments;

public enum CliCommandType
{
    Help,
    Version,
    Convert,
    Batch,
    JobsList,
    JobsStatus,
    JobsCancel,
    Unknown
}

/// <summary>
/// Parsed representation of command line arguments and options.
/// </summary>
public sealed class ParsedArguments
{
    public CliCommandType Command { get; set; } = CliCommandType.Help;
    public string? InputPath { get; set; }
    public string? OutputPath { get; set; }
    public ModelFormatType TargetFormat { get; set; } = ModelFormatType.Unknown;
    public ModelFormatType SourceFormat { get; set; } = ModelFormatType.Unknown;
    public double ScaleFactor { get; set; } = 1.0;
    public bool Voxelize { get; set; }
    public bool Overwrite { get; set; }
    public bool GreedyMeshing { get; set; } = true;
    public int Parallelism { get; set; } = 4;
    public BatchJobPriority Priority { get; set; } = BatchJobPriority.Normal;
    public bool ContinueOnError { get; set; } = true;
    public int RetryLimit { get; set; } = 1;
    public string? JsonReportPath { get; set; }
    public List<string> Extensions { get; set; } = [];
    public Guid? JobId { get; set; }
    public int Limit { get; set; } = 20;
    public string? ParseError { get; set; }
}

