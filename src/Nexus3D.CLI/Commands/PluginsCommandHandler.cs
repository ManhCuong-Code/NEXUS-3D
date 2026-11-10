using NEXUS3D.PluginSystem.Models;
using NEXUS3D.PluginSystem.Services;
using Nexus3D.CLI.Arguments;

namespace Nexus3D.CLI.Commands;

/// <summary>
/// CLI handler for managing, listing, and installing plugins.
/// </summary>
public sealed class PluginsCommandHandler
{
    private readonly IPluginManager _pluginManager;

    public PluginsCommandHandler(IPluginManager pluginManager)
    {
        _pluginManager = pluginManager;
    }

    public async Task<int> ExecuteAsync(ParsedArguments args, CancellationToken cancellationToken)
    {
        return args.Command switch
        {
            CliCommandType.PluginsList => HandleList(),
            CliCommandType.PluginsInfo => HandleInfo(args.PluginId),
            CliCommandType.PluginsInstall => await HandleInstallAsync(args.PackagePath, cancellationToken).ConfigureAwait(false),
            CliCommandType.PluginsEnable => await HandleEnableAsync(args.PluginId, cancellationToken).ConfigureAwait(false),
            CliCommandType.PluginsDisable => await HandleDisableAsync(args.PluginId, cancellationToken).ConfigureAwait(false),
            CliCommandType.PluginsUninstall => await HandleUninstallAsync(args.PluginId, cancellationToken).ConfigureAwait(false),
            _ => CliExitCodes.InvalidArguments
        };
    }

    private int HandleList()
    {
        var plugins = _pluginManager.GetAllPlugins();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("==========================================================================================");
        Console.WriteLine("                                   NEXUS-3D INSTALLED PLUGINS                             ");
        Console.WriteLine("==========================================================================================");
        Console.ResetColor();

        if (plugins.Count == 0)
        {
            Console.WriteLine("No plugins installed or discovered. Use 'nexus3d plugins install <file.nexusplugin>' to install.");
            return CliExitCodes.Success;
        }

        Console.WriteLine("{0,-32} {1,-26} {2,-10} {3,-12} {4}", "PLUGIN ID", "NAME", "VERSION", "STATUS", "FORMATS");
        Console.WriteLine(new string('-', 100));

        foreach (var p in plugins)
        {
            ConsoleColor statusColor = p.Status switch
            {
                PluginLifecycleStatus.Enabled => ConsoleColor.Green,
                PluginLifecycleStatus.Disabled => ConsoleColor.DarkGray,
                PluginLifecycleStatus.Faulted => ConsoleColor.Red,
                _ => ConsoleColor.Yellow
            };

            string formats = string.Join(", ", p.Manifest.SupportedFormats);
            if (string.IsNullOrEmpty(formats)) formats = "(none)";

            Console.Write("{0,-32} {1,-26} {2,-10} ", Truncate(p.Manifest.Id, 32), Truncate(p.Manifest.Name, 26), p.Manifest.Version);
            Console.ForegroundColor = statusColor;
            Console.Write("{0,-12} ", p.Status.ToString().ToUpperInvariant());
            Console.ResetColor();
            Console.WriteLine(formats);
        }

        Console.WriteLine();
        return CliExitCodes.Success;
    }

    private int HandleInfo(string? pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: Plugin ID is required. Usage: nexus3d plugins info <plugin-id>");
            Console.ResetColor();
            return CliExitCodes.InvalidArguments;
        }

        var plugin = _pluginManager.GetPlugin(pluginId);
        if (plugin is null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: Plugin '{pluginId}' is not installed.");
            Console.ResetColor();
            return CliExitCodes.InvalidInputFile;
        }

        var m = plugin.Manifest;

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("==========================================================================================");
        Console.WriteLine($" PLUGIN DETAILS: {m.Name}");
        Console.WriteLine("==========================================================================================");
        Console.ResetColor();

        Console.WriteLine($"ID:                 {m.Id}");
        Console.WriteLine($"Version:            {m.Version}");
        Console.WriteLine($"Author:             {m.Author}");
        Console.WriteLine($"Description:        {m.Description}");
        Console.WriteLine($"Status:             {plugin.Status}");
        Console.WriteLine($"API Version:        {m.PluginApiVersion}");
        Console.WriteLine($"Min Host Version:   {m.MinimumHostVersion}");
        Console.WriteLine($"Assembly:           {m.Assembly}");
        Console.WriteLine($"Entry Point:        {m.EntryPoint}");
        Console.WriteLine($"Directory:          {plugin.PluginDirectory}");
        Console.WriteLine($"Supported Formats:  {string.Join(", ", m.SupportedFormats)}");
        Console.WriteLine($"Capabilities:       {string.Join(", ", m.Capabilities)}");
        Console.WriteLine($"License:            {m.License ?? "N/A"}");

        if (m.Dependencies is { Count: > 0 })
        {
            Console.WriteLine("Dependencies:");
            foreach (var dep in m.Dependencies)
            {
                Console.WriteLine($"  - {dep.PluginId} (>= v{dep.MinimumVersion}) {(dep.IsOptional ? "[Optional]" : "[Required]")}");
            }
        }
        else
        {
            Console.WriteLine("Dependencies:       (none)");
        }

        if (plugin.Status == PluginLifecycleStatus.Faulted && !string.IsNullOrEmpty(plugin.FaultMessage))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Fault Error:        {plugin.FaultMessage}");
            Console.ResetColor();
        }

        Console.WriteLine();
        return CliExitCodes.Success;
    }

    private async Task<int> HandleInstallAsync(string? packagePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: Package file path is required. Usage: nexus3d plugins install <path.nexusplugin>");
            Console.ResetColor();
            return CliExitCodes.InvalidArguments;
        }

        if (!File.Exists(packagePath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: Package file '{packagePath}' does not exist.");
            Console.ResetColor();
            return CliExitCodes.InvalidInputFile;
        }

        Console.WriteLine($"Installing plugin package from '{packagePath}'...");
        var result = await _pluginManager.InstallPluginAsync(packagePath, cancellationToken).ConfigureAwait(false);

        if (result.IsFailure || result.Value is null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Failed to install plugin: [{result.ErrorCode}] {result.ErrorMessage}");
            Console.ResetColor();
            return CliExitCodes.UnknownError;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Successfully installed and activated plugin '{result.Value.Manifest.Name}' (v{result.Value.Manifest.Version}) [{result.Value.Manifest.Id}]!");
        Console.ResetColor();
        return CliExitCodes.Success;
    }

    private async Task<int> HandleEnableAsync(string? pluginId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: Plugin ID is required. Usage: nexus3d plugins enable <plugin-id>");
            Console.ResetColor();
            return CliExitCodes.InvalidArguments;
        }

        var result = await _pluginManager.EnablePluginAsync(pluginId, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Failed to enable plugin: [{result.ErrorCode}] {result.ErrorMessage}");
            Console.ResetColor();
            return CliExitCodes.UnknownError;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Plugin '{pluginId}' is now ENABLED.");
        Console.ResetColor();
        return CliExitCodes.Success;
    }

    private async Task<int> HandleDisableAsync(string? pluginId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: Plugin ID is required. Usage: nexus3d plugins disable <plugin-id>");
            Console.ResetColor();
            return CliExitCodes.InvalidArguments;
        }

        var result = await _pluginManager.DisablePluginAsync(pluginId, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Failed to disable plugin: [{result.ErrorCode}] {result.ErrorMessage}");
            Console.ResetColor();
            return CliExitCodes.UnknownError;
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Plugin '{pluginId}' is now DISABLED.");
        Console.ResetColor();
        return CliExitCodes.Success;
    }

    private async Task<int> HandleUninstallAsync(string? pluginId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: Plugin ID is required. Usage: nexus3d plugins uninstall <plugin-id>");
            Console.ResetColor();
            return CliExitCodes.InvalidArguments;
        }

        var result = await _pluginManager.UninstallPluginAsync(pluginId, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Failed to uninstall plugin: [{result.ErrorCode}] {result.ErrorMessage}");
            Console.ResetColor();
            return CliExitCodes.UnknownError;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Plugin '{pluginId}' has been completely uninstalled.");
        Console.ResetColor();
        return CliExitCodes.Success;
    }

    private static string Truncate(string str, int maxLength) =>
        str.Length <= maxLength ? str : str[..(maxLength - 3)] + "...";
}
