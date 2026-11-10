using NEXUS3D.Formats.Abstractions;

namespace Nexus3D.CLI.Commands;

/// <summary>
/// CLI handler for listing all active model formats (built-in and plugin extensions).
/// </summary>
public sealed class FormatsCommandHandler
{
    private readonly IFormatRegistry _formatRegistry;

    public FormatsCommandHandler(IFormatRegistry formatRegistry)
    {
        _formatRegistry = formatRegistry;
    }

    public int Execute()
    {
        var formats = _formatRegistry.GetAllFormats();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("==========================================================================================");
        Console.WriteLine("                         NEXUS-3D SUPPORTED 3D & SCHEMATIC FORMATS                        ");
        Console.WriteLine("==========================================================================================");
        Console.ResetColor();

        Console.WriteLine("{0,-12} {1,-32} {2,-16} {3,-8} {4,-8} {5}", "SHORTCODE", "NAME", "PRIMARY EXT", "IMPORT", "EXPORT", "EXTENSIONS");
        Console.WriteLine(new string('-', 100));

        foreach (var f in formats)
        {
            string exts = string.Join(", ", f.SupportedExtensions);
            Console.WriteLine("{0,-12} {1,-32} {2,-16} {3,-8} {4,-8} {5}",
                f.ShortCode,
                Truncate(f.DisplayName, 32),
                f.PrimaryExtension,
                f.SupportsImport ? "YES" : "NO",
                f.SupportsExport ? "YES" : "NO",
                exts);
        }

        Console.WriteLine();
        return CliExitCodes.Success;
    }

    private static string Truncate(string str, int maxLength) =>
        str.Length <= maxLength ? str : str[..(maxLength - 3)] + "...";
}

