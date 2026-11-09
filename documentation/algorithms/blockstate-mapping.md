# Thuật Toán Phân Tích Cú Pháp BlockState Và Ánh Xạ Legacy <-> Modern

## 1. Phân Tích Và Chuẩn Hóa BlockState (`BlockStateParser` & `BlockStateFormatter`)

Chuỗi BlockState trong Minecraft 1.13+ có cú pháp:

```text
namespace:block_name[prop1=value1,prop2=value2]
```

- Nếu khuyết `namespace:`, hệ thống tự động chuẩn hóa về `minecraft:<block_name>`.
- Các thuộc tính trong ngoặc vuông `[...]` được kiểm tra cú pháp chặt chẽ (`SCH007` nếu rỗng, thiếu dấu `=`, hoặc không đóng ngoặc `]`) và sắp xếp theo thứ tự từ điển (`StringComparer.Ordinal`) khi định dạng bằng `BlockStateFormatter.Format`.

## 2. Ánh Xạ Hai Chiều Legacy ID (1.12.2) <-> Modern BlockState (`LegacyBlockIdMapping`)

1. **Chiều Legacy -> USM (`TryMapToBlockState`)**:
   - Tra cứu cặp `(blockId, metadata)` trong bảng ánh xạ chuẩn (Stone, Granite, Planks, Logs với trục xoay `axis=x/y/z`, 16 màu Wool/Terracotta/StainedGlass/Concrete, Stairs, Slabs, Chest, Furnace, Sign...).
   - **Nguyên tắc bảo toàn tuyệt đối**: Nếu gặp `(blockId, metadata)` chưa có trong từ điển (ví dụ khối mod `750:5`), hệ thống **không bao giờ đoán bừa thành Stone hoặc Air**, mà tạo `BlockState` bảo toàn `legacy:block_750[meta=5]` kèm cảnh báo `SCH009`.
2. **Chiều USM -> Legacy (`TryMapToLegacy`)**:
   - Nếu `BlockState` thuộc namespace `legacy:block_<id>[meta=<m>]`, trích xuất lại chính xác `<id>` (`0..4095`) và `<m>` (`0..15`).
   - Nếu là `BlockState` hiện đại đã đăng ký, ánh xạ về `(blockId, metadata)` tương ứng.
   - Nếu không ánh xạ được: ném lỗi `SCH009` khi bật `StrictMapping = true`, hoặc fallback có ghi nhận cảnh báo `SCH009` vào `Warnings`.

