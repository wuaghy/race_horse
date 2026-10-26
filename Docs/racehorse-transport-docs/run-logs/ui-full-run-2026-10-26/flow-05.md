# Flow 5 — Incident và emergency route change

- Run: `UI-FULL-6-SCOPE-20261026-RERUN-01`
- FE: `http://localhost:5173/`
- Trip: `TRP-20261025-012` / `89c0d03e-99fa-44f3-b8a8-e643a3aefb7f`
- Route plan: `86d1b1bf-bab6-4910-94b9-e6949658140b`

## Nhật ký thao tác

| Bước | Vai trò / màn hình | Đã nhập hoặc đã bấm | Kết quả FE |
|---|---|---|---|
| 1 | Manager → **Incidents** | Chọn trip `TRP-20261025-012`; type `Traffic delay`; severity `MEDIUM`; title `UI-FULL-6-SCOPE-20261026-RERUN-01 synthetic traffic delay`; details `Synthetic test-only incident for Flow 5 verification. No actual trip, horse, driver, or vehicle affected.` | Form incident được điền bằng dữ liệu synthetic. |
| 2 | Manager → form incident | Nhập Occurred at `2026-10-26T05:45`; bấm **Submit incident report**. | FE hiển thị `An unexpected server error occurred.` Read-only Incident API log sau đó ghi `OccurredAt cannot be in the future`: FE `datetime-local` và server UTC hiểu thời điểm khác nhau. |
| 3 | Manager → form incident | Đổi Occurred at thành `2026-10-25T21:00`; giữ nguyên các trường khác; bấm lại **Submit incident report**. | FE báo `Incident and attached evidence reported to operations.` Incident `INC-20261025-0007`, `OPEN`, `MEDIUM`, gắn đúng Trip ID. |
| 4 | Manager → **Notifications** | Refresh/quan sát notification liên quan incident. | Hiển thị `[MEDIUM] Incident Reported: INCIDENT`, event `New Incident`, trạng thái `SENT`. |
| 5 | Manager → **Route changes** | Mở route plan `86d1b1bf-bab6-4910-94b9-e6949658140b`; tạo proposed version với duration `90` phút; additional cost `50 USD`; reason `Incident INC-20261025-0007: synthetic traffic delay. Proposed local test detour; no actual shipment or additional cost.` | Đề xuất route change được tạo để xử lý incident test. |
| 6 | Manager → proposed route version | Thêm leg từ Origin `31ab12ee-2104-4c77-8246-f5710f08dacd` (synthetic pickup) đến Destination `5026f4fd-a940-4fd5-b1e7-328a1c9f933f` (synthetic delivery), departure `2026-11-20 06:00`, arrival `2026-11-20 07:30`. | Leg được gắn vào route Version 3. |
| 7 | Manager → Route changes | Bấm **Submit**. | Version 3 chuyển `PENDING_APPROVAL`. |
| 8 | Chuyển sang Admin → Route changes | Đăng nhập Admin demo, mở đề xuất route change, bấm **Approve**. | Version 3 chuyển `APPROVED`. |
| 9 | Admin → Route changes | Bấm **Activate**. | Version 3 chuyển `ACTIVE`; Version 2 chuyển `ARCHIVED`. |

## Kết quả

**PASS** — incident `INC-20261025-0007` được tạo và notification `SENT`; proposed route Version 3 được duyệt/kích hoạt với duration 90 phút và additional cost 50 USD. Không có giao hàng hoặc chi phí thật. Ghi nhận lỗi timezone ở bước 2: thời gian FE nhập bị BE đánh giá là tương lai và lỗi được render thành unexpected server error; đổi sang `2026-10-25T21:00` thì submit thành công.
