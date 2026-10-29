# UI scope rerun — 2026-10-29

- Run ID: `UI-SIX-SCOPE-RERUN-20261029-01`.
- Chạy trực tiếp trên FE `http://localhost:5173/`; BE chạy bằng Docker Compose hiện có, không build lại image.
- Dữ liệu vừa tạo trong lần này là synthetic local demo. Database/volume không bị reset; các record lịch sử cũ vẫn còn.

| Scope | Kết quả | Record chính |
|---|---|---|
| 1 Request → Order → Trip | PASS, có lỗi API duyệt khi message rỗng trả 400 | `REQ-20261029-31AB20D7`, `ORD-20261029-35F0BE88`, `TRP-20261029-017` |
| 2 Readiness / Clearance | PASS với hồ sơ readiness demo có sẵn | `CLR-20261029-1323` |
| 3 Route / assignment | Route active, nhưng không đủ Escort nên không đạt READY | Route plan `913bed1b-0497-4192-82d8-f71224789075` |
| 4 Execution / OSM | PASS cho start, departure, arrival; có lệch timezone ở timeline Handover | Trip `TRP-20261029-017` |
| 5 Incident / route change | Một phần: incident còn OPEN; route version 2 active | `INC-20261029-0001`, version `259f3df6-07c7-4b5e-be58-a47691d41432` |
| 6 Handover / completion | Handover và Trip COMPLETED; Order status chưa thấy trên FE | `HND-20261029-40e2c9e6`, Order ID `06e0e961-f7fb-4879-9a66-ccc2648c3025` |

Chi tiết thao tác ở `flow-01.md` … `flow-06.md`. Không coi trạng thái Trip là bằng chứng Order đã complete. Không xóa dữ liệu nghiệp vụ; chỉ thay bộ log cũ bằng bộ log rerun này.

## Bản chạy sửa tiếp theo

Bộ log trên là lịch sử lần chạy Trip 017, không phải trạng thái cuối mới nhất. Các blocker Flow 3, Flow 5 và xác nhận Order ở Flow 6 đã được xử lý trên record chain riêng Trip 018 trong [bộ log repair đủ sáu scope](../ui-repair-rerun-2026-10-29/README.md). Flow 1, Flow 2 và Flow 4 ở bộ này vẫn ghi lại lần chạy Trip 017; không ghép clearance của Trip 017 vào Trip 018.
