using Microsoft.Extensions.DependencyInjection;
using Nexus3D.CLI.Arguments;
using Nexus3D.CLI.Commands;

namespace Nexus3D.CLI;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var parsed = CliArgumentParser.Parse(args);

        if (!string.IsNullOrEmpty(parsed.ParseError))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {parsed.ParseError}");
            Console.ResetColor();
            Console.WriteLine("Run 'nexus3d --help' for usage instructions.");
            return CliExitCodes.InvalidArguments;
        }

        if (parsed.Command == CliCommandType.Help)
        {
            HelpCommandHandler.PrintHelp();
            return CliExitCodes.Success;
        }

        if (parsed.Command == CliCommandType.Version)
        {
            VersionCommandHandler.PrintVersion();
            return CliExitCodes.Success;
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cts.Cancel();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n[INTERRUPT] Received Ctrl+C cancellation request...");
            Console.ResetColor();
        };

        try
        {
            var provider = CliBootstrapper.BuildServiceProvider();

            return parsed.Command switch
            {
                CliCommandType.Convert => await provider.GetRequiredService<ConvertCommandHandler>().ExecuteAsync(parsed, cts.Token).ConfigureAwait(false),
                CliCommandType.Batch => await provider.GetRequiredService<BatchCommandHandler>().ExecuteAsync(parsed, cts.Token).ConfigureAwait(false),
                CliCommandType.JobsList or CliCommandType.JobsStatus or CliCommandType.JobsCancel =>
                    await provider.GetRequiredService<JobsCommandHandler>().ExecuteAsync(parsed, cts.Token).ConfigureAwait(false),
                _ => CliExitCodes.InvalidArguments
            };
        }
        catch (OperationCanceledException)
        {
            return CliExitCodes.CancelledByUser;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Fatal Error: {ex.Message}");
            Console.ResetColor();
            return CliExitCodes.UnknownError;
        }
    }
}

