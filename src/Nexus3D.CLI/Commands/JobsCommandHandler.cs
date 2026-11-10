using NEXUS3D.Conversion.Abstractions.Interfaces;
using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Domain.Enums;
using Nexus3D.CLI.Arguments;

namespace Nexus3D.CLI.Commands;

public sealed class JobsCommandHandler
{
    private readonly IBatchProcessingService _batchService;

    public JobsCommandHandler(IBatchProcessingService batchService)
    {
        _batchService = batchService;
    }

    public async Task<int> ExecuteAsync(ParsedArguments args, CancellationToken cancellationToken)
    {
        return args.Command switch
        {
            CliCommandType.JobsList => await ListJobsAsync(args.Limit, cancellationToken).ConfigureAwait(false),
            CliCommandType.JobsStatus => await ShowJobStatusAsync(args.JobId, cancellationToken).ConfigureAwait(false),
            CliCommandType.JobsCancel => await CancelJobAsync(args.JobId, cancellationToken).ConfigureAwait(false),
            _ => CliExitCodes.InvalidArguments
        };
    }

    private async Task<int> ListJobsAsync(int limit, CancellationToken cancellationToken)
    {
        var jobs = await _batchService.GetJobsAsync(cancellationToken).ConfigureAwait(false);
        var displayed = jobs.Take(limit).ToList();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Persistent Batch Jobs History (Showing {displayed.Count} of {jobs.Count}):");
        Console.ResetColor();
        Console.WriteLine(new string('-', 100));
        Console.WriteLine($"{"Job ID",-38} {"Status",-18} {"Pri",-8} {"Files",-8} {"Time(ms)",-10} {"Name",-20}");
        Console.WriteLine(new string('-', 100));

        foreach (var job in displayed)
        {
            ConsoleColor color = job.Status switch
            {
                BatchJobStatus.Completed => ConsoleColor.Green,
                BatchJobStatus.CompletedWithErrors => ConsoleColor.Yellow,
                BatchJobStatus.Failed => ConsoleColor.Red,
                BatchJobStatus.Running => ConsoleColor.Cyan,
                BatchJobStatus.Cancelled => ConsoleColor.DarkGray,
                _ => ConsoleColor.White
            };

            Console.ForegroundColor = color;
            Console.WriteLine($"{job.JobId,-38} {job.Status,-18} {job.Priority,-8} {job.CompletedFiles}/{job.TotalFiles,-6} {job.TotalDurationMs,-10} {job.JobName,-20}");
            Console.ResetColor();
        }

        Console.WriteLine(new string('-', 100));
        return CliExitCodes.Success;
    }

    private async Task<int> ShowJobStatusAsync(Guid? jobId, CancellationToken cancellationToken)
    {
        if (!jobId.HasValue)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: Job ID is required for 'jobs status <job-id>'.");
            Console.ResetColor();
            return CliExitCodes.InvalidArguments;
        }

        var job = await _batchService.GetJobAsync(jobId.Value, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: Batch job '{jobId.Value}' was not found.");
            Console.ResetColor();
            return CliExitCodes.InvalidArguments;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("=======================================================================");
        Console.WriteLine($"  Batch Job Status: {job.JobName}");
        Console.WriteLine("=======================================================================");
        Console.ResetColor();

        Console.WriteLine($"  Job ID:           {job.JobId}");
        Console.WriteLine($"  Status:           {job.Status}");
        Console.WriteLine($"  Priority:         {job.Priority}");
        Console.WriteLine($"  Target Format:    {job.TargetFormat}");
        Console.WriteLine($"  Output Directory: {job.OutputDirectory}");
        Console.WriteLine($"  Progress:         {job.PercentComplete:F1}% ({job.CompletedFiles}/{job.TotalFiles})");
        Console.WriteLine($"  Succeeded:        {job.SucceededFiles}");
        Console.WriteLine($"  Failed:           {job.FailedFiles}");
        Console.WriteLine($"  Skipped:          {job.SkippedFiles}");
        Console.WriteLine($"  Duration:         {job.TotalDurationMs:N0} ms");
        Console.WriteLine($"  Created At:       {job.CreatedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
        if (job.CompletedAtUtc.HasValue)
        {
            Console.WriteLine($"  Completed At:     {job.CompletedAtUtc.Value:yyyy-MM-dd HH:mm:ss} UTC");
        }

        if (job.Items.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Items:");
            foreach (var item in job.Items.Take(25))
            {
                ConsoleColor itemColor = item.Status switch
                {
                    BatchItemStatus.Succeeded => ConsoleColor.Green,
                    BatchItemStatus.Failed => ConsoleColor.Red,
                    BatchItemStatus.Skipped => ConsoleColor.Yellow,
                    _ => ConsoleColor.White
                };

                Console.ForegroundColor = itemColor;
                Console.WriteLine($"  [{item.Status,-9}] {Path.GetFileName(item.SourceFilePath)} -> {Path.GetFileName(item.TargetFilePath)} ({item.DurationMs}ms)");
                if (!string.IsNullOrEmpty(item.ErrorMessage))
                {
                    Console.WriteLine($"              Error: {item.ErrorMessage}");
                }
                Console.ResetColor();
            }

            if (job.Items.Count > 25)
            {
                Console.WriteLine($"  ... and {job.Items.Count - 25} more items.");
            }
        }

        Console.WriteLine();
        return CliExitCodes.Success;
    }

    private async Task<int> CancelJobAsync(Guid? jobId, CancellationToken cancellationToken)
    {
        if (!jobId.HasValue)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: Job ID is required for 'jobs cancel <job-id>'.");
            Console.ResetColor();
            return CliExitCodes.InvalidArguments;
        }

        await _batchService.CancelJobAsync(jobId.Value, cancellationToken).ConfigureAwait(false);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Cancellation signal sent to batch job '{jobId.Value}'.");
        Console.ResetColor();
        return CliExitCodes.Success;
    }
}
