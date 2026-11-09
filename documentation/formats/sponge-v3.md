# Đặc Tả Triển Khai Sponge Schematic Version 3 (`.schem`)

## 1. Cấu Trúc NBT Sponge v3

```text
TAG_Compound ("")
└── Schematic: TAG_Compound
    ├── Version: TAG_Int = 3
    ├── DataVersion: TAG_Int
    ├── Width: TAG_Short
    ├── Height: TAG_Short
    ├── Length: TAG_Short
    ├── Offset: TAG_Int_Array [x, y, z]
    ├── Metadata: TAG_Compound
    ├── Blocks: TAG_Compound
    │   ├── Palette: TAG_Compound
    │   ├── Data: TAG_Byte_Array (VarInt stream)
    │   └── BlockEntities: TAG_List<TAG_Compound> (chứa Id, Pos, Data)
    ├── Biomes: TAG_Compound (Tùy chọn)
    └── Entities: TAG_List<TAG_Compound> (Tùy chọn)
```

## 2. Điểm Khác Biệt Giữa Sponge v3 Và Sponge v2

- Toàn bộ dữ liệu khối nằm trong compound con `Blocks` (`Blocks.Palette`, `Blocks.Data`, `Blocks.BlockEntities`).
- Mỗi phần tử trong `Blocks.BlockEntities` lưu dữ liệu riêng của khối bên trong compound con `Data` thay vì trộn ở cấp ngoài cùng như v2.
- `SpongeV3Parser` bảo toàn nguyên vẹn `Biomes` và `Entities` dưới dạng raw NBT (`PreservedBiomesTag`, `PreservedEntitiesTag`) kèm cảnh báo minh bạch trong `Warnings` và `LossReport`, giúp round-trip `Sponge v3 -> USM -> Sponge v3` không làm mất dữ liệu quần xã hay thực thể.

