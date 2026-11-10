using System.Globalization;
using NEXUS3D.Domain.Enums;

namespace Nexus3D.CLI.Arguments;

/// <summary>
/// Robust command-line argument parser for NEXUS-3D CLI.
/// </summary>
public static class CliArgumentParser
{
    public static ParsedArguments Parse(string[] args)
    {
        var result = new ParsedArguments();

        if (args.Length == 0)
        {
            result.Command = CliCommandType.Help;
            return result;
        }

        string first = args[0].Trim().ToLowerInvariant();

        if (first is "--help" or "-h" or "help" or "/?")
        {
            result.Command = CliCommandType.Help;
            return result;
        }

        if (first is "--version" or "-v" or "version")
        {
            result.Command = CliCommandType.Version;
            return result;
        }

        if (first == "convert")
        {
            result.Command = CliCommandType.Convert;
            ParseConvertArguments(args, 1, result);
            return result;
        }

        if (first == "batch")
        {
            result.Command = CliCommandType.Batch;
            ParseBatchArguments(args, 1, result);
            return result;
        }

        if (first == "jobs")
        {
            ParseJobsArguments(args, 1, result);
            return result;
        }

        // Unknown command
        result.Command = CliCommandType.Unknown;
        result.ParseError = $"Unknown command or flag '{args[0]}'. Use 'nexus3d --help' for available commands.";
        return result;
    }

    private static void ParseConvertArguments(string[] args, int startIndex, ParsedArguments result)
    {
        for (int i = startIndex; i < args.Length; i++)
        {
            string token = args[i].Trim();
            string lower = token.ToLowerInvariant();

            if (lower is "--input" or "-i")
            {
                if (i + 1 < args.Length) result.InputPath = args[++i];
                else { result.ParseError = "Missing value for --input parameter."; return; }
            }
            else if (lower is "--output" or "-o")
            {
                if (i + 1 < args.Length) result.OutputPath = args[++i];
                else { result.ParseError = "Missing value for --output parameter."; return; }
            }
            else if (lower == "--voxelize")
            {
                result.Voxelize = true;
            }
            else if (lower == "--overwrite")
            {
                result.Overwrite = true;
            }
            else if (lower == "--no-greedy")
            {
                result.GreedyMeshing = false;
            }
            else if (lower == "--scale")
            {
                if (i + 1 < args.Length && double.TryParse(args[++i], NumberStyles.Float, CultureInfo.InvariantCulture, out double s))
                {
                    result.ScaleFactor = Math.Max(0.001, s);
                }
                else
                {
                    result.ParseError = "Invalid or missing numeric value for --scale parameter.";
                    return;
                }
            }
            else
            {
                result.ParseError = $"Unrecognized option '{token}' for 'convert' command.";
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(result.InputPath))
        {
            result.ParseError = "The '--input' parameter is required for the 'convert' command.";
        }
        else if (string.IsNullOrWhiteSpace(result.OutputPath))
        {
            result.ParseError = "The '--output' parameter is required for the 'convert' command.";
        }
    }

    private static void ParseBatchArguments(string[] args, int startIndex, ParsedArguments result)
    {
        for (int i = startIndex; i < args.Length; i++)
        {
            string token = args[i].Trim();
            string lower = token.ToLowerInvariant();

            if (lower is "--input" or "-i")
            {
                if (i + 1 < args.Length) result.InputPath = args[++i];
                else { result.ParseError = "Missing value for --input parameter."; return; }
            }
            else if (lower is "--output" or "-o")
            {
                if (i + 1 < args.Length) result.OutputPath = args[++i];
                else { result.ParseError = "Missing value for --output parameter."; return; }
            }
            else if (lower is "--format" or "-f")
            {
                if (i + 1 < args.Length)
                {
                    result.TargetFormat = ParseFormatType(args[++i]);
                    if (result.TargetFormat == ModelFormatType.Unknown)
                    {
                        result.ParseError = $"Unsupported target format '{args[i]}'. Supported: obj, schem, voxeljson, bedrock.";
                        return;
                    }
                }
                else { result.ParseError = "Missing value for --format parameter."; return; }
            }
            else if (lower is "--extensions" or "-e")
            {
                if (i + 1 < args.Length)
                {
                    var exts = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    result.Extensions.AddRange(exts.Select(e => e.StartsWith('.') ? e : $".{e}"));
                }
                else { result.ParseError = "Missing value for --extensions parameter."; return; }
            }
            else if (lower is "--parallel" or "-p")
            {
                if (i + 1 < args.Length && int.TryParse(args[++i], out int p))
                {
                    result.Parallelism = Math.Clamp(p, 1, 64);
                }
                else { result.ParseError = "Invalid or missing integer value for --parallel parameter."; return; }
            }
            else if (lower == "--priority")
            {
                if (i + 1 < args.Length)
                {
                    result.Priority = ParsePriority(args[++i]);
                }
                else { result.ParseError = "Missing value for --priority parameter."; return; }
            }
            else if (lower == "--voxelize")
            {
                result.Voxelize = true;
            }
            else if (lower == "--overwrite")
            {
                result.Overwrite = true;
            }
            else if (lower == "--continue-on-error")
            {
                result.ContinueOnError = true;
            }
            else if (lower == "--stop-on-error")
            {
                result.ContinueOnError = false;
            }
            else if (lower == "--retry")
            {
                if (i + 1 < args.Length && int.TryParse(args[++i], out int r))
                {
                    result.RetryLimit = Math.Clamp(r, 0, 10);
                }
                else { result.ParseError = "Invalid or missing integer value for --retry parameter."; return; }
            }
            else if (lower == "--json-report")
            {
                if (i + 1 < args.Length) result.JsonReportPath = args[++i];
                else { result.ParseError = "Missing value for --json-report parameter."; return; }
            }
            else
            {
                result.ParseError = $"Unrecognized option '{token}' for 'batch' command.";
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(result.InputPath))
        {
            result.ParseError = "The '--input' parameter is required for the 'batch' command.";
        }
        else if (string.IsNullOrWhiteSpace(result.OutputPath))
        {
            result.ParseError = "The '--output' parameter is required for the 'batch' command.";
        }
        else if (result.TargetFormat == ModelFormatType.Unknown)
        {
            result.ParseError = "The '--format' parameter is required for the 'batch' command (e.g. --format obj, --format schem).";
        }
    }

    private static void ParseJobsArguments(string[] args, int startIndex, ParsedArguments result)
    {
        if (startIndex >= args.Length)
        {
            result.Command = CliCommandType.JobsList;
            return;
        }

        string sub = args[startIndex].Trim().ToLowerInvariant();

        if (sub == "list")
        {
            result.Command = CliCommandType.JobsList;
            for (int i = startIndex + 1; i < args.Length; i++)
            {
                if (args[i].ToLowerInvariant() == "--limit" && i + 1 < args.Length && int.TryParse(args[i + 1], out int lim))
                {
                    result.Limit = Math.Max(1, lim);
                    i++;
                }
            }
        }
        else if (sub == "status")
        {
            result.Command = CliCommandType.JobsStatus;
            if (startIndex + 1 < args.Length && Guid.TryParse(args[startIndex + 1], out var id))
            {
                result.JobId = id;
            }
            else
            {
                result.ParseError = "Missing or invalid GUID for 'jobs status <job-id>'.";
            }
        }
        else if (sub == "cancel")
        {
            result.Command = CliCommandType.JobsCancel;
            if (startIndex + 1 < args.Length && Guid.TryParse(args[startIndex + 1], out var id))
            {
                result.JobId = id;
            }
            else
            {
                result.ParseError = "Missing or invalid GUID for 'jobs cancel <job-id>'.";
            }
        }
        else
        {
            result.ParseError = $"Unknown jobs subcommand '{args[startIndex]}'. Available: list, status, cancel.";
        }
    }

    public static ModelFormatType ParseFormatType(string raw) => raw.Trim().ToLowerInvariant() switch
    {
        "obj" => ModelFormatType.Obj,
        "schem" or "schematic" => ModelFormatType.Schematic,
        "json" or "voxeljson" or "voxel.json" or "voxel" => ModelFormatType.VoxelJson,
        "bedrock" or "geo" or "geo.json" or "geometry" => ModelFormatType.BedrockGeometry,
        _ => ModelFormatType.Unknown
    };

    public static BatchJobPriority ParsePriority(string raw) => raw.Trim().ToLowerInvariant() switch
    {
        "critical" => BatchJobPriority.Critical,
        "high" => BatchJobPriority.High,
        "normal" => BatchJobPriority.Normal,
        "low" => BatchJobPriority.Low,
        _ => BatchJobPriority.Normal
    };
}

