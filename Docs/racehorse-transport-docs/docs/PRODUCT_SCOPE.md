# Phạm vi sản phẩm và luồng vận hành

## 1. Mục tiêu

Quản lý vòng đời vận chuyển ngựa từ lúc khách tạo yêu cầu đến khi hoàn tất bàn giao, đồng bộ trạng thái đơn và lưu lịch sử vận hành.

```text
Customer request
  → Manager review and Order creation
  → Compliance and clearance
  → Trip, route and resource planning
  → READY gate
  → Driver execution, tracking and horse health
  → Incident handling and route revision (when needed)
  → Arrival and handover
  → Customer acceptance or dispute
  → Handover completion
  → Trip completion event
  → Order completion event
```

Request, Order, Trip, execution, route version và Handover là các đối tượng riêng. Một trạng thái hoàn tất ở đối tượng này không tự chứng minh đối tượng khác cũng đã hoàn tất.

## 2. Vai trò và quyền theo luồng

| Vai trò | Trách nhiệm chính |
|---|---|
| Customer | Tạo request, quản lý ngựa/hồ sơ, theo dõi trip, phản hồi nhận hoặc tranh chấp tình trạng ngựa |
| Logistics Manager | Nhận xét duyệt request, tổ chức trip, bàn giao, xử lý sự cố và theo dõi vận hành |
| Transport Specialist / Compliance Officer | Duy trì quy định, rà soát hồ sơ, clearance và đánh giá điều kiện pháp lý |
| Fleet & Route Coordinator | Tạo route version, checkpoint và phân công xe/nhân sự/ngựa/ngăn chuồng |
| Driver / Escort | Chọn trip được phân công, khởi tạo execution, ghi mốc đi/đến, vị trí và tình trạng ngựa |
| Admin | Quản trị tài khoản, vai trò và dữ liệu nền; chỉ thực hiện nghiệp vụ thay thế khi quyền demo được cấu hình phù hợp |

Một tài khoản chỉ được thao tác trên dữ liệu mà role và assignment của tài khoản cho phép. Quyền hiện có trong môi trường demo cần được xác nhận riêng; không xem việc Admin có nút bấm là bằng chứng quy trình sản xuất giao nghiệp vụ đó cho Admin.

## 3. Sáu luồng vận hành

### Flow 1 — Tạo và phê duyệt yêu cầu

1. **Customer** chọn địa điểm pickup/delivery đã được Logistics khai báo, chọn ngựa, giờ dự kiến, phương thức và ghi chú; gửi request.
2. Request chuyển `DRAFT → SUBMITTED`.
3. **Logistics Manager** nhận request để xử lý; request chuyển `UNDER_REVIEW`.
4. Manager chọn một nhánh:
   - `Approve`: tạo Order. Lý do có thể là ghi chú; không bắt buộc nếu API không quy định.
   - `Request information`: bắt buộc ghi nội dung khách cần bổ sung; request quay lại trạng thái chờ bổ sung.
   - `Reject`: bắt buộc lưu lý do từ chối.
5. Sau khi approved, Manager tạo Trip từ Order và tiếp tục sang Flow 2–3.

**Điều kiện vào:** Customer có ngựa; pickup/delivery là địa điểm hoạt động trong danh mục. Không dùng địa chỉ tìm trên OSM trực tiếp thay cho địa điểm đã lưu.

### Flow 2 — Hồ sơ, tuân thủ và thông quan

1. Compliance/Transport Specialist thiết lập quy tắc áp dụng cho tuyến/quốc gia và loại hồ sơ cần có.
2. Customer tải hồ sơ gắn với đúng ngựa, loại tài liệu và thời hạn.
3. Specialist rà soát; tài liệu được duyệt, từ chối hoặc yêu cầu bổ sung.
4. Nếu hành trình cần thông quan, người có quyền tạo clearance, gửi xét duyệt, nhận quyết định và ghi nhận hoàn tất.
5. Manager đánh giá readiness theo Trip, ngựa, quốc gia và ngày khởi hành. Thiếu, hết hạn hoặc đang rà soát phải hiển thị thành blocker cụ thể.

`Ready for departure` chỉ phản ánh kết quả kiểm tra các điều kiện đã cấu hình trong hệ thống. Nó không thay thế giấy phép/kiểm dịch hoặc xác nhận của cơ quan có thẩm quyền. Dữ liệu demo không phải tư vấn pháp lý.

### Flow 3 — Lập tuyến và phân công

1. Manager tạo Trip từ Order ở trạng thái `PLANNED`.
2. Fleet/Route Coordinator tạo Route Plan, thêm một hoặc nhiều leg, thời gian và địa điểm/checkpoint đã lưu.
3. Gán xe/phương tiện, nhân sự, ngựa và stall phù hợp; kiểm tra không xung đột lịch và sức chứa.
4. Gửi route version để duyệt. Người có quyền duyệt sau đó kích hoạt version.
5. Thay đổi route đã active phải tạo version mới; không sửa đè lịch sử version đang chạy.
6. Chỉ xác nhận Trip `READY` khi route/resource/staff/horse, compliance và các điều kiện bắt buộc đều đạt. FE cần nêu các blocker còn lại.

`READY` nghĩa là chuyến đã qua cổng chuẩn bị; chưa có nghĩa Driver đã khởi hành.

### Flow 4 — Execution, tracking và tình trạng ngựa

1. **Driver/Escort** mở **Execution**, chọn chuyến trong **My assigned trip**, tải timeline để xem đúng trip number và trạng thái Trip.
2. Bấm **Start execution** để khởi tạo bản ghi execution cho Trip đã chọn. Trip number được lấy từ lựa chọn; không nhập thêm một trip number khác.
3. Chọn điểm khởi hành trên bản đồ OSM, sau đó bấm **Record departure**. Khi tới điểm giao, chọn điểm đến và bấm **Record arrival**.
4. Bổ sung checkpoint nếu có mốc trung gian. Đánh giá tình trạng ngựa và ghi health observation theo chính sách vận hành.
5. Driver có thể nhập GPS point/ETA nếu nguồn dữ liệu hỗ trợ. Nếu người dùng chọn điểm trên OSM hoặc nhập thủ công thì đó là dữ liệu định vị thủ công, không được gọi là GPS thiết bị hoặc vị trí realtime.
6. **Customer/Manager** xem tracking và timeline. Lỗi tải timeline phải báo là lỗi tải, không được trình bày như timeline rỗng.

Trạng thái Trip và trạng thái execution là hai trạng thái khác nhau. `READY` là Trip sẵn sàng; `PLANNED` trên execution là bản ghi execution đã khởi tạo nhưng chưa có sự kiện khởi hành. Việc ghi điểm `DEPARTED`/`ARRIVED` phải tạo event có thời điểm, tọa độ và actor tương ứng.

### Flow 5 — Incident và đổi tuyến khẩn cấp

1. Driver/Escort hoặc người vận hành báo incident gắn với Trip; ghi loại, mức độ, thời điểm thực tế, mô tả và bằng chứng nếu có.
2. Hệ thống tạo incident và thông báo theo severity; người nhận xử lý/điều tra, cập nhật trạng thái và ghi kết quả.
3. Nếu cần đổi tuyến, Manager/Coordinator tạo route version đề xuất kèm lý do, ETA/chi phí ảnh hưởng và các leg/checkpoint mới.
4. Người có quyền duyệt; sau khi được duyệt mới activate version. FE hiển thị version đang active và lịch sử version cũ.

Thời điểm trong `datetime-local` là giờ địa phương của thiết bị; khi lưu cần chuyển thành timestamp UTC và khi đọc phải chuyển lại đúng giờ địa phương. Không sửa ngày để lách lỗi timezone.

### Flow 6 — Bàn giao, chấp nhận và hoàn tất

1. Sau khi Trip đến nơi, **Logistics Manager** tải Handover hiện có hoặc tạo draft nếu chưa có; không tạo bản ghi trùng cho cùng Trip.
2. Manager nhập người nhận/lịch hẹn, thêm những ngựa được giao, kiểm tra tình trạng từng con và đính kèm bằng chứng cần thiết.
3. Manager gửi Handover; trạng thái thành `PENDING_ACCEPTANCE`.
4. **Customer** xem nội dung và phản hồi cho từng ngựa: `Accept` hoặc `Dispute` kèm lý do. Không cho phản hồi lần hai trên Handover đã hoàn tất hoặc con ngựa đã có quyết định cuối.
5. Khi đã giải quyết các phản hồi và có đủ chữ ký/bằng chứng theo quy tắc, Manager bấm hoàn tất. Handover chuyển `COMPLETED`.
6. Handover phát event sang Planning; Planning cập nhật Trip `COMPLETED` rồi phát `Planning.TripCompleted`; Booking consume event để cập nhật Order `COMPLETED`.
7. Đây là đồng bộ bất đồng bộ giữa các service. FE phải đọc từng trạng thái từ service sở hữu nó và cho biết đang đồng bộ nếu Trip/Order chưa cập nhật; không suy ra Order hoàn tất chỉ từ Handover/Trip hoàn tất.

**FE hiện tại:** màn Handover đọc và hiển thị riêng trạng thái Order từ Booking, có nút refresh trong lúc event consumer đồng bộ. Chỉ kết luận Flow 6 hoàn tất khi Handover, Trip và Order lần lượt hiển thị `COMPLETED`; không suy diễn Order từ trạng thái của hai đối tượng còn lại.

## 4. Ranh giới thuật ngữ và trạng thái

- **Request:** yêu cầu của Customer; chưa phải đơn được chấp nhận.
- **Order:** nghĩa vụ vận chuyển tạo từ request đã duyệt.
- **Trip:** chuyến cụ thể được lập kế hoạch cho Order.
- **Execution:** bản ghi chạy và các event thực tế của Trip.
- **Route Plan / Version / Leg / Checkpoint:** kế hoạch tuyến có lịch sử phiên bản và điểm mốc.
- **Trip Event:** sự kiện di chuyển đã ghi nhận, không đồng nghĩa với location stream realtime.
- **Incident:** sự cố cần theo dõi và xử lý.
- **Handover:** biên bản và quyết định giao nhận theo từng ngựa.

State machine chi tiết và chủ sở hữu chuyển trạng thái nằm trong [WORKFLOW_STATE.md](WORKFLOW_STATE.md). Service sở hữu dữ liệu là nguồn sự thật của trạng thái.

## 5. Giới hạn GPS và dữ liệu thử nghiệm

MVP hỗ trợ event thủ công, điểm bản đồ, vị trí GPS do nguồn tích hợp gửi, timeline và ETA đơn giản. Chỉ tuyên bố GPS realtime khi có thiết bị/driver app gửi vị trí tự động với thời gian cập nhật được giám sát.

Địa chỉ, tọa độ, ngựa, hồ sơ, sự cố, xác nhận và chữ ký trong local demo phải được đánh dấu synthetic; không đại diện cho chuyến thật, xác nhận pháp lý hoặc chữ ký có hiệu lực.
