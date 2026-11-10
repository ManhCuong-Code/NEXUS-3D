using Microsoft.Extensions.Logging;
using NEXUS3D.Conversion.Abstractions.Interfaces;
using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Domain.Enums;
using Nexus3D.CLI.Arguments;
using Nexus3D.CLI.Formatting;

namespace Nexus3D.CLI.Commands;

public sealed class BatchCommandHandler
{
    private readonly IBatchProcessingService _batchService;
    private readonly ILogger<BatchCommandHandler> _logger;

    public BatchCommandHandler(
        IBatchProcessingService batchService,
        ILogger<BatchCommandHandler> logger)
    {
        _batchService = batchService;
        _logger = logger;
    }

    public async Task<int> ExecuteAsync(ParsedArguments args, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(args.InputPath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: Missing required --input directory path.");
            Console.ResetColor();
            return CliExitCodes.InvalidArguments;
        }

        if (!Directory.Exists(args.InputPath) && !File.Exists(args.InputPath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: Input directory or file does not exist: '{args.InputPath}'");
            Console.ResetColor();
            return CliExitCodes.InvalidInputFile;
        }

        if (string.IsNullOrWhiteSpace(args.OutputPath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: Missing required --output directory path.");
            Console.ResetColor();
            return CliExitCodes.InvalidArguments;
        }

        if (args.TargetFormat == ModelFormatType.Unknown)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: Missing or invalid --format parameter.");
            Console.ResetColor();
            return CliExitCodes.InvalidArguments;
        }

        Directory.CreateDirectory(args.OutputPath);

        string jobName = $"CLI-Batch-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        var filters = args.Extensions.Count > 0 ? args.Extensions : null;

        var request = new BatchJobRequest(
            JobName: jobName,
            InputFiles: File.Exists(args.InputPath) ? [args.InputPath] : [],
            InputDirectory: Directory.Exists(args.InputPath) ? args.InputPath : null,
            OutputDirectory: args.OutputPath,
            TargetFormat: args.TargetFormat,
            Priority: args.Priority,
            MaxConcurrency: args.Parallelism,
            OverwriteExisting: args.Overwrite,
            ContinueOnError: args.ContinueOnError,
            RetryLimit: args.RetryLimit,
            ExtensionFilter: filters);

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[NEXUS-3D] Initializing Batch Job: {jobName}");
        Console.WriteLine($"  Input:       {args.InputPath}");
        Console.WriteLine($"  Output:      {args.OutputPath}");
        Console.WriteLine($"  Target:      {args.TargetFormat}");
        Console.WriteLine($"  Concurrency: {args.Parallelism} threads");
        Console.WriteLine($"  Priority:    {args.Priority}");
        Console.WriteLine($"  Overwrite:   {args.Overwrite}");
        Console.ResetColor();
        Console.WriteLine();

        try
        {
            var job = await _batchService.CreateJobAsync(request, cancellationToken).ConfigureAwait(false);

            if (job.TotalFiles == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Warning: No matching 3D model files found in input path.");
                Console.ResetColor();
                return CliExitCodes.Success;
            }

            Console.WriteLine($"Queued {job.TotalFiles} model files for conversion...");

            var progress = new Progress<BatchProgress>(report =>
            {
                ConsoleProgressBar.Render(report);
            });

            var result = await _batchService.StartJobAsync(job.JobId, progress, cancellationToken).ConfigureAwait(false);
            ConsoleProgressBar.Finish();

            Console.WriteLine();
            PrintBatchSummary(result);

            if (!string.IsNullOrWhiteSpace(args.JsonReportPath))
            {
                await JsonReportGenerator.SaveToFileAsync(result, args.JsonReportPath, cancellationToken).ConfigureAwait(false);
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"JSON execution report saved to: {args.JsonReportPath}");
                Console.ResetColor();
            }

            if (result.Status == BatchJobStatus.Cancelled)
            {
                return CliExitCodes.CancelledByUser;
            }

            if (result.FailedFiles == 0)
            {
                return CliExitCodes.Success;
            }

            if (result.SucceededFiles > 0)
            {
                return CliExitCodes.PartialBatchFailure;
            }

            return CliExitCodes.ConversionFailure;
        }
        catch (OperationCanceledException)
        {
            ConsoleProgressBar.Finish();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n[CANCELLED] Batch operation cancelled by user.");
            Console.ResetColor();
            return CliExitCodes.CancelledByUser;
        }
        catch (Exception ex)
        {
            ConsoleProgressBar.Finish();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[ERROR] Batch conversion failed: {ex.Message}");
            Console.ResetColor();
            _logger.LogError(ex, "Batch execution failure");
            return CliExitCodes.ConversionFailure;
        }
    }

    private static void PrintBatchSummary(BatchJobResult result)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("=======================================================================");
        Console.WriteLine($"  Batch Execution Summary: {result.JobName}");
        Console.WriteLine("=======================================================================");
        Console.ResetColor();

        Console.WriteLine($"  Job ID:       {result.JobId}");
        Console.WriteLine($"  Status:       {result.Status}");
        Console.WriteLine($"  Total Files:  {result.TotalFiles}");

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  Succeeded:    {result.SucceededFiles}");
        Console.ResetColor();

        if (result.FailedFiles > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  Failed:       {result.FailedFiles}");
            Console.ResetColor();
        }
        else
        {
            Console.WriteLine($"  Failed:       0");
        }

        if (result.SkippedFiles > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  Skipped:      {result.SkippedFiles}");
            Console.ResetColor();
        }

        Console.WriteLine($"  Total Time:   {result.DurationMs:N0} ms");
        if (result.TotalFiles > 0)
        {
            double avg = (double)result.DurationMs / result.TotalFiles;
            Console.WriteLine($"  Avg / File:   {avg:F1} ms");
        }

        if (result.FailedFiles > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine();
            Console.WriteLine("Failed Files Breakdown:");
            foreach (var item in result.Items.Where(i => i.Status == BatchItemStatus.Failed).Take(10))
            {
                Console.WriteLine($"  - {Path.GetFileName(item.SourceFilePath)}: [{item.ErrorCode}] {item.ErrorMessage}");
            }
            if (result.FailedFiles > 10)
            {
                Console.WriteLine($"    ... and {result.FailedFiles - 10} more failed files.");
            }
            Console.ResetColor();
        }

        Console.WriteLine();
    }
}
