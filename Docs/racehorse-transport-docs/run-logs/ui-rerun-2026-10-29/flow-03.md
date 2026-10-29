# Flow 3 — Lập route, phân công và kích hoạt

- Run: `UI-SIX-SCOPE-RERUN-20261029-01`
- Trip: `TRP-20261029-017`; Route plan: `913bed1b-0497-4192-82d8-f71224789075`.
- Thực hiện trực tiếp trên FE bằng Manager demo. Mọi địa điểm, thời gian và tài nguyên là dữ liệu synthetic.

## Nhật ký từng bước

| # | Màn / thao tác | Dữ liệu và kết quả FE |
|---|---|---|
| 1 | Trips, routes & assignments → Trip `TRP-20261029-017` → **Create route plan** | Toast `Route plan created.`; version 1 `DRAFT`. |
| 2 | **Add route leg** | Sequence 1; pickup `LOCAL-6S-20261025-191344 Pickup`; delivery `LOCAL-6S-20261025-191344 Delivery`; Road; `2026-11-05 09:00–12:00`; 15 km. Toast `Route leg added.` |
| 3 | **Add checkpoint** | #1 `DEPARTURE` tại pickup, arrival 08:45, departure 09:00; #2 `DESTINATION` tại delivery, arrival 12:00, departure 12:15. FE xác nhận checkpoint được thêm. |
| 4 | **Assign resource** | Xe `VEH-20261025191557 · TEST-191557` bị từ chối do xung đột lịch. Đổi sang `VEH-327D86C863564E8AB6D29609C120A20A · TEST-LOCAL-001`; toast `Resource assigned.` |
| 5 | **Assign staff** | Tài khoản duy nhất hiện trong danh sách là `Demo Driver & Escort`. Chọn vai trò `Escort`; FE trả `Staff member is already assigned to a conflicting route leg during this period.` Không có Escort được gán. |
| 6 | **Assign horse** | Chọn `Silver Comet · DEMO-HORSE-001` và ngăn chuồng synthetic; toast `Horse assigned.` FE hiển thị horse ID `44444444-4444-4444-8444-444444444441` và compartment ID `535c42f8-5734-4829-9d45-db5928b32c6c`. |
| 7 | Route version → **Submit** → **Approve** → **Activate** | FE lần lượt xác nhận `Version submitted for approval`, `Version approved`, `Version activated`; version 1 thành `ACTIVE`. Không có chốt READY vì thiếu Escort. |

## Kết quả và blocker

**Route được tạo và kích hoạt; phân công/READY chưa hoàn chỉnh.** Không thể gán Escort vì tài khoản demo duy nhất bị báo trùng lịch. Sau khi tạo route change ở Flow 5 và kích hoạt version 2, FE readiness còn báo thiếu vehicle/flight và Escort; version mới không giữ đủ các assignment như kỳ vọng. Trip không được đánh dấu `READY` trước khi chạy execution. Flow 6 sau đó hoàn tất handover và FE cập nhật Trip thành `COMPLETED`.

## Kết quả sau sửa

Blocker overlap assignment và mất assignment khi đổi tuyến đã được xử lý, sau đó chạy lại trên record chain Trip 018. FE xác nhận route v3 active, đủ vehicle/staff/horse/stall và `No blockers reported.` Chi tiết ở [Flow 3 repair rerun](../ui-repair-rerun-2026-10-29/flow-03.md).
