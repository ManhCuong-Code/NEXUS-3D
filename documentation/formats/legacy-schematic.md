# Đặc Tả Triển Khai Legacy Schematic (`.schematic`)

## 1. Cấu Trúc NBT Legacy MCEdit / WorldEdit

```text
TAG_Compound ("Schematic")
├── Width: TAG_Short
├── Height: TAG_Short
├── Length: TAG_Short
├── Materials: TAG_String ("Alpha")
├── Blocks: TAG_Byte_Array (8-bit thấp của Block ID)
├── Data: TAG_Byte_Array (4-bit metadata 0..15)
├── AddBlocks: TAG_Byte_Array (Tùy chọn, packed 4-bit cao của Block ID)
├── TileEntities: TAG_List<TAG_Compound> (id, x, y, z, ...)
├── Entities: TAG_List<TAG_Compound>
├── WEOffsetX / WEOffsetY / WEOffsetZ: TAG_Int
└── WEOriginX / WEOriginY / WEOriginZ: TAG_Int
```

## 2. Xử Lý Block ID Mở Rộng Và Metadata

- `Blocks[i] & 0xFF` cung cấp 8 bit thấp (`0..255`).
- Khi có `AddBlocks`, 4 bit cao (`highNibble`) được giải nén từ `AddBlocks[i >> 1]` để tạo thành Block ID 12-bit (`0..4095`).
- Các cặp `(blockId, metadata)` chưa có trong bảng ánh xạ chuẩn 1.12.2 -> 1.13+ **không bao giờ bị đoán bừa thành Stone hoặc Air**, mà được bảo toàn chính xác dưới dạng `legacy:block_<id>[meta=<metadata>]` kèm cảnh báo `SCH009`.

