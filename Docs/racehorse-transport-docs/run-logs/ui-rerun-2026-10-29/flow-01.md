# Flow 1 — Tạo request, review và tạo Order

- Run: `UI-SIX-SCOPE-RERUN-20261029-01`
- Cách chạy: thao tác trực tiếp trên FE `http://localhost:5173/`, đăng nhập Customer rồi Logistics Manager.
- Dữ liệu: synthetic local demo; không phải khách hàng, ngựa hay chuyến thật.
- Request: `REQ-20261029-31AB20D7`
- Order: `ORD-20261029-35F0BE88` (ID `06e0e961-f7fb-4879-9a66-ccc2648c3025`)
- Trip tạo để nối sang các scope sau: `TRP-20261029-017`

## Nhật ký từng bước

| # | Vai trò / màn | Thao tác và dữ liệu đã nhập | Kết quả nhìn thấy trên FE |
|---|---|---|---|
| 1 | Customer → Transport requests | Đăng nhập `customer.demo@racehorse.local`. Bấm **+ New request**. | Mở form New transport request; danh sách địa điểm và ngựa tải được. |
| 2 | Customer → New transport request | Pickup `LOCAL-6S-20261025-191344 Pickup`; delivery `LOCAL-6S-20261025-191344 Delivery`; horse `Silver Comet · DEMO-HORSE-001`; mode `Road`; departure `2026-11-05 09:00`; expected arrival `2026-11-05 12:00`; note `SCOPE-6-20261029-FE-01 — dữ liệu synthetic để chạy thử sáu scope; không phải chuyến vận chuyển thật.` | Form phân biệt pickup/delivery; mốc giờ hiển thị đúng `11/5/2026 9:00 AM`. |
| 3 | Customer → New transport request | Bấm **Create & submit request**. | Toast `Request submitted for review.`; request `REQ-20261029-31AB20D7`, `SUBMITTED`, 1 horse, departure `11/5/2026, 9:00 AM`. |
| 4 | Logistics Manager → Transport requests | Đăng xuất Customer, đăng nhập `manager.demo@racehorse.local`, mở **Transport requests** và chọn request vừa tạo. | Queue có `REQ-20261029-31AB20D7`; màn review hiện đúng pickup, delivery, horse và note synthetic. |
| 5 | Manager → Request review | Bấm **Take request**. | Toast `Request taken into dispatch.`; trạng thái chuyển `SUBMITTED → UNDER_REVIEW`. |
| 6 | Manager → Request review | Để trống **Message to requester**, bấm **Approve & create transport order**. | FE báo `Request failed (400)`. Không tạo Order ở lần bấm này. |
| 7 | Manager → Request review | Nhập message `Approved for synthetic FE scope rerun on 2026-10-29.` rồi bấm lại **Approve & create transport order**. | Toast `Transport order created from the approved request.`; request `APPROVED`, Order `ORD-20261029-35F0BE88`. Ghi nhận UI cho biết reason chỉ bắt buộc khi xin bổ sung/từ chối nhưng API hiện đang trả 400 khi duyệt không có reason. |
| 8 | Manager → Trips, routes & assignments | Bấm **Create trip & continue to planning** từ request đã duyệt. | Toast `Trip TRP-20261029-017 is ready for route planning and staff assignment.`; Trip `PLANNED`, Order ID `06e0e961-f7fb-4879-9a66-ccc2648c3025`. |

## Kết quả

**PASS có lỗi UX/API ghi nhận.** Customer tạo và gửi request qua FE; Manager nhận request, duyệt sau khi thêm message, Order được tạo, rồi FE tạo Trip `TRP-20261029-017`. Duyệt khi message rỗng thất bại HTTP 400 dù FE ghi message là không bắt buộc. Dữ liệu giờ ở form request được gửi và hiển thị đúng trong lần này.
