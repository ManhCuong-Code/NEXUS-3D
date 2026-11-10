# NEXUS-3D Developer & Extension Guide

**Version:** 1.0.0  
**Framework:** .NET 10 LTS / C# 14  
**Architecture:** Clean Architecture + Modular Architecture + Plugin Architecture  
**Repository:** [https://github.com/ManhCuong-Code/NEXUS-3D](https://github.com/ManhCuong-Code/NEXUS-3D)

---

## 1. Architectural Philosophy

NEXUS-3D strictly adheres to **Clean Architecture** and **Domain-Driven Design (DDD)** principles.

```
                      +-----------------------------+
                      |       UI & Presentation     |
                      |  (NEXUS3D.App, Nexus3D.CLI) |
                      +--------------+--------------+
                                     |
                                     v
                      +-----------------------------+
                      |      Application Layer      |
                      | (Services, Commands, Batch) |
                      +--------------+--------------+
                                     |
                                     v
                      +-----------------------------+
                      |     Domain Layer (USM)      |
                      | (Meshes, Voxels, Materials) |
                      +--------------+--------------+
                                     ^
                                     |
                      +--------------+--------------+
                      |     Infrastructure Layer    |
                      | (Storage, SQLite, Plugins)  |
                      +-----------------------------+
```

### The Universal Scene Model (USM)
Rather than writing direct converters between every pair of formats ($N \times M$), all formats map to and from the **Universal Scene Model (`USM`)**.
- Adding format $K$ requires only 1 Importer (`Format -> USM`) and 1 Exporter (`USM -> Format`).
- All existing conversions (`$K \leftrightarrow \text{All}$`) become immediately available at zero extra cost.

---

## 2. Solution Structure

```
NEXUS-3D/
├── src/
│   ├── NEXUS3D.Shared/                  # Core primitives (Vector3D, ColorRgba, Matrix4x4)
│   ├── NEXUS3D.Domain/                  # USM domain entities (Scene, Mesh, VoxelGrid, Material)
│   ├── NEXUS3D.Application/             # Application services, conversion coordinators
│   ├── NEXUS3D.Infrastructure/          # File system, configuration, SQLite persistence
│   ├── NEXUS3D.Formats.Abstractions/    # IModelImporter, IModelExporter contracts
│   ├── NEXUS3D.Formats.Schematic/       # Sponge v1/v2/v3 & Legacy MCEdit NBT engine
│   ├── NEXUS3D.Formats.Obj/             # Wavefront OBJ & MTL parser/exporter
│   ├── NEXUS3D.Formats.BedrockGeometry/ # Minecraft Bedrock Geometry JSON reader/writer
│   ├── NEXUS3D.Formats.VoxelJson/       # Voxel Cuboid JSON reader/writer
│   ├── NEXUS3D.Conversion.Engine/       # Multi-format conversion coordinator & pipeline
│   ├── NEXUS3D.Conversion.Batch/        # Concurrent worker pool & batch job manager
│   ├── NEXUS3D.Voxelization.Engine/     # SAT triangle-box conservative voxelizer
│   ├── NEXUS3D.Rendering/               # HelixToolkit WPF 3D viewport renderer
│   ├── NEXUS3D.Editor.Engine/           # Interactive 3D tools & Undo/Redo stack
│   ├── NEXUS3D.Plugin.SDK/              # Public developer SDK package
│   ├── NEXUS3D.PluginSystem/            # AssemblyLoadContext isolation runtime
│   ├── NEXUS3D.App/                     # WPF Desktop Presentation application
│   └── Nexus3D.CLI/                     # Standalone Headless CLI application
├── plugins/
│   └── NEXUS3D.Sample.Plugin/           # Reference implementation of a custom format plugin
├── installer/
│   └── nexus3d_setup.iss               # Inno Setup 6 deployment script
├── scripts/
│   └── package-release.ps1             # Automated packaging and artifact generator
└── tests/
    ├── NEXUS3D.Domain.Tests/
    ├── NEXUS3D.Formats.Schematic.Tests/
    ├── NEXUS3D.Formats.Obj.Tests/
    ├── NEXUS3D.Formats.Json.Tests/
    ├── NEXUS3D.Conversion.Tests/
    ├── NEXUS3D.Voxelization.Tests/
    ├── NEXUS3D.PluginSystem.Tests/
    └── NEXUS3D.Performance.Tests/
```

---

## 3. Developing Plugins with `NEXUS3D.Plugin.SDK`

Third-party developers can create custom format importers and exporters without touching the core codebase.

### 3.1. Reference the SDK
In your project `.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="NEXUS3D.Plugin.SDK" Version="1.0.0" />
  </ItemGroup>
</Project>
```

### 3.2. Implement the Importer
```csharp
using NEXUS3D.Domain.Scenes;
using NEXUS3D.Formats.Abstractions;

public class MyCustomImporter : IModelImporter
{
    public string FormatName => "My 3D Format";
    public string DefaultExtension => ".my3d";
    public IReadOnlyList<string> SupportedExtensions => new[] { ".my3d" };

    public async Task<Scene> ImportAsync(Stream stream, ImportOptions options, CancellationToken cancellationToken = default)
    {
        var scene = new Scene("Imported Model");
        // Read stream and construct USM meshes or voxels...
        return scene;
    }
}
```

### 3.3. Implement the Exporter
```csharp
using NEXUS3D.Domain.Scenes;
using NEXUS3D.Formats.Abstractions;

public class MyCustomExporter : IModelExporter
{
    public string FormatName => "My 3D Format";
    public string DefaultExtension => ".my3d";
    public IReadOnlyList<string> SupportedExtensions => new[] { ".my3d" };

    public async Task ExportAsync(Scene scene, Stream outputStream, ExportOptions options, CancellationToken cancellationToken = default)
    {
        // Write USM meshes or voxels to outputStream...
    }
}
```

### 3.4. Provide Plugin Manifest (`manifest.json`)
```json
{
  "id": "com.developer.formats.my3d",
  "name": "My Custom 3D Format Plugin",
  "version": "1.0.0",
  "author": "Third Party Developer",
  "description": "Imports and exports proprietary 3D format for NEXUS-3D",
  "entryAssembly": "MyCustomPlugin.dll",
  "supportedFormats": [".my3d"]
}
```

### 3.5. Package as `.nexusplugin`
A `.nexusplugin` is a standard ZIP archive containing:
1. `manifest.json` at root.
2. Compiled plugin assemblies (`MyCustomPlugin.dll`, dependencies).
3. Any bundled assets or documentation.

Compress the directory into a `.zip` file and change the extension to `.nexusplugin`.

---

## 4. Building from Source

### Prerequisites
- Windows 10/11 x64
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Visual Studio 2026 / Visual Studio Code with C# Dev Kit

### Build & Test Commands
```powershell
# Restore dependencies
dotnet restore NEXUS-3D.sln

# Build entire solution in Release mode
dotnet build NEXUS-3D.sln -c Release

# Run complete test suite (360+ tests)
dotnet test NEXUS-3D.sln -c Release --verbosity normal

# Run performance benchmark suite
dotnet test tests/NEXUS3D.Performance.Tests/NEXUS3D.Performance.Tests.csproj -c Release --logger "console;verbosity=detailed"
```

---

## 5. Security & Isolation

- **Isolated Assembly Loading:** Each plugin executes within its own `PluginAssemblyLoadContext` based on .NET Core's `AssemblyLoadContext`. This prevents DLL hell and assembly version collision.
- **Archive Extraction Guard:** All archive extractions validate paths against base destination directories using `Path.GetFullPath` to completely neutralize Zip Slip vulnerabilities.
