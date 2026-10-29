# Flow 6 — Handover, Customer acceptance và completion

- Run: `UI-SIX-SCOPE-RERUN-20261029-01`
- Trip: `TRP-20261029-017`; Handover `HND-20261029-40e2c9e6`; Order ID `06e0e961-f7fb-4879-9a66-ccc2648c3025`.
- Toàn bộ là dữ liệu demo synthetic; không đại diện shipment, kiểm tra ngựa, chữ ký pháp lý hay Customer acceptance thật.

## Nhật ký từng bước

| # | Vai trò / màn | Thao tác và dữ liệu | Kết quả FE |
|---|---|---|---|
| 1 | Manager → **Handover** | Chọn Trip `TRP-20261029-017`, bấm **Load trip timeline**. FE mở/hiển thị draft `HND-20261029-40e2c9e6`; không tạo draft thứ hai. | Timeline có departure/arrival, incident Flow 5 và audit của handover. |
| 2 | Manager → receiver | Receiver `Demo Customer Receiver`; email tài khoản demo customer; schedule nhập `2026-10-29T10:00`; notes ghi rõ synthetic/no real shipment/signature/acceptance; **Save receiver details**. | Toast `Handover details saved.`; completion blocker giảm còn horse PENDING và signature. Form sau lưu hiển thị 03:00 cho giá trị nhập 10:00, ghi nhận lệch 7 giờ cần kiểm tra timezone. |
| 3 | Manager → horse | Thêm horse `44444444-4444-4444-8444-444444444441`; initial condition `Stable (synthetic check)`; notes `Synthetic demo only; no actual horse inspection.`; **Add horse**. | Toast `Horse added to handover.`; horse status `PENDING`. |
| 4 | Manager → inspection | Chọn `Issue found`; note `Synthetic condition exercise: mild fatigue reported; no actual inspection or horse transport.`; **Inspect**. | Toast `Horse inspection saved.`; horse condition `ISSUE_FOUND`. |
| 5 | Manager → submit | Bấm **Submit for acceptance**. | Toast `Handover submitted for customer acceptance.`; `DRAFT → PENDING_ACCEPTANCE`. |
| 6 | Customer demo → **Handover** | Tải trip bằng ID `42b9b26d-f981-4dd5-b215-5a53095bd813`; chọn Silver Comet → **Accept**. | Toast `Horse acceptance recorded.`; horse `PENDING → ACCEPTED`, handover `PENDING_ACCEPTANCE → ACCEPTED`. |
| 7 | Manager → signature | FE yêu cầu receiver signature. Tạo rồi upload `/tmp/scope-6-synthetic-signature.png`, ảnh chỉ ghi `SYNTHETIC DEMO — NOT A SIGNATURE`, không phải chữ ký người nhận. | Toast `Signature uploaded.`; completion requirement thành `Ready to complete`. |
| 8 | Manager → complete | Bấm **Complete handover**. | Toast `Handover completed.`; `HND-20261029-40e2c9e6` thành `COMPLETED`. |
| 9 | Manager → **Trips, routes & assignments** | Refresh/đi vào danh sách Trips và kiểm tra Trip `TRP-20261029-017`. | FE hiển thị Trip `COMPLETED`; Order ID vẫn hiện nhưng FE không hiển thị Order status. Chưa xác nhận được Order `COMPLETED` từ FE. |

## Kết quả và giới hạn

**PASS cho Handover/Trip; Order completion chưa xác minh trên FE.** Customer demo đã accept và Manager hoàn tất Handover với ảnh placeholder được ghi rõ không phải chữ ký. FE cập nhật Trip `COMPLETED`; theo `PRODUCT_SCOPE.md`, cần đọc trạng thái Order từ Booking riêng vì FE chưa có phần hiển thị trạng thái Order sau event consumer.

## Kết quả sau sửa

FE nay hiển thị Order status đọc từ Booking. Trên Trip 018, Handover, Trip và Order đều được xác nhận `COMPLETED` sau event consumer; ảnh placeholder vẫn chỉ là dữ liệu synthetic. Chi tiết ở [Flow 6 repair rerun](../ui-repair-rerun-2026-10-29/flow-06.md). Kết quả này thuộc record chain mới, không sửa ngược dữ liệu Trip 017.
