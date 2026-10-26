# Chạy lại từ đầu — sáu flow trên FE

- Run ID: `UI-FULL-6-SCOPE-20261026-RERUN-01`.
- Ngày chạy: 2026-10-26 (Asia/Ho_Chi_Minh).
- Phạm vi: chạy trực tiếp trên `http://localhost:5173/` theo `Docs/racehorse-transport-docs/docs/PRODUCT_SCOPE.md`.
- Dữ liệu: chạy bằng dữ liệu test synthetic. Có tạo records mới; Flow 6 tái sử dụng Handover DRAFT tồn tại sẵn. Không reset database nghiệp vụ.
- Trạng thái: hoàn tất chạy lại sáu flow trên FE; mỗi flow có bảng từng bước gồm vai trò/màn hình, dữ liệu đã nhập/nút đã bấm và phản hồi FE; lỗi/retry và giới hạn xác minh cũng được ghi riêng.

| Flow | Trạng thái | Kết quả |
|---|---|---|
| 1. Request & approval | PASS | `REQ-20261025-4F56883A` → `ORD-20261025-D33AD114` → `TRP-20261025-012`; FE approval cần reason (UI hiện không nói rõ). |
| 2. Legal / quarantine / clearance | PASS | Readiness `1/1`; `CLR-20261025-2785` completed on FE. Hồ sơ hợp lệ đã có trong demo. |
| 3. Route & resource planning | PASS | `TRP-20261025-012` READY; route v2 ACTIVE, leg/checkpoints/xe/escort/horse đã gán. |
| 4. Execution / tracking / health / customer tracking | PASS* | Departure/arrival timeline, OSM manual points, GPS, ETA, customer view, health observation; ETA schedule unavailable. Search OSM reload bug recorded. |
| 5. Incident / emergency route change | PASS | `INC-20261025-0007` MEDIUM; notification SENT; route v3 ACTIVE (90 min, USD 50). OccurredAt timezone mismatch noted. |
| 6. Handover / acceptance / completion | PASS* | `HND-20261025-a4748910` COMPLETED; `TRP-20261025-012` COMPLETED. FE không hiển thị trạng thái Order nên chưa xác nhận Order completion. |

Các log Flow 1–6 cũ đã được xóa trước khi bắt đầu run này. Database nghiệp vụ không reset; Flow 6 sử dụng handover DRAFT đã tồn tại và ghi rõ trong log. Các tọa độ OSM, ghi chú và tài liệu test đều là dữ liệu synthetic; điểm chọn trên bản đồ không phải GPS thiết bị thật. PASS* nghĩa là flow đã được thao tác đến trạng thái ghi nhận, nhưng có giới hạn nêu trong cột kết quả/log.
