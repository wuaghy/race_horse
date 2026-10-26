# Flow 6 — Handover, horse inspection, customer acceptance, signature và completion

- Run: `UI-FULL-6-SCOPE-20261026-RERUN-01`
- FE: `http://localhost:5173/`
- Trip `TRP-20261025-012` / `89c0d03e-99fa-44f3-b8a8-e643a3aefb7f`
- Order `ORD-20261025-D33AD114` / `938f16b0-ce98-4805-8990-949efdf0cf8f`
- Horse Silver Comet / `44444444-4444-4444-8444-444444444441`

## Nhật ký thao tác

| Bước | Vai trò / màn hình | Đã nhập hoặc đã bấm | Kết quả FE |
|---|---|---|---|
| 1 | Manager → **Handover** | Chọn `TRP-20261025-012 · READY`; bấm **Load trip timeline**. | FE tải HND `HND-20261025-a4748910 · DRAFT` có sẵn trong DB. Ban đầu vùng form mới báo chưa có record; khi load timeline, FE lấy được draft. |
| 2 | Manager → **Prepare new handover** | Thử tạo draft mới với Receiver name `Demo Customer Acceptance`, email `customer.demo@racehorse.local`, scheduled at `2026-10-26T13:30`; bấm **Create handover draft**. | FE báo `A handover record already exists for this trip.` Không tạo bản ghi thứ hai; tiếp tục dùng HND draft hiện có. Database không reset. |
| 3 | Manager → HND `HND-20261025-a4748910` → **Receiver and schedule** | Receiver name `Demo Customer Acceptance`; email `customer.demo@racehorse.local`; scheduled at `2026-10-26T13:30`; notes `UI-FULL-6-SCOPE-20261026-RERUN-01 synthetic handover test; no real horse transfer.`; bấm **Save receiver details**. | Toast `Handover details saved.`; event `UPDATED DRAFT → DRAFT`. FE hiển thị lại scheduled at `2026-10-26T06:30`, lệch 7 giờ so với giá trị nhập. |
| 4 | Manager → HND → phần thêm horse | Dropdown **Horse** chọn `44444444-4444-4444-8444-444444444441`; Initial condition `GOOD`; Notes `UI-FULL-6-SCOPE-20261026-RERUN-01 synthetic arrival inspection: healthy, no visible injury.`; bấm **Add horse**. | Toast `Horse added to handover.`; horse xuất hiện trong bảng với trạng thái `PENDING`. Summary báo cần submit acceptance, kiểm tra horse và chữ ký. |
| 5 | Manager → hàng Silver Comet → **Inspection condition** | Giữ lựa chọn `Healthy`; Inspection notes `UI-FULL-6-SCOPE-20261026-RERUN-01 visual inspection: no issue observed; synthetic demo.`; bấm **Inspect**. | Toast `Horse inspection saved.`; timeline có `HORSE_INSPECTED ... HEALTHY`; horse vẫn `PENDING` chờ khách nhận. |
| 6 | Manager → HND | Bấm **Submit for acceptance**. | Toast `Handover submitted for customer acceptance.`; HND chuyển `DRAFT → PENDING_ACCEPTANCE`. |
| 7 | Chuyển vai trò Manager → Customer | Đăng xuất Manager, đăng nhập `customer.demo@racehorse.local`; mở **Handover**; nhập Trip ID `89c0d03e-99fa-44f3-b8a8-e643a3aefb7f` vào **Trip ID**; bấm **Load trip timeline**. | Customer tải được `HND-20261025-a4748910 · PENDING_ACCEPTANCE`; timeline hiển thị Silver Comet, kiểm tra `HEALTHY`, trạng thái `PENDING`. |
| 8 | Customer → bảng horse trong HND | Bấm **Accept** cho Silver Comet. | Toast `Horse acceptance recorded.`; HND chuyển sang `ACCEPTED`; horse thành `ACCEPTED`, ghi chú `Accepted on delivery.`. |
| 9 | Chuyển vai trò Customer → Manager | Đăng xuất Customer, đăng nhập `manager.demo@racehorse.local`; mở **Handover**, chọn lại `TRP-20261025-012`, bấm **Load trip timeline**. | FE hiển thị HND `HND-20261025-a4748910 · ACCEPTED`; horse đã được customer chấp nhận. |
| 10 | Manager → HND → **Upload signature** | Bấm **Upload signature**; chọn file test `/tmp/ui-full-flow6-test-signature.png`, ảnh ghi `SYNTHETIC TEST SIGNATURE` và `NO LEGAL EFFECT · DEMO ONLY`. | Toast `Signature uploaded`; timeline có event `SIGNATURE_UPLOADED ui-full-flow6-test-signature.png`; FE báo `Ready to complete`. File không chứa chữ ký người thật. |
| 11 | Manager → HND | Bấm **Complete handover**. | Toast `Handover completed.`; HND đổi `ACCEPTED → COMPLETED`; timeline ghi event COMPLETED. |
| 12 | Manager → **Trips, routes & assignments** | Chọn `TRP-20261025-012 · COMPLETED` để tải chi tiết trip. | FE hiển thị `TRP-20261025-012 COMPLETED · Order 938f16b0-ce98-4805-8990-949efdf0cf8f · Compliance ready`. Dropdown tại Handover từng vẫn hiển thị `READY`; trang Trips sau khi refresh thể hiện trạng thái mới `COMPLETED`. |
| 13 | Chuyển vai trò Manager → Customer → **Notifications** | Đăng xuất Manager, đăng nhập Customer; mở **Notifications**. | FE hiển thị `You’re all caught up`; không có notification Order completion. Màn Request/Handover/Trip trên FE chỉ cho thấy Order ID liên kết, không render trạng thái hiện tại của Order. |

## Kết quả và giới hạn xác minh

**PASS handover và Trip** — thao tác FE đã đi qua receiver details, horse add, inspection, submit acceptance, customer accept, signature upload và complete. FE xác nhận HND `COMPLETED` và Trip `COMPLETED`.

**Order completion chưa xác minh trên FE** — Trip detail có hiển thị Order ID nhưng FE không có trạng thái Order trên các màn đã kiểm tra; Customer notifications cũng không có event Order completion. Do đó không suy luận Order `COMPLETED` từ Trip `COMPLETED`. Cần màn FE/API tra trạng thái Order hoặc consumer log/DB read-only để xác nhận riêng.

## Lưu ý dữ liệu

- HND `HND-20261025-a4748910` đã tồn tại trước lần chạy này. Tạo draft trùng bị FE từ chối; đã ghi rõ và tái sử dụng bản DRAFT.
- Database không được reset. Tên người nhận, note, horse inspection, GPS/log notes và ảnh chữ ký đều là dữ liệu synthetic; không có ngựa thật được bàn giao.
