# Minecraft Bedrock Geometry JSON Specification

## 1. Overview
The **Minecraft Bedrock Geometry** format defines entity, block, and item model geometry for Minecraft Bedrock Edition. NEXUS-3D provides comprehensive parsing, validation, and serialization supporting:
- Legacy Schema `1.8.0` / `1.10.0` (`format_version` = "1.8.0" / "1.10.0", root entity keys like `"geometry.guardian"`).
- Modern Schemas `1.12.0` and `1.16.0`..`1.21.0` (`"minecraft:geometry"` list, description identifiers, texture sizes, visible bounds).

## 2. Structural Schema

```json
{
  "format_version": "1.16.0",
  "minecraft:geometry": [
    {
      "description": {
        "identifier": "geometry.guardian",
        "texture_width": 64,
        "texture_height": 64,
        "visible_bounds_width": 2.0,
        "visible_bounds_height": 2.0,
        "visible_bounds_offset": [ 0.0, 1.0, 0.0 ]
      },
      "bones": [
        {
          "name": "root",
          "pivot": [ 0.0, 0.0, 0.0 ],
          "rotation": [ 0.0, 0.0, 0.0 ],
          "cubes": [
            {
              "origin": [ -6.0, 0.0, -6.0 ],
              "size": [ 12.0, 12.0, 12.0 ],
              "uv": [ 0, 0 ],
              "inflate": 0.0,
              "mirror": false
            }
          ]
        }
      ]
    }
  ]
}
```

## 3. Features Supported in NEXUS-3D
1. **Bone Hierarchy**:
   - `name`, `parent`, `pivot`, `rotation`, `binding`.
   - Cyclic dependency detection and topological sorting.
   - Validation against missing parent bones.
2. **Cube Primitives**:
   - `origin`, `size`, `inflate`, `mirror`.
   - Box UV (`[u, v]`) and Per-face UV (`north`, `south`, `east`, `west`, `up`, `down`).
3. **Locators & PolyMesh**:
   - Bone locators (`offset`, `rotation`).
   - Bedrock PolyMesh support for normalized face-vertex primitives.
4. **Coordinate System Adaptation**:
   - Bedrock: Right-Handed, Y-Up, 16 units = 1 block / 1 meter.
   - Converted to USM metric space and back seamlessly with coordinate transformations and UV un-flipping.
5. **Loss Analysis**:
   - Molang expressions, procedural animations, and specific render controllers are preserved in `ExtensionData` and recorded in `BedrockConversionReport`.
