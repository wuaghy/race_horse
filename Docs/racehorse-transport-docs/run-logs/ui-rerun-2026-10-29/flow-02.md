# Flow 2 — Compliance readiness và border clearance

- Run: `UI-SIX-SCOPE-RERUN-20261029-01`
- Cách chạy: thao tác trực tiếp trên FE; Logistics Manager tạo clearance, Admin demo xử lý review vì UI hiện chỉ cấp nút review cho Compliance Officer/Admin.
- Trip: `TRP-20261029-017`; horse: Silver Comet / `DEMO-HORSE-001`.
- Country: Vietnam, country ID `d0acfc32-85c6-4ba0-b99f-ab030b75dbb8`.
- Clearance: `CLR-20261029-1323`; authority/reference/notes đều synthetic, không gửi hồ sơ cho cơ quan thật.

## Nhật ký từng bước

| # | Vai trò / màn | Thao tác và dữ liệu đã nhập | Kết quả nhìn thấy trên FE |
|---|---|---|---|
| 1 | Logistics Manager → Trip readiness | Chọn Trip `TRP-20261029-017` trong dropdown Trip (thay vì gõ UUID); Origin country ID và Destination country ID đều `d0acfc32-85c6-4ba0-b99f-ab030b75dbb8`; Horse IDs `44444444-4444-4444-8444-444444444441`; Departure date `2026-11-05`. | Các tiêu chí được điền trên FE; Trip dropdown lấy ID nội bộ từ lựa chọn TripNo. |
| 2 | Manager → Trip readiness | Bấm **Evaluate requirements**. | `Ready for departure`; `1/1 requirements met`, `0 missing`, `0 expired`, `0 in review`. Kết quả dùng hồ sơ demo đã có trong DB; lần này không upload giấy tờ mới. |
| 3 | Manager → Border clearance | Chọn Trip `TRP-20261029-017`; bấm **+ New case**. | Mở form New border clearance case, Trip được giữ theo lựa chọn FE. |
| 4 | Manager → New border clearance case | Country ID `d0acfc32-85c6-4ba0-b99f-ab030b75dbb8`; Authority `Synthetic local test authority`; Reference `SCOPE-6-20261029-FE-01-CL-01`; Notes `Test-only clearance; no official submission or real shipment.`; bấm **Create case**. | Toast `Clearance case created.`; case `CLR-20261029-1323`, trạng thái `DRAFT`. |
| 5 | Manager → Border clearance | Bấm **Submit** ở case `CLR-20261029-1323`. | Toast `Clearance case submitted.`; trạng thái `SUBMITTED`. |
| 6 | Admin demo → Border clearance | Đăng xuất Manager, đăng nhập `admin.demo@racehorse.local`; chọn lại Trip `TRP-20261029-017`, bấm **Load cases**. | Case `CLR-20261029-1323` tải được ở trạng thái `SUBMITTED`; xuất hiện nút **Start review**. |
| 7 | Admin → Border clearance | Bấm **Start review**. | Toast `Clearance case moved to review.`; trạng thái `UNDER_REVIEW`. |
| 8 | Admin → Border clearance | Bấm **Approve**. | Toast `Clearance case approved.`; trạng thái `APPROVED`. |
| 9 | Admin → Border clearance | Bấm **Complete**. | Toast `Clearance case completed.`; trạng thái `COMPLETED`. |

## Kết quả và giới hạn

**PASS với hồ sơ readiness đã có sẵn.** Readiness đạt 1/1 theo dữ liệu demo; clearance mới được tạo và đi hết vòng đời trên FE `DRAFT → SUBMITTED → UNDER_REVIEW → APPROVED → COMPLETED`. Đây là dữ liệu synthetic, không phải giấy phép/clearance chính thức. UI readiness và clearance yêu cầu UUID ở phiên bản ban đầu; trong lần chạy này đã cập nhật chúng để chọn TripNo từ danh sách FE và thực hiện bằng dropdown.
