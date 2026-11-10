# NEXUS-3D: Interactive 3D Editor Tools Architecture

## 1. Overview and Design Principles

The **Interactive 3D Editor** in NEXUS-3D provides real-time spatial transformations, bone hierarchy editing, texture slot assignment, and UV mapping for Universal Scene Model (USM) scenes. Designed according to Clean Architecture and MVVM patterns, the editor separates pure mathematical manipulation from presentation controls.

```
+-------------------------------------------------------------------------+
|                              NEXUS3D.App                                |
|  - ViewportWorkspaceView (Toolbar, Shortcuts Q/W/E/R/T, Undo/Redo)      |
|  - Viewport3DControl & TransformGizmoVisual3D (Interactive Drag)        |
|  - ObjectInspectorViewModel (Numeric Transform, Pivot, UV Controls)     |
+-------------------------------------------------------------------------+
                                    |
                                    v
+-------------------------------------------------------------------------+
|                          NEXUS3D.Editor.Engine                          |
|  - Services: TransformService, PivotService, BoneEditorService,         |
|              TextureEditorService, SelectionService, UndoRedoService    |
|  - Picking: RaycastPicker (Möller–Trumbore, AABB slab intersection)     |
|  - Commands: TransformCommand, ApplyTransformCommand, PivotCommand,    |
|              BoneTransformCommand, BoneReparentCommand, UVCommand       |
|  - Mathematics: TransformMath (Decompose TRS, Quaternion, Euler)        |
+-------------------------------------------------------------------------+
                                    |
                                    v
+-------------------------------------------------------------------------+
|                  NEXUS3D.Editor.Abstractions & Domain                   |
|  - ITransformService, IPivotService, IBoneEditorService,               |
|    ITextureEditorService, ISelectionService, IUndoRedoService           |
|  - SelectionState, SnapSettings, UVTransformSettings                    |
+-------------------------------------------------------------------------+
                                    |
                                    v
+-------------------------------------------------------------------------+
|                             NEXUS3D.Domain                              |
|  - Scene3D, SceneNode, Transform3D, MeshData, VoxelGrid, SkeletonData   |
+-------------------------------------------------------------------------+
```

---

## 2. Interactive Gizmo and Selection System

### 2.1. Multi-Selection & Raycast Picking
- **Selection Service (`ISelectionService`)**: Tracks single or multiple selected `SceneNode` entities and active `BoneData`. Multi-selection is toggled using `Ctrl+Click`. Group bounding boxes and central pivot points are computed on-the-fly.
- **Raycast Picking (`IRaycastPicker`)**:
  - Implements the **Möller–Trumbore** ray-triangle intersection algorithm for polygon meshes.
  - Implements slab intersection tests against bounding boxes for voxel grids.
  - Automatically converts world rays into the node's local coordinate frame via $M_{world}^{-1}$.

### 2.2. Interactive 3D Gizmos (`TransformGizmoVisual3D`)
- **Translate Gizmo**: 3 colored axis arrows (X: Red, Y: Green, Z: Blue), 3 planar handles (XY: Yellow, XZ: Cyan, YZ: Magenta), and a center origin handle.
- **Rotate Gizmo**: 3 orthogonal rings/toruses (X, Y, Z) and a center ball.
- **Scale Gizmo**: 3 colored axis stems with cubic tips and a central cube for uniform scaling.
- **Pivot Gizmo**: Spherical diamond with crosshairs for repositioning local origin.
- **Camera-Distance Scaling**: Scales dynamically as $s = \max(0.2, d_{cam} \times 0.08)$ to remain visually consistent at any zoom level.

---

## 3. Mathematical Foundations

### 3.1. Transform Snapping and Quaternions
- Rotations are computed entirely using Hamilton quaternions ($q = [x, y, z, w]$), preventing gimbal lock.
- Euler angles in degrees $(\text{Pitch}, \text{Yaw}, \text{Roll})$ are derived via:
  $$\text{Roll} = \text{atan2}(2(wz + xy), 1 - 2(y^2 + z^2))$$
  $$\text{Pitch} = \text{asin}(\text{clamp}(2(wx - yz), -1, 1))$$
  $$\text{Yaw} = \text{atan2}(2(wy + zx), 1 - 2(x^2 + y^2))$$
- Snapping rules apply discrete rounding according to configurable steps (e.g. $0.5$ units for translation, $15^\circ$ for rotation, $0.1$ for scale).

### 3.2. Pivot Editing with Invariant World Appearance
When shifting the node pivot by displacement $\Delta$:
1. Local mesh vertex positions shift by $-\Delta$:
   $$V'_{local} = V_{local} - \Delta$$
2. Child node local translations shift by $-\Delta$:
   $$P'_{child} = P_{child} - \Delta$$
3. Node position in parent space compensates by $+R \cdot (S \cdot \Delta)$:
   $$P'_{node} = P_{node} + R_{node} \cdot (S_{node} \cdot \Delta)$$
Because $[P_{node} + R \cdot (S \cdot \Delta)] + R \cdot [S \cdot (V_{local} - \Delta)] = P_{node} + R \cdot (S \cdot V_{local})$, the world position of every vertex in the mesh and all children remains algebraically identical!

### 3.3. Apply / Bake Transform
Bakes the node's local matrix $M = T \cdot R \cdot S$ into:
1. Every vertex $V \leftarrow M \cdot V$ and normal $N \leftarrow (M^{-1})^T \cdot N$.
2. Child transforms $M_{child} \leftarrow M \cdot M_{child}$.
3. Resets $T \leftarrow (0,0,0)$, $R \leftarrow I$, $S \leftarrow (1,1,1)$.

---

## 4. Bone Kinematics & Hierarchy Safeguards

- **Forward Kinematics**:
  $$\text{World}_{bone} = \text{ParentWorld} \cdot T(\text{Pivot}) \cdot \text{LocalMatrix} \cdot T(-\text{Pivot})$$
- **Cycle Guard**: Verifies candidate parent is not a descendant of the bone being reparented, preventing circular dependency chains ($A \to B \to C \to A$).
- **Pose Preservation**: On reparenting, the new local transform is recalculated as $M'_{local} = M_{newParent}^{-1} \cdot M_{world}$, ensuring bones do not jump when reorganizing the skeleton tree.

---

## 5. Undo/Redo Command Architecture

Continuous mouse drags produce dozens of intermediate preview states. To ensure crisp undo behavior:
1. **MouseDown**: Captures initial transforms and records start point.
2. **MouseMove**: Continuously updates preview transforms in real-time.
3. **MouseUp**: Consolidates the entire drag session into a single `IEditorCommand` pushed to `IUndoRedoService`.

