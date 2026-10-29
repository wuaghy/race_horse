# Flow 4 — Execution, tracking và tình trạng ngựa

- Run: `UI-SIX-SCOPE-RERUN-20261029-01`
- Trip: `TRP-20261029-017` (UUID nội bộ `42b9b26d-f981-4dd5-b215-5a53095bd813`).
- Thao tác trực tiếp trên FE `Execution`; actor đăng nhập thực tế là Logistics Manager demo vì không gán được Driver/Escort ở Flow 3. Không phải GPS thiết bị hoặc chuyến thật.

## Nhật ký từng bước

| # | Màn / thao tác | Dữ liệu và kết quả FE |
|---|---|---|
| 1 | **Execution** → chọn `TRP-20261029-017 · PLANNED` → **Load timeline** | FE ban đầu báo chưa có execution record (`Trip execution not found.`), timeline chưa có event. |
| 2 | **Start execution** | Toast `Trip execution initialized.`; execution hiển thị trạng thái `PLANNED`, `Not departed`. Trip number lấy từ dropdown; không nhập Trip number lần thứ hai. |
| 3 | **Choose departure point on OpenStreetMap** | Bấm một điểm trên bản đồ OSM, FE hiển thị `10.785922, 106.700900`; bấm **Record departure**. Toast `Trip departure recorded.`; execution đổi `PLANNED → DEPARTED`; event #1 có tọa độ `10.7859218, 106.7009`. |
| 4 | **Choose arrival point on OpenStreetMap** | Bấm điểm OSM `10.776816, 106.700900`; bấm **Record arrival**. Toast `Trip arrival recorded.`; execution đổi `DEPARTED → ARRIVED`; event #2 có tọa độ `10.7768157, 106.7009`. |

## Kết quả và giới hạn FE

**PASS cho việc khởi tạo execution và ghi hai event OSM.** Điểm bản đồ là tọa độ người dùng chọn, không phải GPS realtime. Khi xem timeline Handover, FE hiện `Checkpoint 1/2 · null` thay vì tọa độ; trang Execution lúc ghi event có hiển thị đúng coordinates. UI Handover cũng hiển thị event theo `2:12 AM`, trong khi execution detail trước đó hiển thị `9:12 AM` cho cùng event (lệch 7 giờ); cần sửa/kiểm chứng chuyển đổi timezone. Trip đang được chọn vẫn có thể hiện nhãn `PLANNED` trong dropdown đã tải trước khi cập nhật; trạng thái cuối `COMPLETED` được xác nhận lại ở Flow 6 sau completion event.
