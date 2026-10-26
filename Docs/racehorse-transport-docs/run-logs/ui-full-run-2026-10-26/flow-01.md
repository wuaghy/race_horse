# Flow 1 — Tạo và phê duyệt Transport Request

- Run: `UI-FULL-6-SCOPE-20261026-RERUN-01`
- Thực hiện: FE tại `http://localhost:5173/`; hai vai trò Customer và Logistics Manager.
- Dữ liệu test: synthetic; không phải chuyến/địa chỉ/khách hàng thật.

## Nhật ký thao tác

| Bước | Vai trò / màn hình | Đã nhập hoặc đã bấm | Kết quả FE |
|---|---|---|---|
| 1 | Customer → **Transport requests** | Bấm **+ New request**. | Mở form tạo request. |
| 2 | Customer → form request | Pickup `LOCAL-6S-20261025-191344 Pickup`; delivery `LOCAL-6S-20261025-191344 Delivery` (cả hai là test location tại Ho Chi Minh City). Horse `Silver Comet · DEMO-HORSE-001`. | Các điểm và ngựa được chọn trong form. |
| 3 | Customer → form request | Departure `2026-10-27 09:00`; expected arrival `2026-10-27 12:00`; mode `Road`; note `UI-FULL-6-SCOPE-20261026-RERUN-01 — synthetic FE workflow verification; not a real transport.` | Dữ liệu request được điền. |
| 4 | Customer → form request | Bấm **Create & submit request**. | Toast `Request submitted for review.`; request `REQ-20261025-4F56883A`, trạng thái `SUBMITTED`, có 1 horse. |
| 5 | Chuyển vai trò Customer → Manager | Đăng xuất, đăng nhập `manager.demo@racehorse.local`, mở **Transport requests**. | Queue Manager mở. |
| 6 | Manager → request queue | Tìm/mở `REQ-20261025-4F56883A`, bấm **Take request**. | Toast `Request taken into dispatch.`; trạng thái `UNDER_REVIEW`. |
| 7 | Manager → request review | Bấm **Approve & create transport order** khi chưa điền reason. | API trả HTTP 400, FE báo `Request failed (400)`. FE ghi reason chỉ cần khi yêu cầu bổ sung/từ chối, nhưng BE vẫn validate `Reason` là required/min length 1 khi approve. |
| 8 | Manager → request review | Điền reason `Approved for synthetic six-scope FE rerun; test data only.`; bấm lại **Approve & create transport order**. | Toast `Transport order created from the approved request.`; request `APPROVED`, order `ORD-20261025-D33AD114` (Order ID `938f16b0-ce98-4805-8990-949efdf0cf8f`). |
| 9 | Manager → request review | Bấm **Create trip & continue to planning**. | Trip `TRP-20261025-012` được tạo ở trạng thái `PLANNED`; Trip ID `89c0d03e-99fa-44f3-b8a8-e643a3aefb7f`. |

## Kết quả

**PASS** — request được tạo/submitted, Manager nhận và approve request, rồi tạo Order và Trip qua FE. Ghi nhận lỗi validation/UX ở bước 7: reason được mô tả như không bắt buộc khi approve nhưng BE từ chối nếu reason rỗng; đã nhập reason test và submit lại thành công.
