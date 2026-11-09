# Thuật Toán Giải Mã Palette Và Ánh Xạ Tuyến Tính YZX

## 1. Công Thức Địa Chỉ Hóa Tuyến Tính YZX (`SchematicIndexing`)

Cả Sponge Schematic (v2/v3) và Legacy `.schematic` đều lưu trữ khối theo thứ tự trục $X$ tăng nhanh nhất, sau đó đến $Z$, và cuối cùng là $Y$:

$$\text{index} = x + z \times \text{Width} + y \times \text{Width} \times \text{Length}$$

Quá trình giải ngược từ `index` sang tọa độ 3D $(x, y, z)$:

$$\text{layerSize} = \text{Width} \times \text{Length}$$

$$y = \lfloor \text{index} / \text{layerSize} \rfloor, \quad r = \text{index} \bmod \text{layerSize}$$

$$z = \lfloor r / \text{Width} \rfloor, \quad x = r \bmod \text{Width}$$

## 2. Giải Mã Palette (`PaletteDecoder`)

1. Duyệt toàn bộ tag `TAG_Int` trong compound `Palette` (Sponge v2) hoặc `Blocks.Palette` (Sponge v3).
2. Phân tích chuỗi khóa bằng `BlockStateParser.Parse(tag.Name)` thành `BlockState` chuẩn hóa.
3. Hỗ trợ chỉ số Palette không liên tiếp (*non-contiguous indices*) đúng theo đặc tả Sponge.
4. Khi giải mã mảng `BlockData`, mọi chỉ số giải mã từ `VarIntDecoder` đều được đối chiếu tức thời với `SchematicPalette.ContainsIndex(index)` (`SCH005` nếu không tồn tại) và kiểm tra tổng số khối đọc được bằng đúng $\text{Width} \times \text{Height} \times \text{Length}$ (`SCH004`).

