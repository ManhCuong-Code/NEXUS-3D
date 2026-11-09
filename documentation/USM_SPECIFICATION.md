# NEXUS 3D – Giai Đoạn 02: Universal Scene Model (USM)
## Tài Liệu Đặc Tả Kỹ Thuật & Kiến Trúc Mô Hình Trung Gian

---

## 1. Tổng Quan Kiến Trúc USM

**Universal Scene Model (USM)** là trái tim dữ liệu trung gian (Intermediate Scene Representation) của **NEXUS 3D**. USM được thiết kế hoàn toàn độc lập với định dạng file ngoài, độc lập với giao diện WPF, và độc lập với cơ sở dữ liệu SQLite.

```text
             INPUT FORMATS (.schem / .schematic / .obj / .mtl / .geo.json / .png)
                                       |
                                FORMAT PARSERS (Stage 03+)
                                       |
                                       v
+-----------------------------------------------------------------------------+
|                        UNIVERSAL SCENE MODEL (USM)                          |
|                                                                             |
|  Scene3D (Root Aggregate - Schema v1)                                       |
|   ├── SceneGraph ──> SceneNode (Root -> Children) ──> SceneObject           |
|   │                                                    ├── List<MeshData>   |
|   │                                                    ├── List<VoxelGrid>  |
|   │                                                    └── SkeletonId?      |
|   ├── CoordinateMetadata (SourceSystem, CurrentSystem, Unit, ConversionMat) |
|   ├── MaterialLibrary ──> List<MaterialData> ──> TextureSlot                |
|   ├── List<TextureData> ──> TextureReference + TextureMetadata              |
|   ├── List<SkeletonData> ──> BoneData + BoneInfluence                       |
|   ├── List<AnimationData> ──> AnimationTrack + AnimationKeyframe            |
|   └── SceneMetadata ──> SourceMetadata, Extensions, RawDataContainer,       |
|                         ProvenanceHistory, Warnings, LossReport             |
+-----------------------------------------------------------------------------+
                                       |
                    VALIDATION ENGINE (SceneValidator / USM001..USM019)
                                       |
             +-------------------------+-------------------------+
             |                         |                         |
     SCENE SERIALIZER          CONVERSION ENGINE          RENDERING / UI
   (SceneJsonSerializer)      (Voxel <-> Mesh, etc.)       (Stage 04+)
```

---

## 2. Quy Ước Toán Học & Hệ Tọa Độ Chuẩn (Mathematical Conventions)

| Thành phần | Quy ước chuẩn trong `NEXUS3D.Domain` |
|---|---|
| **Độ chính xác số thực** | `double` (64-bit IEEE 754) cho toàn bộ tính toán hình học trong Domain |
| **Bố cục `Matrix4x4D`** | Row-major fields (`M11..M44`), nhân vector cột `v' = M * v`, Translation nằm tại cột 4 (`M14, M24, M34`) |
| **Thứ tự ghép `Transform3D`** | `LocalMatrix = TranslationMatrix * RotationMatrix * ScaleMatrix` (`M = T * R * S`) |
| **Tích lũy phân cấp Cha-Con** | `WorldMatrix = ParentWorldMatrix * LocalMatrix` (Iterative traversal, chống đệ quy) |
| **Biến đổi pháp tuyến (Normal)** | `NormalWorld = Normalize((Inverse(WorldMatrix))^T * NormalLocal)` |
| **Hệ tọa độ chuẩn USM** | Right-Handed, `+Y` Up, `-Z` Forward, `+X` Right, Đơn vị: `Block` (`1.0m`) |
| **Chuyển đổi cơ sở hệ tọa độ** | `R = unitScale * B_target * (B_source)^T`; tự động phát hiện lật thứ tự đỉnh (`RequiresWindingFlip`) khi đổi Handedness |

---

## 3. Phân Nhóm Bảo Toàn Dữ Liệu (Data Preservation & Loss Report)

USM phân biệt minh bạch 5 trạng thái bảo toàn dữ liệu thông qua `LossReport` và `DataFidelityCategory`:

1. **`Supported`**: Dữ liệu được ánh xạ trực tiếp 100% vào các cấu trúc USM (`MeshData`, `VoxelGrid`, `MaterialData`, `SkeletonData`, `AnimationData`).
2. **`PreservedRaw`**: Dữ liệu đặc thù của định dạng gốc (ví dụ NBT tùy chỉnh của Sponge Schematic hoặc trường mở rộng Bedrock) được lưu nguyên vẹn trong `RawDataContainer` hoặc `ExtensionData` (namespaced `prefix:name`) để tái xuất (round-trip) không mất mát.
3. **`Approximated`**: Dữ liệu đã được xấp xỉ sang mô hình tương đương (ví dụ chuyển shader đặc thù sang PBR `MaterialData`).
4. **`Unmapped`**: Dữ liệu được nhận diện nhưng chưa ánh xạ sang thành phần hình học.
5. **`Lost`**: Dữ liệu thực sự bị lược bỏ (`HasDataLoss == true` chỉ khi tồn tại mục `Lost`).

---

## 4. Bảng Mã Lỗi Chuẩn USM (`UsmErrorCodes`)

| Mã lỗi | Hằng số | Ý nghĩa |
|---|---|---|
| `USM001` | `InvalidScene` | Cấu trúc `Scene3D` hoặc `SceneGraph` vi phạm bất biến cơ bản |
| `USM002` | `DuplicateId` | Trùng lặp `Guid` giữa các `SceneNode`, `MaterialData`, `TextureData`, `BoneData` |
| `USM003` | `SceneGraphCycle` | Phát hiện chu trình cha-con (cycle) trong `SceneGraph` |
| `USM004` | `NodeNotFound` | Không tìm thấy `SceneNode` được tham chiếu |
| `USM005` | `InvalidTransform` | `Transform3D` chứa `NaN`/`Infinity`, scale bằng `0`, hoặc cảnh báo scale âm |
| `USM006` | `InvalidCoordinateSystem` | Hệ trục tọa độ không trực giao (`UpAxis` song song `ForwardAxis`) |
| `USM007` | `InvalidVoxelGrid` | `VoxelGrid` có `VoxelSize <= 0`, vị trí voxel không hợp lệ hoặc không khớp bounds |
| `USM008` | `InvalidPaletteIndex` | `Voxel.PaletteIndex` nằm ngoài phạm vi `VoxelPalette` |
| `USM009` | `InvalidMesh` | `MeshData` chứa đỉnh `NaN`, đa giác `< 3` đỉnh, tam giác suy biến hoặc pháp tuyến chưa chuẩn hóa |
| `USM010` | `InvalidVertexIndex` | `Face.VertexIndices` vượt quá phạm vi danh sách `MeshData.Vertices` |
| `USM011` | `MaterialReferenceNotFound` | Tham chiếu `MaterialId` không tồn tại hoặc thuộc tính `MaterialData` ngoài khoảng `[0, 1]` |
| `USM012` | `TextureReferenceNotFound` | Tham chiếu `TextureId` không tồn tại hoặc `TextureData` không hợp lệ |
| `USM013` | `InvalidSkeleton` | Chu trình xương, thiếu xương cha, hoặc tổng trọng số `BoneInfluence` khác `1.0` |
| `USM014` | `InvalidAnimation` | Thời lượng âm, keyframe không tăng dần theo thời gian, hoặc thiếu đối tượng đích |
| `USM015` | `InvalidMetadata` | `ExtensionData.Namespace` sai quy ước `prefix:name` |
| `USM016` | `UnsupportedSchemaVersion` | Phiên bản schema USM JSON không được hỗ trợ |
| `USM017` | `SerializationFailed` | Dữ liệu JSON bị hỏng hoặc sai định dạng `NEXUS3D.USM` |
| `USM018` | `DataLimitExceeded` | Vượt giới hạn độ sâu (`MaxDepth`) hoặc kích thước payload (`MaxPayloadBytes`) |
| `USM019` | `InvalidRawData` | `RawDataContainer` thiếu thông tin nguồn hoặc vượt giới hạn bộ nhớ |

