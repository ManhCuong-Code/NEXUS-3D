# Thuật Toán Mã Hóa Và Giải Mã VarInt (`BlockData`)

## 1. Nguyên Lý Base-128 LEB128 VarInt

Trong Sponge Schematic v2 (`BlockData`) và Sponge Schematic v3 (`Blocks.Data`), mỗi chỉ số khối trong `Palette` là một số nguyên không âm được mã hóa bằng chuỗi byte độ dài biến thiên (1 đến 5 bytes):

- **7 bit thấp** (`byte & 0x7F`) chứa dữ liệu giá trị theo thứ tự Little-Endian theo nhóm 7-bit.
- **Bit thứ 8** (`byte & 0x80`) là bit tiếp nối (*continuation bit*):
  - `1` (`0x80`): còn byte tiếp theo thuộc cùng một VarInt.
  - `0`: byte cuối cùng của VarInt hiện tại.

| Khoảng Giá Trị Palette Index | Số Bytes VarInt |
|---|---|
| `0 .. 127` | 1 byte |
| `128 .. 16,383` | 2 bytes |
| `16,384 .. 2,097,151` | 3 bytes |
| `2,097,152 .. 268,435,455` | 4 bytes |
| `268,435,456 .. 2,147,483,647` | 5 bytes |

## 2. Triển Khai Trong `VarIntDecoder` & `VarIntEncoder`

- **`VarIntDecoder.ReadVarInt`**: Kiểm tra tràn bộ đệm (`SCH006`), giới hạn tối đa 5 bytes (`bitOffset >= 35` -> `SCH006`), và từ chối mọi giá trị giải mã âm (`value < 0` -> `SCH006`).
- **`VarIntEncoder.WriteVarInt`**: Mã hóa số nguyên không âm bằng vòng lặp dịch phải 7 bit (`unsignedVal >>= 7`), ném `SchematicException(SchematicErrorCodes.InvalidVarInt)` nếu đầu vào âm.

