# Flow 4 — Execution, OSM, GPS, ETA, timeline và horse health

- Run: `UI-FULL-6-SCOPE-20261026-RERUN-01`
- FE: `http://localhost:5173/`
- Trip: `TRP-20261025-012` / `89c0d03e-99fa-44f3-b8a8-e643a3aefb7f`
- Tọa độ đều do người chạy chọn trên bản đồ OSM; không phải GPS thiết bị hoặc vị trí người thật.

## Nhật ký thao tác

| Bước | Vai trò / màn hình | Đã nhập hoặc đã bấm | Kết quả FE |
|---|---|---|---|
| 1 | Driver/Escort → **Execution** | Đăng nhập `driver.demo@racehorse.local`; chọn **Assigned trips → TRP-20261025-012 · READY**; bấm **Start execution**. | Toast `Trip execution initialized.` |
| 2 | Driver/Escort → Execution map (OpenStreetMap) | Click chọn departure `10.773443, 106.694205`; bấm **Record departure**. | Toast `Trip departure recorded.`; timeline có `DEPARTURE · DEPARTED`, event 1. |
| 3 | Driver/Escort → Execution map (OpenStreetMap) | Click chọn arrival `10.761976, 106.718581`; bấm **Record arrival**. | Toast `Trip arrival recorded.`; timeline có `DESTINATION · ARRIVED`, event 2. FE có lúc hiển thị Trip ARRIVED trên màn Execution. |
| 4 | Driver/Escort → **Execution**, OSM search | Nhập `Ben Thanh Market, Ho Chi Minh City` vào **Search for a place or address**, bấm **Search OSM**. | FE điều hướng lại `/?`, mất state màn/trip đang chọn. Chọn lại trip rồi **Load timeline** xác nhận event departure đã được server lưu. Tiếp tục thao tác bằng click map OSM thay vì Search. |
| 5 | Driver/Escort → **Tracking / Live tracking** | Chọn cùng trip; bấm **Load tracking**; click map để chọn current point `10.77504502318027, 106.6997842010498`; Accuracy `25 m`; Speed `20 km/h`; bấm **Record GPS point**. | FE lưu và hiển thị GPS sample; timestamp FE `10/26/2026 05:29:46`, speed `20 km/h`. Đây là điểm nhập thủ công trên OSM. |
| 6 | Driver/Escort → Live tracking | Chọn ETA destination `10.775003, 106.709054`; giữ Fallback minutes `60`; bấm **Calculate ETA**. | FE báo `ETA calculated from the latest GPS point`; ETA `10/26/2026 06:29:53`; distance remaining `1.0125829 km`; duration hiển thị `— min`; Schedule status `unavailable`. |
| 7 | Chuyển sang Customer → **Live tracking** | Đăng xuất Driver, đăng nhập `customer.demo@racehorse.local`; nhập Trip ID `89c0d03e-99fa-44f3-b8a8-e643a3aefb7f`; bấm **Load tracking**. | Customer thấy cùng GPS point, speed và ETA. |
| 8 | Customer → **Trip timeline** | Nhập cùng Trip ID; bấm **Load timeline**. | Customer thấy hai event DEPARTURE và DESTINATION. |
| 9 | Chuyển sang Manager → **Health** | Đăng nhập `manager.demo@racehorse.local`; chọn trip và horse `44444444-4444-4444-8444-444444444441` (Silver Comet); nhập condition `Good`, stress `2/10`, note `UI-FULL-6-SCOPE-20261026-RERUN-01 synthetic transit check: comfortable, hydration normal, no visible injury.`; bấm **Save observation**. | FE báo lưu thành công; current condition `GOOD`; history có observation stress `2/10`. |

## Kết quả và giới hạn

**PASS phần manual execution/tracking** — departure/arrival events, GPS point, ETA, Customer tracking/timeline và health observation đều đã hiện qua FE. Đây là test manual với tọa độ click OSM, không phải GPS thật.

ETA còn `1.0125829 km`, nhưng duration rỗng và schedule unavailable; FE đã ghi arrival event trong khi route plan được dời lịch sang 20/11. Vì vậy kết quả ETA này chỉ xác minh thao tác FE, không đại diện ETA vận hành. Hành vi Search OSM reload `/?` và làm mất selection đã ghi ở bước 4.
