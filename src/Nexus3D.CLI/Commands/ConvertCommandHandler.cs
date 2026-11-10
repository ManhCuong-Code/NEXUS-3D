using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NEXUS3D.Conversion.Abstractions;
using NEXUS3D.Conversion.Abstractions.Interfaces;
using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Domain.Enums;
using Nexus3D.CLI.Arguments;

namespace Nexus3D.CLI.Commands;

public sealed class ConvertCommandHandler
{
    private readonly IUniversalConversionPipeline _pipeline;
    private readonly ILogger<ConvertCommandHandler> _logger;

    public ConvertCommandHandler(
        IUniversalConversionPipeline pipeline,
        ILogger<ConvertCommandHandler> logger)
    {
        _pipeline = pipeline;
        _logger = logger;
    }

    public async Task<int> ExecuteAsync(ParsedArguments args, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(args.InputPath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: Missing required input file path.");
            Console.ResetColor();
            return CliExitCodes.InvalidArguments;
        }

        if (!File.Exists(args.InputPath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: Input file does not exist: '{args.InputPath}'");
            Console.ResetColor();
            return CliExitCodes.InvalidInputFile;
        }

        if (string.IsNullOrWhiteSpace(args.OutputPath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: Missing required output file path.");
            Console.ResetColor();
            return CliExitCodes.InvalidArguments;
        }

        if (File.Exists(args.OutputPath) && !args.Overwrite)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: Output file already exists: '{args.OutputPath}'. Use --overwrite to replace it.");
            Console.ResetColor();
            return CliExitCodes.InvalidArguments;
        }

        var sourceFormat = DetectFormat(args.InputPath);
        var targetFormat = DetectFormat(args.OutputPath);

        if (sourceFormat == ModelFormatType.Unknown)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: Unable to determine source format from extension '{Path.GetExtension(args.InputPath)}'.");
            Console.ResetColor();
            return CliExitCodes.InvalidInputFile;
        }

        if (targetFormat == ModelFormatType.Unknown)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: Unable to determine target format from extension '{Path.GetExtension(args.OutputPath)}'.");
            Console.ResetColor();
            return CliExitCodes.InvalidArguments;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[NEXUS-3D] Converting: {Path.GetFileName(args.InputPath)} ({sourceFormat}) -> {Path.GetFileName(args.OutputPath)} ({targetFormat})");
        Console.ResetColor();

        string? outDir = Path.GetDirectoryName(args.OutputPath);
        if (!string.IsNullOrWhiteSpace(outDir))
        {
            Directory.CreateDirectory(outDir);
        }

        var options = new ConversionOptions
        {
            ScaleFactor = args.ScaleFactor,
            MeshingMode = args.GreedyMeshing ? MeshingAlgorithm.GreedyMeshing : MeshingAlgorithm.ExposedFaceCulling,
            VoxelizationMode = args.Voxelize ? VoxelizationMode.SurfaceOnly : VoxelizationMode.SurfaceOnly,
            PreserveMaterials = true
        };

        var request = new ConversionRequest(
            SourceFilePath: args.InputPath,
            TargetFilePath: args.OutputPath,
            SourceFormat: sourceFormat,
            TargetFormat: targetFormat,
            Options: options);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await _pipeline.ExecuteAsync(request, null, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            if (result.IsSuccess)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[SUCCESS] Conversion completed in {result.DurationMs}ms.");
                Console.WriteLine($"  Source Size: {result.SourceSizeBytes:N0} bytes");
                Console.WriteLine($"  Target Size: {result.TargetSizeBytes:N0} bytes");
                Console.WriteLine($"  Output File: {result.TargetFilePath}");
                Console.ResetColor();

                if (result.Warnings.Count > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  Warnings ({result.Warnings.Count}):");
                    foreach (var w in result.Warnings.Take(5))
                    {
                        Console.WriteLine($"    - {w}");
                    }
                    Console.ResetColor();
                }

                return CliExitCodes.Success;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAILURE] Conversion failed after {result.DurationMs}ms: {result.ErrorMessage}");
                Console.ResetColor();
                return CliExitCodes.ConversionFailure;
            }
        }
        catch (OperationCanceledException)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[CANCELLED] Conversion cancelled by user.");
            Console.ResetColor();
            return CliExitCodes.CancelledByUser;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] Unhandled conversion error: {ex.Message}");
            Console.ResetColor();
            _logger.LogError(ex, "Conversion error");
            return CliExitCodes.ConversionFailure;
        }
    }

    private static ModelFormatType DetectFormat(string path)
    {
        string name = Path.GetFileName(path).ToLowerInvariant();
        if (name.EndsWith(".geo.json", StringComparison.OrdinalIgnoreCase)) return ModelFormatType.BedrockGeometry;
        if (name.EndsWith(".voxel.json", StringComparison.OrdinalIgnoreCase)) return ModelFormatType.VoxelJson;

        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".obj" => ModelFormatType.Obj,
            ".schem" or ".schematic" => ModelFormatType.Schematic,
            ".json" => ModelFormatType.VoxelJson,
            _ => ModelFormatType.Unknown
        };
    }
}
