# NEXUS-3D v1.0.0 Release Notes

**Release Date:** November 2026  
**Platform:** Windows Desktop (x64) – .NET 10 LTS  
**License:** MIT License  
**Repository:** [https://github.com/ManhCuong-Code/NEXUS-3D](https://github.com/ManhCuong-Code/NEXUS-3D)

---

## 🌟 Overview

**NEXUS-3D** is an enterprise-grade Universal 3D Model & Minecraft Schematic Converter, Editor, and Processing Engine. Built from the ground up on C# 14 and .NET 10 LTS, NEXUS-3D provides bi-directional conversion, high-performance voxelization, an interactive 3D viewport, batch automation, headless CLI execution, and an extensible Plugin SDK architecture.

At its core lies the **Universal Scene Model (USM)**—a unified intermediate representation that decouples source formats from target formats, eliminating $O(N \times M)$ converter proliferation into an extensible $O(N + M)$ pipeline.

---

## 🚀 Key Features by Engine & Phase

### 1. Universal Scene Model (USM) – Phase 01 & 02
- Decoupled intermediate representation unifying polygonal meshes, voxel grids, materials, textures, animations, and metadata.
- Clean Architecture with zero external format leakage in Domain layers.
- Thread-safe scene graph with node hierarchies, transform hierarchies, and coordinate system normalization.

### 2. Schematic Engine – Phase 03
- **Sponge v1, v2, v3** `.schem` specification support with palette compression and block entity parsing.
- **Legacy MCEdit** `.schematic` backward compatibility with block ID/data value mapping.
- Native NBT Reader/Writer supporting gzip and zlib compression streams.
- Accurate spatial transformations, volume calculations, and palette deduplication.

### 3. Wavefront OBJ Engine – Phase 04
- High-throughput OBJ & MTL parser with support for polygonal meshes, quads, n-gons (ear-clipping triangulation).
- Smoothing groups, vertex normals, texture coordinates (UVs), and multi-material groups.
- Complete MTL material mapping (ambient, diffuse, specular, shininess, roughness, normal/bump maps).

### 4. JSON Geometry & Voxel Engine – Phase 05
- **Minecraft Bedrock Geometry JSON** (`format_version` 1.12.0 / 1.16.0) reader and writer with hierarchical bones, pivots, cube elements, and UV mapping.
- **Voxel JSON** interchange format support for dense and sparse voxel volumes.
- Strict schema validation with descriptive diagnostics and path-traced error reporting.

### 5. Multi-Format Universal Conversion Engine – Phase 06
- Dynamic format resolution matrix enabling seamless conversions:
  - `OBJ -> Schematic` (via Voxelization)
  - `Schematic -> OBJ` (Voxel meshing / face culling)
  - `Bedrock JSON <-> OBJ`
  - `Bedrock JSON <-> Schematic`
  - `Voxel JSON <-> OBJ / Schematic`
- Configurable material palettes, block state mappings, coordinate system conversions (Y-up vs Z-up, left-handed vs right-handed).

### 6. Universal 3D Viewport & Rendering Engine – Phase 07
- High-performance WPF 3D Viewport powered by DirectX / SharpDX HelixToolkit.
- Real-time rendering of complex meshes (1,000,000+ vertices) and voxel grids.
- PBR lighting, directional shadows, grid floor, orientation gizmo, wireframe toggle, and multi-camera modes (Perspective, Orthographic, Orbit, First-Person Walkthrough).

### 7. Conservative Voxelization Engine – Phase 08
- High-precision SAT (Separating Axis Theorem) triangle-box intersection algorithm.
- Multi-resolution voxelization (from $16^3$ up to $512^3$ grids).
- Hollow voxelization (shell only) vs Solid voxelization (ray-casting / flood-fill).
- Automatic color/texture sampling and nearest-neighbor Minecraft block palette matching.

### 8. Interactive 3D Editor Tools – Phase 09
- Direct manipulation Gizmos: Translate, Rotate, Scale with snapping options.
- Voxel editing tools: Pencil, Brush, Eraser, Fill Box, Eyedropper, Volume Selection.
- Mesh editing tools: Subdivide, Smooth, Decimate, Invert Normals, Recalculate UVs.
- Robust Undo/Redo command history stack (`ICommand` pattern).

### 9. High-Performance Batch Processing & Standalone CLI – Phase 10
- Multithreaded concurrent batch conversion queue with real-time progress reporting, pause/resume, and cancellation.
- Standalone Headless CLI executable (`nexus3d.exe`):
  - `nexus3d convert -i model.obj -o output.schem --resolution 128`
  - `nexus3d batch -d ./input_models -o ./converted --target schem`
  - `nexus3d formats list`
  - `nexus3d inspect model.geo.json`
- Scriptable automation and CI/CD ready with exit codes and JSON log output.

### 10. Plugin SDK & Extension Hub – Phase 11
- Dynamic Plugin System with isolated `AssemblyLoadContext` runtime loader.
- Third-party SDK package: `NEXUS3D.Plugin.SDK` on NuGet.
- Hot-loading and unloading of external format importers, exporters, and voxel processors.
- Bundled sample reference plugin: RawVox 3D format (`.rawvox`).
- Cryptographic hash verification, manifest validation, and capability inspection.

### 11. Packaging, Profiling & Release – Phase 12
- Memory optimization: Lazy attribute instantiation saves >120 MB on 1M vertices.
- Spatial 3D locality: Chunked $16 \times 16 \times 16$ voxel storage backend.
- Bounded concurrency worker pool preventing memory spikes in mass batch conversions.
- Self-contained Windows installer & Zero-dependency Portable packages.

---

## 📦 Release Artifacts

| Artifact | Type | Description |
| :--- | :--- | :--- |
| `NEXUS-3D-Setup-v1.0.0-win-x64.exe` | Windows Installer | Inno Setup installer with desktop shortcut, context menus, and file associations |
| `NEXUS-3D-Portable-v1.0.0-win-x64.zip` | Portable Package | Zero-install self-contained GUI Studio & CLI bundle |
| `nexus3d-cli-v1.0.0-win-x64.zip` | Standalone CLI | Lightweight standalone headless CLI for servers and CI/CD pipelines |
| `NEXUS3D.Plugin.SDK.1.0.0.nupkg` | NuGet Package | Official SDK for building third-party formats and extensions |
| `com.nexus3d.formats.rawvox-1.0.0.nexusplugin` | Sample Plugin | Reference custom format package for NEXUS-3D Plugin Hub |
| `SHA256SUMS.txt` | Checksums | Cryptographic SHA-256 verification hashes for all distribution files |

---

## 💻 System Requirements

- **Operating System:** Windows 10 (Version 1809+) or Windows 11 (64-bit).
- **Architecture:** x64 (AMD64 / Intel 64).
- **Runtime:** .NET 10 LTS runtime is embedded in self-contained builds (no separate .NET installation required).
- **Hardware:**
  - Minimum: Intel Core i3 / AMD Ryzen 3, 4 GB RAM, DirectX 11 compatible GPU.
  - Recommended: Intel Core i7 / AMD Ryzen 7, 16 GB RAM, dedicated NVIDIA / AMD GPU with 4+ GB VRAM.

---

## 🔒 Security & Quality Assurance

- **Vulnerabilities:** 0 vulnerabilities across all solution dependencies (`dotnet list package --vulnerable`).
- **Zip Slip & Path Traversal:** Strict path canonicalization guards in plugin installation and archive extraction.
- **Code Coverage & Unit Tests:** Over 360 unit and integration tests passing with 100% success rate.
