# Thuật Toán Giải Nén Và Đóng Gói `AddBlocks` (12-Bit Block ID)

## 1. Cấu Trúc Nibble Trong `AddBlocks`

Định dạng Legacy `.schematic` lưu 8 bit thấp của Block ID trong mảng `Blocks` (`byte[totalBlocks]`). Khi công trình chứa các khối có ID từ `256` đến `4095`, mảng tùy chọn `AddBlocks` (`byte[(totalBlocks + 1) / 2]`) lưu 4 bit cao (`highNibble`) của 2 khối liên tiếp trong mỗi byte:

- **Chỉ số chẵn (`(index & 1) == 0`)**: lưu ở 4 bit thấp (`addBlocks[index >> 1] & 0x0F`).
- **Chỉ số lẻ (`(index & 1) == 1`)**: lưu ở 4 bit cao (`(addBlocks[index >> 1] >> 4) & 0x0F`).

$$\text{blockId} = (\text{highNibble} \ll 8) \mid (\text{blocks}[\text{index}] \ \& \ 0\text{xFF})$$

## 2. Đóng Gói Khi Xuất (`LegacyBlockEncoder`)

Khi xuất sang `.schematic`:
- Nếu tất cả khối đều có $\text{ID} \le 255$, mảng `AddBlocks` được lược bỏ (`null`) để tiết kiệm dung lượng và tương thích tối đa.
- Nếu có bất kỳ khối nào có $\text{ID} \in [256, 4095]$, `LegacyBlockEncoder` tự động cấp phát mảng `AddBlocks` có độ dài $\lceil N / 2 \rceil$ và đóng gói nibble chẵn/lẻ đối xứng 100% với `LegacyBlockDecoder`.

