# Đặc Tả Triển Khai Sponge Schematic Version 2 (`.schem`)

## 1. Cấu Trúc NBT Sponge v2

```text
TAG_Compound ("Schematic")
├── Version: TAG_Int = 2
├── DataVersion: TAG_Int
├── Width: TAG_Short (unsigned 16-bit)
├── Height: TAG_Short (unsigned 16-bit)
├── Length: TAG_Short (unsigned 16-bit)
├── Offset: TAG_Int_Array [x, y, z]
├── PaletteMax: TAG_Int
├── Palette: TAG_Compound { "<blockstate>": TAG_Int }
├── BlockData: TAG_Byte_Array (VarInt stream)
├── BlockEntities: TAG_List<TAG_Compound>
└── Metadata: TAG_Compound
```

## 2. Quy Trình Đọc (`SpongeV2Parser`) & Xuất (`SpongeV2Exporter`)

1. Kiểm tra `Version == 2` (`SCH002` nếu khác 2) và `DataVersion > 0` (`SCH013`).
2. Đọc `Width`, `Height`, `Length` dưới dạng số nguyên 16-bit dương và kiểm tra tràn số `Width * Height * Length`.
3. Giải mã `Palette` thành `SchematicPalette` (hỗ trợ chỉ số không liên tiếp) và đối chiếu `PaletteMax`.
4. Giải mã `BlockData` bằng `VarIntDecoder` theo thứ tự tuyến tính YZX:
   $$\text{index} = x + z \times \text{Width} + y \times \text{Width} \times \text{Length}$$
5. Đọc `BlockEntities` (và tương thích ngược với `TileEntities`), bảo toàn toàn bộ tag NBT con.
6. Bảo toàn `Entities` và các tag gốc chưa ánh xạ trong `UnmappedRootTags`.

