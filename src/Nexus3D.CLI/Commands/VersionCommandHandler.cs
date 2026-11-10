using System.Reflection;
using System.Runtime.InteropServices;

namespace Nexus3D.CLI.Commands;

public static class VersionCommandHandler
{
    public static void PrintVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        string version = asm.GetName().Version?.ToString() ?? "1.0.0";

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"NEXUS-3D CLI v{version}");
        Console.ResetColor();
        Console.WriteLine("Universal 3D & Schematic Model Conversion Engine");
        Console.WriteLine($"Runtime: .NET {Environment.Version} ({RuntimeInformation.FrameworkDescription})");
        Console.WriteLine($"OS: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        Console.WriteLine($"Intermediate Model: Universal Scene Model (USM) v2.0");
        Console.WriteLine("Supported Formats: Wavefront OBJ (.obj), Minecraft Schematic (.schem/.schematic),");
        Console.WriteLine("                   Voxel JSON (.voxel.json), Bedrock Geometry (.geo.json)");
    }
}

