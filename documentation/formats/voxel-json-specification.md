# Voxel JSON Format Specification

## 1. Overview
The **Voxel JSON** format in NEXUS-3D provides a schema-flexible, human-readable, and machine-parsable representation for 3D volumetric / block-based models. It supports multiple community and engine dialects:
1. **Nexus Voxel Grid v1 (`nexus_voxel_grid_v1`)**: The native, loss-minimized schema for NEXUS-3D containing metadata, bounding grid dimensions, palettes, layered sparse voxels, and extension attributes.
2. **Sparse Voxel Array (`sparse_voxel_array`)**: A compact format representing individual voxels with coordinate tuples `[x, y, z]` or `{x, y, z}` and color/material keys.
3. **Minecraft Block Model Cuboids (`block_model_cuboids`)**: Compatible with standard Minecraft Java block models specifying elements with `from`, `to`, `faces`, and rotation.

## 2. Dialects

### 2.1. Nexus Voxel Grid v1 (`nexus_voxel_grid_v1`)
Standard schema:
```json
{
  "formatVersion": "1.0",
  "dialect": "nexus_voxel_grid_v1",
  "metadata": {
    "name": "Lantern",
    "author": "NEXUS-3D",
    "description": "Standard voxel grid",
    "createdAtUtc": "2026-11-10T00:00:00Z"
  },
  "grid": {
    "sizeX": 16,
    "sizeY": 16,
    "sizeZ": 16,
    "voxelSize": 1.0,
    "palette": [
      { "id": 0, "name": "nexus:iron_frame", "color": "#4A4A4A" },
      { "id": 1, "name": "nexus:glowstone_core", "color": "#FFD700" }
    ]
  },
  "layers": [
    {
      "name": "BaseLayer",
      "visible": true,
      "voxels": [
        { "x": 0, "y": 0, "z": 0, "paletteIndex": 0 },
        { "x": 0, "y": 1, "z": 0, "paletteIndex": 1 }
      ]
    }
  ]
}
```

### 2.2. Sparse Voxel Array (`sparse_voxel_array`)
A flattened list of coordinates and palette IDs or hex colors:
```json
{
  "voxels": [
    { "x": 0, "y": 0, "z": 0, "color": "#FF0000" },
    { "x": 1, "y": 0, "z": 0, "color": "#00FF00" }
  ]
}
```

### 2.3. Minecraft Block Model Cuboids
Element-based definition converted to voxel space using unit resolution:
```json
{
  "elements": [
    {
      "from": [ 0, 0, 0 ],
      "to": [ 16, 16, 16 ],
      "faces": {
        "down":  { "texture": "#down", "cullface": "down" },
        "up":    { "texture": "#up", "cullface": "up" }
      }
    }
  ]
}
```

## 3. Integration with Universal Scene Model (USM)
- Mapped to `VoxelGrid` in `Scene3D`.
- Supports palettes with RGBA hex color codes and material definitions (`MaterialData`).
- Preserves layer metadata and unknown JSON properties in `SceneMetadata.ExtensionData["VoxelJson_ExtensionData"]`.
- Round-trip fidelity guaranteed with zero block loss in sparse representations.
