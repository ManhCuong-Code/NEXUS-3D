# NEXUS-3D – Plugin SDK & Extension Hub Architecture Specification

## 1. Executive Summary

Phase 11 introduces a high-performance, modular, and dynamically extensible plugin architecture for **NEXUS-3D**. This architecture enables third-party developers and organizations to build, package, distribute, and load custom 3D model formats, parsers, exporters, detectors, and validators without modifying the core codebase.

The system is built on two primary pillars:
1. **`NEXUS3D.Plugin.SDK`**: A lightweight, standalone, headless library containing strictly core interfaces, manifest models, declarative attributes, base classes, and validation primitives. It contains **zero** UI or WPF dependencies, making it suitable for high-throughput headless workers, CLI pipelines, and cloud runners.
2. **`NEXUS3D.PluginSystem`**: The host runtime execution engine integrated into the NEXUS-3D Desktop App and CLI. It manages dynamic isolated assembly loading via collectible .NET `AssemblyLoadContext` (ALC), manifest schema validation, semantic version compatibility, topological dependency resolution, cryptographic package verification, Zip Slip attack defenses, and lifecycle synchronization with `IFormatRegistry`.

---

## 2. Architectural Blueprint

```
+-----------------------------------------------------------------------------------+
|                                  NEXUS-3D HOST                                    |
|                                                                                   |
|  +--------------------+   +-----------------------+   +------------------------+  |
|  |     WPF App        |   |   Headless CLI        |   | Universal Pipeline     |  |
|  |  (Extension Hub)   |   |   (nexus3d plugins)   |   | (Conversion & Batch)   |  |
|  +---------+----------+   +-----------+-----------+   +-----------+------------+  |
|            |                          |                           |               |
|            +--------------------------+---------------------------+               |
|                                       |                                           |
|                                       v                                           |
|                   +---------------------------------------+                       |
|                   |       NEXUS3D.PluginSystem            |                       |
|                   | - PluginManager                       |                       |
|                   | - PluginPackageManager (Zip Slip/Trav)|                       |
|                   | - PluginDependencyResolver (Kahn/Topol)                       |
|                   | - PluginApiVersionChecker (SemVer)    |                       |
|                   | - PluginConfigurationStore            |                       |
|                   | - Isolated PluginLoadContext (ALC)    |                       |
|                   +-------------------+-------------------+                       |
+---------------------------------------|-------------------------------------------+
                                        | implements & hosts
                                        v
+-----------------------------------------------------------------------------------+
|                              NEXUS3D.Plugin.SDK                                   |
| - INexusPlugin, IPluginContext, IPluginRegistrationContext                        |
| - PluginManifest, PluginDependencyInfo, PluginManifestValidator                   |
| - NexusPluginBase, [NexusPlugin], [SupportedFormat]                               |
| - IPluginConfiguration, IPluginLogger                                             |
+-----------------------------------------------------------------------------------+
                                        | references
                                        v
+-----------------------------------------------------------------------------------+
|                              THIRD-PARTY PLUGINS                                  |
|  e.g., NEXUS3D.Sample.Plugin (RawVox 3D Formats: .rawvox, .rvox)                  |
|  - Custom IFormatDescriptor, IFormatParser, IFormatExporter                       |
|  - Packaged into secure '.nexusplugin' archives with manifest.json                |
+-----------------------------------------------------------------------------------+
```

---

## 3. Core SDK Contracts & Contracts Layer

### 3.1. `INexusPlugin`
The fundamental entry point for all NEXUS-3D plugins:

```csharp
public interface INexusPlugin : IAsyncDisposable, IDisposable
{
    PluginManifest Manifest { get; }
    Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default);
    Task RegisterServicesAsync(IPluginRegistrationContext registrationContext, CancellationToken cancellationToken = default);
    Task ShutdownAsync(CancellationToken cancellationToken = default);
}
```

### 3.2. `IPluginContext`
Exposed to the plugin during initialization, providing isolated disk boundaries, logging, and configuration:
- `PluginDirectory`: Read-only path to the unpacked plugin assembly folder.
- `DataDirectory`: Isolated, per-plugin persistent storage directory (`%LOCALAPPDATA%\NEXUS3D\PluginsData\<PluginId>`).
- `Logger`: Isolated logger tagged with the plugin identifier.
- `Configuration`: Type-safe JSON configuration accessor.

### 3.3. `IPluginRegistrationContext`
Allows plugins to contribute capabilities back to the host runtime:
- `RegisterFormatDescriptor(IFormatDescriptor descriptor)`
- `RegisterParser(IFormatParser parser)`
- `RegisterExporter(IFormatExporter exporter)`
- `RegisterDetector(IFormatDetector detector)`
- `RegisterValidator(IFormatValidator validator)`

---

## 4. Manifest Schema (`manifest.json`)

Each plugin is defined by a root `manifest.json`:

```json
{
  "id": "com.nexus3d.formats.rawvox",
  "name": "RawVox 3D Format Extension",
  "version": "1.0.0",
  "author": "NEXUS-3D Core Team",
  "description": "Provides reading, writing, and conversion for RawVox 3D (.rawvox, .rvox) models.",
  "assembly": "NEXUS3D.Sample.Plugin.dll",
  "entryPoint": "NEXUS3D.Sample.Plugin.RawVoxPlugin",
  "pluginApiVersion": "1.0.0",
  "minimumHostVersion": "1.0.0",
  "supportedFormats": ["RAWVOX"],
  "capabilities": ["VoxelImport", "VoxelExport", "MeshExport"],
  "dependencies": []
}
```

### Strict Manifest Validation
`PluginManifestValidator` enforces:
1. **Reverse Domain ID**: Validated using regex `^[a-zA-Z0-9_\-\.]+$` with dot delimiters. Spaces and slashes are rejected.
2. **Semantic Versioning**: `Version`, `PluginApiVersion`, and `MinimumHostVersion` must strictly conform to Semantic Versioning 2.0.0.
3. **Path Traversal Defense**: The `assembly` property cannot contain `../`, `..\`, leading slashes, or absolute paths.

---

## 5. Isolated Runtime & AssemblyLoadContext (ALC)

### 5.1. `PluginLoadContext`
To enable true dynamic loading, independent dependency resolution, and unloading without memory leaks:
- Implements collectible `AssemblyLoadContext(isCollectible: true)`.
- **Contract Boundary Delegation**: For shared host assemblies (`NEXUS3D.Plugin.SDK`, `NEXUS3D.Domain`, `NEXUS3D.Formats.Abstractions`, `NEXUS3D.Shared`, and Microsoft.Extensions), `Load(AssemblyName)` returns `null`. This forces .NET to delegate the load to the default ALC, guaranteeing runtime type identity equality (`typeof(T) == typeof(T)`).
- **Third-Party Dependency Isolation**: Bundled DLL dependencies (e.g., custom parsers, external libraries) are loaded directly from the plugin directory into the isolated ALC.

### 5.2. Security Notice
> [!WARNING]
> While `AssemblyLoadContext` provides type and dependency isolation, it is **not** a secure sandbox boundary in modern .NET. Unsafe or malicious native code executed in a plugin can impact the host process. NEXUS-3D protects against archive-level exploits, malformed paths, and untrusted inputs, but advises administrators to review third-party plugin sources.

---

## 6. Security & Package Integrity

### 6.1. Archive Format (`.nexusplugin`)
A `.nexusplugin` file is a ZIP archive containing:
- `manifest.json` at root
- Primary plugin assembly (`<Name>.dll`)
- Associated dependencies and assets

### 6.2. Zip Slip & Path Traversal Protection
`PluginPackageManager.UnpackPlugin` explicitly verifies every entry path before extraction:
```csharp
string destinationPath = Path.GetFullPath(Path.Combine(fullTargetDir, entry.FullName));
if (!destinationPath.StartsWith(fullTargetDir, StringComparison.OrdinalIgnoreCase))
{
    throw new SecurityException($"Zip Slip attack detected in archive entry: '{entry.FullName}'");
}
```
Any attempt to write outside the plugin folder immediately aborts extraction, deletes intermediate artifacts, and logs a security violation.

### 6.3. Cryptographic Verification
When packages are created using `PluginPackageManager.PackPlugin`, a SHA-256 hash is computed. During automated installation, the hash is matched against database registrations to detect tampering.

---

## 7. Dependency Resolution Engine

`PluginDependencyResolver` processes all discovered plugin manifests before loading:
1. **Missing Dependency Detection**: Identifies required dependencies not present in the environment (optional dependencies are ignored if absent).
2. **Topological Sorting**: Uses Kahn's algorithm on directed acyclic dependency graphs (DAG) to establish exact load sequence.
3. **Cycle Diagnostics**: Employs Depth-First Search (DFS) graph coloring to detect and trace cyclic dependency loops (e.g., `A -> B -> C -> A`), returning clear human-readable error messages.

---

## 8. CLI & Headless Integration

NEXUS-3D CLI (`nexus3d.exe`) provides full plugin lifecycle administration:

| Command | Description |
|---|---|
| `nexus3d plugins list` | Displays table of all installed plugins, versions, status, and formats. |
| `nexus3d plugins info <pluginId>` | Displays detailed manifest, capabilities, and dependencies. |
| `nexus3d plugins install <packagePath>` | Unpacks, validates, and installs a `.nexusplugin` package. |
| `nexus3d plugins enable <pluginId>` | Enables a plugin and registers its format descriptors. |
| `nexus3d plugins disable <pluginId>` | Disables a plugin and deregisters its format descriptors. |
| `nexus3d plugins uninstall <pluginId>` | Shuts down, unloads, and removes plugin files from disk. |
| `nexus3d formats list` | Displays all currently available formats (built-in + active plugins). |

---

## 9. WPF Extension Hub User Interface

The desktop UI provides a clean master-detail management hub:
- **Telemetry Bar**: Displays real-time metrics (Total Extensions, Active & Loaded, Disabled, Faulted).
- **Master List**: Fast search filter by name, ID, author, or supported extension. Displays status badges (`ACTIVE`, `DISABLED`, `FAULTED`).
- **Detail Pane**: Shows full metadata, capabilities, dependencies, disk location, and inline error logs for faulted plugins.
- **Actions**:
  - `+ Install Package (.nexusplugin)...` (file picker)
  - `Enable / Disable` toggle
  - `Reload` plugin in collectible ALC
  - `Uninstall` plugin
  - `Open Plugins Folder` in File Explorer
