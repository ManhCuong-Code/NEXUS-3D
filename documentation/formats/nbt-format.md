# Đặc Tả Triển Khai Định Dạng NBT (Named Binary Tag)

## 1. Bảng 13 Loại Tag NBT Đầy Đủ

| Tag ID | Enum `NbtTagType` | Class Triển Khai | Kích Thước Payload |
|---|---|---|---|
| `0` | `End` | `NbtEndTag` | 0 byte (đánh dấu kết thúc `TAG_Compound`) |
| `1` | `Byte` | `NbtByteTag` | 1 byte có dấu (`sbyte`) |
| `2` | `Short` | `NbtShortTag` | 2 bytes Big-Endian (`short`) |
| `3` | `Int` | `NbtIntTag` | 4 bytes Big-Endian (`int`) |
| `4` | `Long` | `NbtLongTag` | 8 bytes Big-Endian (`long`) |
| `5` | `Float` | `NbtFloatTag` | 4 bytes IEEE 754 (`float`) |
| `6` | `Double` | `NbtDoubleTag` | 8 bytes IEEE 754 (`double`) |
| `7` | `ByteArray` | `NbtByteArrayTag` | `int32 length` + `length` bytes |
| `8` | `String` | `NbtStringTag` | `uint16 byteLength` + UTF-8 bytes |
| `9` | `List` | `NbtListTag` | `byte elementType` + `int32 count` + `count` payloads không tên |
| `10` | `Compound` | `NbtCompoundTag` | Chuỗi các named tags kết thúc bằng `TAG_End (0x00)` |
| `11` | `IntArray` | `NbtIntArrayTag` | `int32 length` + `length * 4` bytes |
| `12` | `LongArray` | `NbtLongArrayTag` | `int32 length` + `length * 8` bytes |

## 2. Phát Hiện Nén Tự Động (`NbtCompressionDetector`)

- **GZip**: Byte đầu `0x1F 0x8B` -> giải nén bằng `GZipStream` có kiểm soát giới hạn kích thước.
- **ZLib**: Byte đầu `0x78` (`0x01`, `0x5E`, `0x9C`, `0xDA`) -> giải nén bằng `ZLibStream`.
- **Uncompressed (None)**: Byte đầu `0x0A` (`TAG_Compound`).

## 3. Giới Hạn Bảo Mật Chống Zip-Bomb & StackOverflow (`NbtReadOptions`)

- `MaxCompressedBytes`: 64 MB
- `MaxDecompressedBytes`: 256 MB (`NBT005`)
- `MaxNestingDepth`: 128 cấp (`NBT004`)
- `MaxStringLength`: 32,767 bytes (`NBT005`)
- `MaxArrayLength`: 67,108,864 phần tử (`NBT005`)
- `MaxListLength`: 1,048,576 phần tử (`NBT005`)
- `MaxTagCount`: 2,097,152 tags (`NBT005`)

