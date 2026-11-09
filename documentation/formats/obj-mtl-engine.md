# Wavefront OBJ & MTL Engine (`NEXUS3D.Formats.Obj`)

## 1. Overview

The **Stage 04 Wavefront OBJ & MTL Engine** (`src/NEXUS3D.Formats.Obj/`) provides high-precision parsing, validation, Universal Scene Model (USM) conversion, texture path resolution, and atomic multi-file exporting for Wavefront `.obj` and `.mtl` assets.

---

## 2. Module Structure

```text
src/NEXUS3D.Formats.Obj/
├── Common/
│   ├── ObjDocument.cs               # Intermediate OBJ document model
│   └── ObjException.cs              # Standardized OBJ001..OBJ017 error codes & exception
├── Parsing/
│   ├── ObjLexer.cs                  # Streaming line/continuation tokenizer
│   ├── ObjLineParser.cs             # Statement parser & positive/negative index resolver
│   ├── ObjParser.cs                 # Document parser & optional normal recalculation
│   ├── ObjParseOptions.cs           # Safety limits & parsing flags
│   └── ObjParseResult.cs            # Parse result envelope
├── Geometry/
│   ├── ObjVertex.cs                 # v x y z [w] (double precision)
│   ├── ObjTextureCoordinate.cs      # vt u [v] [w] (1D/2D/3D UVW)
│   ├── ObjNormal.cs                 # vn x y z
│   ├── ObjFaceVertex.cs             # Independent 0-based (Position, UV, Normal) corner
│   ├── ObjFace.cs                   # Triangle/Quad/N-gon + 2D Ear-Clipping Triangulation
│   ├── ObjPolylineAndPoint.cs       # vp, l (polyline), and p (point) elements
│   ├── ObjObject.cs                 # o <name> partition
│   ├── ObjGroup.cs                  # g <group1> <group2> ... partition
│   └── ObjSmoothingGroup.cs         # s <1..N | off | 0> partition
├── Materials/
│   ├── MtlDocument.cs               # Intermediate MTL library model & options
│   ├── MtlMaterial.cs               # newmtl properties & texture maps
│   ├── MtlProperty.cs               # Custom properties & MtlTextureMapStatement options
│   ├── MtlParser.cs                 # MTL parser & USM MaterialData mapper
│   └── MtlExporter.cs               # Standards-compliant MTL exporter
├── Textures/
│   ├── ObjTextureReference.cs       # Resolved texture reference & USM TextureData adapter
│   ├── TexturePathResolver.cs       # Cross-platform path resolver, header inspector & safe copier
│   └── TextureValidationService.cs  # Texture existence & format validator
├── Conversion/
│   ├── ObjMappingOptions.cs         # Bidirectional conversion options
│   ├── ObjConversionReport.cs       # Detailed conversion & fidelity metrics
│   ├── ObjDataPreserver.cs          # RawDataContainer preservation for vp/l/p/comments
│   ├── ObjToSceneConverter.cs       # OBJ + MTL -> USM Scene3D converter
│   └── SceneToObjConverter.cs       # USM Scene3D -> OBJ + MTL converter
├── Exporting/
│   ├── ObjExportOptions.cs          # Precision, triangulation, MTL & texture copy options
│   ├── ObjExportResult.cs           # Export metrics & output paths
│   └── ObjExporter.cs               # 1-based Wavefront OBJ writer
├── Validation/
│   ├── ObjGeometryValidator.cs      # Fineness, zero-length normal & degenerate polygon checks
│   ├── ObjReferenceValidator.cs     # Index bounds & usemtl reference checks
│   ├── MtlValidator.cs              # Material range & duplicate name checks
│   └── ObjValidator.cs              # Unified IFormatValidator implementation
└── Services/
    ├── ObjFormatDetector.cs         # Content & header detector (wavefront:obj)
    ├── ObjFormatParser.cs           # Unified IObjReader / IFormatParser service
    ├── ObjFormatExporter.cs         # Unified IObjWriter / IFormatExporter with atomic staging
    └── ObjServiceRegistration.cs    # Dependency Injection registration (AddObjFormatServices)
```

---

## 3. Supported Wavefront OBJ Statements & Indexing Rules

| Statement | Syntax | Support Level |
|---|---|---|
| `v` | `v x y z [w]` | Full 3D & 4D homogeneous coordinates (`double` precision) |
| `vt` | `vt u [v] [w]` | Full 1D, 2D, and 3D texture coordinates |
| `vn` | `vn i j k` | Full 3D vertex normals (preserves original normals; optional recalculation) |
| `vp` | `vp u [v] [w]` | Full parameter-space vertices (preserved in `ObjDataPreserver`) |
| `f` | `f v`, `f v/vt`, `f v//vn`, `f v/vt/vn` | Triangles, Quads, and arbitrary N-gons; 1-based positive & negative relative indices |
| `l` | `l v1[/vt1] v2[/vt2] ...` | Polylines (preserved in `ObjDataPreserver`) |
| `p` | `p v1 v2 ...` | Point elements (preserved in `ObjDataPreserver`) |
| `o` | `o object_name` | Mapped to `SceneNode` / `SceneObject` / `MeshData` |
| `g` | `g group_1 [group_2 ...]` | Mapped to `Face.GroupNames` and `MeshSubmesh` |
| `s` | `s <1..N \| off \| 0>` | Mapped to `Face.SmoothingGroup` |
| `mtllib` | `mtllib file1.mtl ...` | Automatically resolved relative to `.obj` directory |
| `usemtl` | `usemtl material_name` | Linked to `MaterialData.Id` on each `Face` and `MeshSubmesh` |
| `#` / `\` | Comments & line continuations | Preserved in header/material comments; logical lines joined before parsing |

### Index Conversion Formula
- **Positive index (`idx > 0`)**: `zeroBased = idx - 1`
- **Negative index (`idx < 0`)**: `zeroBased = currentElementCount + idx`
- **Zero index (`idx == 0`)**: Rejected with `OBJ007` (`IndexOutOfRange`).

---

## 4. UV Seam & Hard Normal Edge Preservation in USM

In Wavefront OBJ, a single 3D vertex position `v` can be referenced by multiple face corners with different `vt` (UV seam) or `vn` (hard normal crease) indices.

When `ObjToSceneConverter` maps an `ObjDocument` to USM `MeshData`:
1. Each unique `(PositionIndex, TextureCoordIndex, NormalIndex)` tuple within an object creates a distinct USM `Vertex` in `MeshData.Vertices`.
2. Each USM `Face` stores both the expanded `VertexIndices` and the original `FaceCorner(PositionIndex, TexCoordIndex, NormalIndex)` list in `Face.Corners`.
3. When `SceneToObjConverter` exports back to `.obj`, it reconstructs the shared `v`, `vt`, and `vn` pools without ever merging vertices that have distinct UVs or normals.

---

## 5. Atomic Multi-File Export

`ObjFormatExporter.ExportSceneToFileAsync` stages all output files (`.obj`, `.mtl`, and optional copied textures) inside a temporary directory `.nexus_obj_stage_<guid>` first, parses back both `.obj` and `.mtl` to verify vertex/face/material counts, and only then moves the verified files into the target destination.
