# Flow 3 — Route, vehicle, staff, horse và readiness

- Run: `UI-FULL-6-SCOPE-20261026-RERUN-01`
- FE: `http://localhost:5173/`
- Trip: `TRP-20261025-012` / `89c0d03e-99fa-44f3-b8a8-e643a3aefb7f`
- Order: `ORD-20261025-D33AD114` / `938f16b0-ce98-4805-8990-949efdf0cf8f`
- Route plan ID: `86d1b1bf-bab6-4910-94b9-e6949658140b`

## Nhật ký thao tác

| Bước | Vai trò / màn hình | Đã nhập hoặc đã bấm | Kết quả FE |
|---|---|---|---|
| 1 | Manager → **Trips, routes & assignments** | Chọn trip vừa tạo từ Order `938f16b0-ce98-4805-8990-949efdf0cf8f`; mở route planning và bấm **Create route plan**. | Route plan được tạo; bắt đầu Version 1. |
| 2 | Manager → Route Version 1 | Thêm leg Pickup `LOCAL-6S-20261025-191344 Pickup` → Delivery `LOCAL-6S-20261025-191344 Delivery`; mode `ROAD`; distance `15 km`; departure `2026-10-27 09:00`; arrival `2026-10-27 12:00`. | Leg được thêm vào Version 1. |
| 3 | Manager → Resource assignment | Thử gán xe `VEH-20261025191358 · TEST-191358`. | FE từ chối vì xe bị trùng route leg/booking. Chuyển sang xe `VEH-20261025191557 · TEST-191557`; gán thành công cho Version 1. |
| 4 | Manager → Staff assignment | Thử gán `driver.demo@racehorse.local` cho lịch 27/10. | FE báo `Staff member is already assigned to a conflicting route leg.` |
| 5 | Manager → Route planning | Tạo Version 2 với reason `Reschedule synthetic run to avoid existing demo resource bookings.`; thêm lại leg cùng pickup/delivery, `ROAD`, `15 km`, departure `2026-11-20 09:00`, arrival `2026-11-20 12:00`. | Version 2 chứa leg mới với lịch tránh booking demo xung đột. |
| 6 | Manager → Version 2 → Resource assignment | Gán xe `VEH-20261025191358 · TEST-191358`. | Vehicle assignment được lưu. |
| 7 | Manager → Version 2 → Staff assignment | Chọn `Demo Driver & Escort`, role `ESCORT`, bấm **Assign staff**. | FE lưu assignment ESCORT. Account driver demo đã bị booking trùng ở lịch 27/10; cùng account vẫn được dùng để chạy màn Execution trong Flow 4. |
| 8 | Manager → Version 2 → Horse assignment | Gán `Silver Comet · DEMO-HORSE-001`, Horse ID `44444444-4444-4444-8444-444444444441`, stall `STALL-20261025191358`. | Horse và stall synthetic được gán vào leg. |
| 9 | Manager → Route-leg checkpoints | Tạo checkpoint `DEPARTURE` tại pickup, 20/11 `08:45–09:00`; tạo `DESTINATION` tại delivery, 20/11 `12:00–12:15`. | Hai checkpoint được lưu. |
| 10 | Manager → Route planning | Bấm **Submit** cho Version 2. | Version 2 chuyển `PENDING_APPROVAL`. |
| 11 | Đổi vai trò sang Admin → Route planning | Đăng nhập Admin demo, mở trip/route, bấm **Approve**, rồi **Activate**. | Version 2 chuyển `APPROVED → ACTIVE`; Version 1 được lưu thành `DRAFT` trong flow ban đầu. |
| 12 | Manager/Admin → **Trip readiness** | Đánh giá readiness cho trip sau khi route/resources/checkpoints đã gán. | FE báo `No blockers reported.`; bấm **Confirm READY**. FE báo `Trip confirmed ready.`; Trip thành `READY`. |

## Kết quả

**PASS tại thời điểm kết thúc Flow 3** — route Version 2 active, leg, xe, staff role ESCORT, horse/stall và hai checkpoints được thiết lập; Trip thành `READY`. Lịch được dời từ 27/10 sang 20/11 để tránh xung đột resource demo. Flow 5 sau đó tạo Version 3 emergency route và archive Version 2; xem [flow-05.md](flow-05.md).
