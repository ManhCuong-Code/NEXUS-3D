namespace Nexus3D.CLI.Commands;

public static class HelpCommandHandler
{
    public static void PrintHelp()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("=======================================================================");
        Console.WriteLine("  NEXUS-3D CLI – Universal 3D Model & Schematic Converter");
        Console.WriteLine("=======================================================================");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  nexus3d <command> [options]");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Commands:");
        Console.ResetColor();
        Console.WriteLine("  convert          Convert a single 3D model or schematic file.");
        Console.WriteLine("  batch            Execute high-throughput batch conversion on a directory or file list.");
        Console.WriteLine("  jobs list        Display persistent history of batch conversion jobs.");
        Console.WriteLine("  jobs status <id> Inspect status and file progress of a specific batch job.");
        Console.WriteLine("  jobs cancel <id> Cancel a pending or running batch job.");
        Console.WriteLine("  plugins list     List installed and active plugins.");
        Console.WriteLine("  plugins info <id> Show detailed manifest and capabilities of a plugin.");
        Console.WriteLine("  plugins install <pkg> Install a .nexusplugin archive package.");
        Console.WriteLine("  plugins enable <id> Enable an installed plugin.");
        Console.WriteLine("  plugins disable <id> Disable an installed plugin.");
        Console.WriteLine("  plugins uninstall <id> Safely remove and uninstall a plugin.");
        Console.WriteLine("  formats list     Display all active 3D model formats (built-in & plugins).");
        Console.WriteLine("  --version, -v    Display application version and runtime metadata.");
        Console.WriteLine("  --help, -h       Show this detailed help manual.");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Convert Options:");
        Console.ResetColor();
        Console.WriteLine("  -i, --input <file>     Source 3D file (.obj, .schem, .voxel.json, .geo.json)");
        Console.WriteLine("  -o, --output <file>    Target destination file");
        Console.WriteLine("  --voxelize             Enable mesh voxelization preprocessing");
        Console.WriteLine("  --overwrite            Overwrite target file if it already exists");
        Console.WriteLine("  --scale <number>       Scale geometry (default: 1.0)");
        Console.WriteLine("  --no-greedy            Disable greedy meshing optimization for voxels");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Batch Options:");
        Console.ResetColor();
        Console.WriteLine("  -i, --input <dir>      Source directory containing 3D models");
        Console.WriteLine("  -o, --output <dir>     Output directory for converted models");
        Console.WriteLine("  -f, --format <fmt>     Target format: obj | schem | voxeljson | bedrock");
        Console.WriteLine("  -e, --extensions <ext> Comma-separated file filters (e.g. .obj,.schem)");
        Console.WriteLine("  -p, --parallel <n>     Max concurrent conversion tasks (default: 4)");
        Console.WriteLine("  --priority <level>     Scheduling priority: critical | high | normal | low");
        Console.WriteLine("  --voxelize             Enable mesh voxelization preprocessing");
        Console.WriteLine("  --overwrite            Overwrite existing destination files");
        Console.WriteLine("  --stop-on-error        Halt entire batch on first failed model (default: continue)");
        Console.WriteLine("  --retry <n>            Maximum retry attempts on transient failure (default: 1)");
        Console.WriteLine("  --json-report <file>   Export structured JSON execution summary");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Standard Exit Codes:");
        Console.ResetColor();
        Console.WriteLine("  0: Success");
        Console.WriteLine("  1: Unknown Error");
        Console.WriteLine("  2: Invalid Arguments");
        Console.WriteLine("  3: Invalid Input File");
        Console.WriteLine("  4: Conversion Failure");
        Console.WriteLine("  5: Partial Batch Failure (one or more files failed)");
        Console.WriteLine("  6: Operation Cancelled By User");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Examples:");
        Console.ResetColor();
        Console.WriteLine("  nexus3d convert -i model.obj -o model.schem --voxelize");
        Console.WriteLine("  nexus3d batch -i ./models -o ./exports -f schem --parallel 8 --priority high");
        Console.WriteLine("  nexus3d batch -i ./assets -o ./converted -f obj --extensions .schem,.json --json-report report.json");
        Console.WriteLine("  nexus3d jobs list --limit 10");
        Console.WriteLine("  nexus3d jobs status 3fa85f64-5717-4562-b3fc-2c963f66afa6");
        Console.WriteLine();
    }
}

