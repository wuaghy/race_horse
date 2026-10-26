# Flow 2 — Legal / quarantine / border clearance

- Run: `UI-FULL-6-SCOPE-20261026-RERUN-01`
- FE: `http://localhost:5173/`
- Trip: `TRP-20261025-012` / `89c0d03e-99fa-44f3-b8a8-e643a3aefb7f`
- Horse: Silver Comet / `44444444-4444-4444-8444-444444444441`
- Country: Vietnam / `d0acfc32-85c6-4ba0-b99f-ab030b75dbb8`

## Nhật ký thao tác

| Bước | Vai trò / màn hình | Đã nhập hoặc đã bấm | Kết quả FE |
|---|---|---|---|
| 1 | Manager → **Trip readiness** | Trip ID `89c0d03e-99fa-44f3-b8a8-e643a3aefb7f`; origin country ID và destination country ID đều `d0acfc32-85c6-4ba0-b99f-ab030b75dbb8`; horse ID `44444444-4444-4444-8444-444444444441`; departure date `2026-10-27`. | Các tiêu chí chuyến được nhập/chọn. |
| 2 | Manager → **Trip readiness** | Bấm **Evaluate requirements**. | FE: `Ready for departure`, `1/1 requirements met`, `0 missing`, `0 expired`, `0 in review`. Hồ sơ đủ điều kiện đã tồn tại trong dữ liệu demo. Không upload tài liệu mới ở lần chạy này. |
| 3 | Manager → **Border clearance** | Bấm **+ New case**. | Mở form clearance. |
| 4 | Manager → form clearance | Trip ID như trên; Country ID `d0acfc32-85c6-4ba0-b99f-ab030b75dbb8`; Authority `Synthetic local test authority`; Reference `UI-FULL-6-SCOPE-20261026-RERUN-01-CL-01`; Notes `Test-only clearance; no official submission or real shipment.`; bấm **Create case**. | Clearance `CLR-20261025-2785` được tạo ở `DRAFT`. |
| 5 | Manager → clearance case | Bấm **Submit**. | Trạng thái `SUBMITTED`. |
| 6 | Đổi vai trò sang Admin demo | Đăng xuất Manager, đăng nhập Admin demo; mở/lấy lại case `CLR-20261025-2785`. Local demo không có account Transport Specialist/Compliance Officer trong danh sách đã kiểm tra, nên dùng Admin cho các nút review có quyền. | Case vẫn `SUBMITTED`. |
| 7 | Admin → clearance case | Bấm lần lượt **Start review**, **Approve**, **Complete**; kiểm tra trạng thái sau từng thao tác. | `SUBMITTED → UNDER_REVIEW → APPROVED → COMPLETED`. |

## Kết quả

**PASS với hồ sơ demo đã có** — readiness 1/1; case `CLR-20261025-2785` đi hết vòng đời trên FE. Flow này xác nhận khả năng đánh giá hồ sơ và xử lý clearance, nhưng không tạo/upload hồ sơ pháp lý mới. Authority/reference/notes là dữ liệu synthetic, không gửi cho cơ quan thật.
