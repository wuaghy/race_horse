# Flow 5 — Incident và đổi tuyến khẩn cấp

- Run: `UI-SIX-SCOPE-RERUN-20261029-01`
- Trip: `TRP-20261029-017`; incident `INC-20261029-0001`; route plan `913bed1b-0497-4192-82d8-f71224789075`.
- Tất cả dữ liệu là synthetic; incident giả lập fatigue/traffic, không mô tả sự cố thật.

## Nhật ký từng bước

| # | Màn / thao tác | Dữ liệu và kết quả FE |
|---|---|---|
| 1 | **Incidents** → trip `TRP-20261029-017`; chọn horse `44444444-4444-4444-8444-444444444441`; type `Horse illness or injury`; severity `MEDIUM`. | Nhập title `SCOPE-6-20261029-FE-01 synthetic horse fatigue observation`; occurred at `2026-10-29 09:13` theo form; details ghi rõ đây là synthetic, không có vận chuyển/ngựa thật; không đính kèm bằng chứng thật. |
| 2 | **Submit incident report** | Toast `Incident and attached evidence reported to operations.`; tạo `INC-20261029-0001`, trip ID `42b9b26d-f981-4dd5-b215-5a53095bd813`, severity `MEDIUM`, status `OPEN`. |
| 3 | **Resolve** trên incident vừa tạo | FE không mở prompt nhập Resolution summary và không đổi trạng thái sau click; incident vẫn `OPEN`. Không ghi nhận là đã điều tra/resolve. |
| 4 | **Route changes** → trip `TRP-20261029-017` | Route plan ID tự tải `913bed1b-0497-4192-82d8-f71224789075`. |
| 5 | **Propose route version** | Duration `40` phút; additional cost `0 USD`; reason `SCOPE-6-20261029-FE-01 synthetic traffic delay exercise. Proposed 40 minute ETA extension for demonstration only; no real disruption.` FE tạo version 2 `DRAFT`, ID `259f3df6-07c7-4b5e-be58-a47691d41432`. |
| 6 | Version 2 cần có leg trước khi gửi | Bấm **Submit** ngay tại màn Route changes bị từ chối: `Cannot submit a route plan version with no route legs.` Chuyển **Trips, routes & assignments**, chọn version 2 DRAFT, thêm leg sequence 1 Road từ pickup `LOCAL-6S-20261025-191344` đến delivery cùng request; `2026-11-05 09:00–12:40`, 15 km. Toast `Route leg added.` |
| 7 | Quay lại **Route changes**, chọn lại trip và **Load version history** | Version 2 hiện trong lịch sử ở `DRAFT`, duration 40 phút, cost 0 USD. Bấm **Submit** → `Route version submitted for approval.` |
| 8 | **Approve** → **Activate** version 2 | FE xác nhận `Route version approved.` rồi `New route version activated.`; version 1 `ARCHIVED`, version 2 `ACTIVE`. Thao tác được FE cho phép với cùng tài khoản Manager tạo đề xuất. |

## Kết quả và giới hạn

**PASS một phần.** Emergency route version đã có leg, qua `DRAFT → PENDING_APPROVAL → APPROVED → ACTIVE`; incident được ghi nhưng còn `OPEN` vì nút Resolve không mở được trường Resolution summary. Sau route change, FE readiness báo thiếu vehicle/flight và Escort; cần kiểm tra vì assignment trước đó không được giữ trên active version mới.

## Kết quả sau sửa

Form Resolve inline đã ghi resolution và chuyển incident sang `RESOLVED`; route assignments bị thiếu được khôi phục trên route v3. Kết quả repair trên Trip 018 được ghi riêng tại [Flow 5 repair rerun](../ui-repair-rerun-2026-10-29/flow-05.md); log này vẫn giữ nguyên lịch sử Trip 017.
