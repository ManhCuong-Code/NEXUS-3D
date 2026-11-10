# NEXUS-3D User Guide & Operating Manual

**Version:** 1.0.0  
**Target Platform:** Windows 10 / Windows 11 (64-bit)  
**Binary Executables:** `NEXUS3D.App.exe` (WPF Desktop Studio), `nexus3d.exe` (Headless CLI)

---

## 1. Introduction

NEXUS-3D is an all-in-one 3D model converter, voxelization engine, interactive editor, and batch processing system. It enables bi-directional workflow between traditional 3D graphics (Wavefront OBJ, MTL), Minecraft game assets (Sponge Schematics v1/v2/v3, Legacy MCEdit Schematics), Bedrock Geometry JSON, and Voxel interchange formats.

---

## 2. Installation & Quick Start

### 2.1. Windows Installer (`NEXUS-3D-Setup-v1.0.0-win-x64.exe`)
1. Download and run `NEXUS-3D-Setup-v1.0.0-win-x64.exe`.
2. Follow the setup wizard to choose installation directory (default: `C:\Program Files\NEXUS-3D`).
3. Select optional tasks:
   - Create Desktop Shortcut.
   - Associate `.schem`, `.schematic`, `.rawvox` file types.
   - Add `nexus3d.exe` to system `PATH` (enables CLI from any terminal).
4. Click **Install**. Launch **NEXUS-3D** from Start Menu or Desktop.

### 2.2. Portable Edition (`NEXUS-3D-Portable-v1.0.0-win-x64.zip`)
1. Extract the `.zip` archive to any directory (e.g. `D:\Tools\NEXUS-3D`).
2. Run `NEXUS3D.App.exe` for the Graphical Studio.
3. Run `nexus3d.exe` from PowerShell / Command Prompt for the Headless CLI.
4. All configurations and temporary caches remain inside the portable folder.

---

## 3. Desktop GUI Studio Overview

### 3.1. 3D Viewport Controls
- **Rotate / Orbit Camera:** Hold `Right Mouse Button` + Drag or `Alt + Left Mouse Button` + Drag.
- **Pan Camera:** Hold `Middle Mouse Button` (Scroll Wheel Click) + Drag or `Shift + Right Mouse Button`.
- **Zoom In / Out:** Rotate `Mouse Scroll Wheel` or `Alt + Right Mouse Button` + Drag.
- **Reset Camera:** Press `Home` or click **Fit to View** icon in the toolbar.
- **Toggle Wireframe:** Press `W` or toggle the wireframe icon.
- **Toggle Grid Floor / Bounds:** View menu -> Show Grid / Show Bounding Box.

### 3.2. Importing & Exporting Models
1. **Open / Import:** Click `File -> Open` (or `Ctrl + O`). Select `.obj`, `.schem`, `.schematic`, `.geo.json`, or `.json`.
2. **Export / Save As:** Click `File -> Export As` (or `Ctrl + E`).
3. **Choose Target Format:**
   - **Wavefront OBJ (`.obj`):** Exports polygonal mesh with accompanying `.mtl` material file and textures.
   - **Minecraft Sponge Schematic (`.schem`):** Exports voxel grid serialized to modern Sponge v2/v3 NBT format with block palettes.
   - **Bedrock Geometry (`.geo.json`):** Exports bone hierarchies and cube elements for Minecraft Bedrock resource packs.
   - **Voxel JSON (`.voxel.json`):** Exports human-readable voxel arrays or cuboid definitions.

### 3.3. Voxelization Engine
When converting a polygonal mesh (`.obj`) into a voxel/schematic target, the **Voxelization Dialog** automatically appears:
- **Resolution (Grid Size):** Choose from 16 to 512 (default: 64). Higher resolution creates finer detail with more blocks.
- **Voxelization Mode:**
  - *Conservative Surface (Hollow):* Voxelizes the polygonal shell only, saving blocks and file size.
  - *Solid Interior:* Floods the model interior using ray-casting and parity tests.
- **Palette Mapping:**
  - *Nearest RGB Color:* Maps vertex/texture UV colors to the closest Minecraft wool/terracotta/concrete block.
  - *Custom Block Palette:* Select specific Minecraft block sets (e.g. Stone/Wood, Quartz, Sci-Fi).

### 3.4. Interactive 3D Editor Tools
- **Transform Gizmos:** Select objects to activate Translate ($T$), Rotate ($R$), or Scale ($S$) gizmos with axis locking ($X, Y, Z$).
- **Voxel Sculpting:**
  - **Pencil ($B$):** Place individual voxels.
  - **Eraser ($E$):** Remove voxels.
  - **Brush ($Shift + B$):** Place voxels with configurable radius (1-5 blocks).
  - **Box Fill ($U$):** Click and drag to fill a 3D rectangular cuboid.
  - **Eyedropper ($I$):** Sample block type from any existing voxel in the scene.
- **History Stack:** Complete `Ctrl + Z` (Undo) and `Ctrl + Y` (Redo) support.

### 3.5. Plugin Manager & Extension Hub
1. Open `Tools -> Plugin Manager`.
2. View all installed extensions (built-in and third-party).
3. Click **Install Plugin...** and select any `.nexusplugin` file.
4. Enable or disable plugins on the fly without restarting the application.

---

## 4. Standalone Headless CLI (`nexus3d.exe`)

The standalone CLI is completely independent and ideal for CI/CD pipelines, automated build scripts, and bulk conversions.

### 4.1. Command Syntax
```powershell
nexus3d <command> [options]
```

### 4.2. Core Commands

#### 1. Single File Conversion (`convert`)
```powershell
# Convert OBJ mesh to Sponge Schematic with voxelization
nexus3d convert -i player_statue.obj -o player_statue.schem --voxelize

# Convert Schematic to OBJ mesh with greedy meshing enabled
nexus3d convert -i castle.schem -o castle.obj --overwrite

# Convert Bedrock Geometry JSON to OBJ mesh
nexus3d convert -i guardian.geo.json -o guardian.obj --scale 1.0
```

#### 2. High-Throughput Batch Processing (`batch`)
```powershell
# Convert all .obj models in a folder to .schem with 8 parallel worker threads
nexus3d batch -i ./models_input -o ./schem_output -f schem --parallel 8 --priority high

# Filter specific extensions and export a detailed JSON execution summary
nexus3d batch -i ./raw_assets -o ./converted -f obj -e .schem,.json --json-report results.json
```

#### 3. Batch Jobs Inspection (`jobs`)
```powershell
# List recent batch conversion jobs
nexus3d jobs list --limit 10

# Inspect progress and file status of a specific job
nexus3d jobs status 3fa85f64-5717-4562-b3fc-2c963f66afa6

# Cancel an active job
nexus3d jobs cancel 3fa85f64-5717-4562-b3fc-2c963f66afa6
```

#### 4. Supported Formats Inspection (`formats`)
```powershell
nexus3d formats list
```

#### 5. Plugin Management (`plugins`)
```powershell
# List active plugins
nexus3d plugins list

# Install a .nexusplugin package
nexus3d plugins install ./dist/plugins/com.nexus3d.formats.rawvox-1.0.0.nexusplugin

# View plugin details
nexus3d plugins info com.nexus3d.formats.rawvox

# Disable or enable a plugin
nexus3d plugins disable com.nexus3d.formats.rawvox
nexus3d plugins enable com.nexus3d.formats.rawvox

# Uninstall a plugin
nexus3d plugins uninstall com.nexus3d.formats.rawvox
```

---

## 5. Exit Codes & Automation Scripting

When invoking `nexus3d.exe` from scripts (PowerShell, Bash, Python, GitHub Actions), check `$LASTEXITCODE` / `$?`:

| Exit Code | Meaning | Description |
| :---: | :--- | :--- |
| `0` | **Success** | All operations completed successfully |
| `1` | **Unknown Error** | Unhandled internal exception |
| `2` | **Invalid Arguments** | Missing or malformed command-line flags |
| `3` | **Invalid Input File** | File does not exist, cannot be read, or unrecognized format |
| `4` | **Conversion Failure** | Processing failed during parsing, voxelization, or export |
| `5` | **Partial Batch Failure** | In batch mode, one or more files encountered errors |
| `6` | **Cancelled** | Operation was aborted by user or job cancellation request |

---

## 6. Frequently Asked Questions (FAQ)

**Q: Do I need to install .NET 10 to run NEXUS-3D?**  
A: No. Both the Desktop GUI installer and Portable packages are published as self-contained executables with the .NET 10 runtime embedded.

**Q: Can I convert very large models (over 1 million polygons)?**  
A: Yes. NEXUS-3D has been optimized with lazy-allocated vertex attributes, chunked $16^3$ voxel storage backends, and 64-bit address space, allowing smooth processing of large models within reasonable RAM limits.

**Q: How do I load my converted `.schem` file into Minecraft?**  
A: Use WorldEdit, FastAsyncWorldEdit (FAWE), or Axiom mod. Place the `.schem` file in your `plugins/WorldEdit/schematics` or `config/worldedit/schematics` folder, then run `//schem load <filename>` and `//paste`.
