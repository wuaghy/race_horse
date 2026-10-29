# Workflow và state machine

Tài liệu này mô tả trạng thái nghiệp vụ và các cổng trước khi chuyển bước. API/service sở hữu entity là nguồn sự thật; giao diện không tự đặt trạng thái dựa trên một entity liên quan.

## 1. Transport Request

```text
DRAFT → SUBMITTED → UNDER_REVIEW
                       ├→ APPROVED → tạo Order
                       ├→ NEED_INFORMATION → Customer bổ sung → SUBMITTED
                       └→ REJECTED
```

- Customer tạo/sửa request ở `DRAFT`; gửi thì thành `SUBMITTED`.
- Manager nhận request thì thành `UNDER_REVIEW`.
- `Approve` cho phép ghi ghi chú tùy chọn; `Request information` và `Reject` cần reason.
- Chỉ request `APPROVED` mới được tạo Order.
- Hủy request/order phải kiểm tra cam kết vận hành liên quan.

## 2. Order

```text
CREATED → PLANNING → READY_FOR_EXECUTION → IN_PROGRESS → COMPLETED
```

Order được tạo khi request được duyệt. Booking sở hữu trạng thái Order. Khi Planning phát `Planning.TripCompleted`, Booking consume event và chuyển Order sang `COMPLETED` theo xử lý idempotent; không đổi Order đã `CANCELLED`.

Vì cập nhật đi qua event, UI phải hiển thị trạng thái Booking đọc được và nhãn đồng bộ đang chờ khi event chưa được xử lý. Trip `COMPLETED` không phải bằng chứng đủ để kết luận Order đã `COMPLETED`.

**Gap FE hiện tại:** chưa có màn/API đọc trạng thái Order theo Trip; sau Handover hoàn tất, giao diện chỉ xác nhận Handover/Trip. Không ghi nhận Order hoàn tất trên FE cho tới khi bổ sung read endpoint và hiển thị trạng thái Booking.

## 3. Trip và execution

Trip:

```text
PLANNED → READY → DEPARTED → IN_TRANSIT → ARRIVED → COMPLETED
                    │           └→ DELAYED → IN_TRANSIT
                    └→ incident / route change nếu có
```

Trip chỉ `READY` khi đạt các điều kiện chuẩn bị bắt buộc. `READY` chưa phải khởi hành.

Execution là bản ghi gắn với Trip:

```text
chưa khởi tạo → PLANNED → DEPARTED → ARRIVED
```

Driver chọn trip được phân công, tải dữ liệu trip và khởi tạo execution. Sau đó ghi sự kiện departure/arrival kèm tọa độ và thời điểm. Điểm người dùng chọn trên OSM là tọa độ nhập thủ công; chỉ thiết bị tích hợp mới là nguồn GPS tự động.

Trip Event, GPS sample, ETA và health log là dữ liệu khác nhau; không thay thế nhau.

## 4. Compliance readiness và clearance

Document:

```text
UPLOADED → UNDER_REVIEW
              ├→ APPROVED
              ├→ REJECTED
              └→ NEED_ADDITIONAL_INFO → UPLOADED
```

Clearance:

```text
DRAFT → SUBMITTED → UNDER_REVIEW
                       ├→ NEED_ADDITIONAL_INFO → SUBMITTED
                       ├→ APPROVED → COMPLETED
                       └→ REJECTED
```

Trip readiness là kết quả đánh giá tại thời điểm được gọi, không phải trạng thái hồ sơ riêng. Kết quả cần giải thích đủ/thiếu/hết hạn/đang rà soát theo từng yêu cầu. Readiness chỉ kiểm tra quy tắc đã cấu hình, không khẳng định cơ quan nhà nước đã cấp phép.

## 5. Route Plan và Version

```text
DRAFT → PENDING_APPROVAL
          ├→ REJECTED
          └→ APPROVED → ACTIVE → ARCHIVED
```

Route Plan gắn với Trip. Version `ACTIVE` không sửa trực tiếp; thay đổi phải tạo version mới có reason, submit, approve, rồi activate. Việc activate version mới archive version cũ để giữ lịch sử. Chỉ dùng route/resource phù hợp với Trip đang chuẩn bị hoặc thay đổi.

## 6. Incident và emergency route change

```text
OPEN → INVESTIGATING → RESOLVED → CLOSED
  └──────────────────→ CANCELLED
```

Incident lưu Trip, người báo, severity, occurred-at, mô tả và bằng chứng. Thời điểm nhập từ giao diện là local time được chuyển sang UTC trước khi gửi và render về local time khi đọc. Lỗi validation thời gian phải trả thông báo field-level; không đổi ngày nhập để vượt validation.

Incident không tự thay route. Route change vẫn theo quy trình version ở mục 5 và cần lý do, tác động ETA/chi phí cùng phê duyệt.

## 7. Handover và quyết định theo ngựa

```text
Handover: DRAFT → PENDING_ACCEPTANCE → ACCEPTED → COMPLETED
                                  └→ DISPUTED → Manager resolves → ACCEPTED
```

Từng horse trong Handover có quyết định riêng:

```text
PENDING → ACCEPTED
        └→ DISPUTED → Manager resolves → ACCEPTED
```

- Mỗi Trip có tối đa một Handover hiện hành; khi đã có draft thì tải và tiếp tục draft đó.
- Chỉ Manager/role được ủy quyền mới sửa người nhận, horse list, inspection và submit.
- Customer chỉ accept/dispute horse đang `PENDING` trong Handover `PENDING_ACCEPTANCE`.
- Manager ghi resolution note để giải quyết horse đang `DISPUTED`; quyết định được audit và horse chuyển `ACCEPTED`.
- Handover chỉ hoàn tất khi mọi điều kiện bắt buộc đều đạt, gồm các quyết định horse và chữ ký/bằng chứng theo cấu hình.
- Sau `COMPLETED`, không hiện lại nút Accept/Dispute/Complete và API phải từ chối transition lặp.

## 8. Chuỗi event hoàn tất xuyên service

```text
Handover.HandoverCompleted
  → Planning: Trip = COMPLETED
  → Planning.TripCompleted (outbox)
  → Booking: Order = COMPLETED
```

Consumer phải idempotent; event được xử lý lại không nhân đôi audit/outbox hoặc làm lùi trạng thái. UI đọc Handover, Trip và Order từ domain sở hữu chúng. Nếu một service chưa nhận event, hiển thị trạng thái từng domain và trạng thái đồng bộ đang chờ; không báo hoàn tất đồng nhất khi chưa có xác nhận.

## 9. Event timeline và lỗi tải

Timeline là dữ liệu tổng hợp từ event/checkpoint, audit, health và incident. Nếu một nguồn bắt buộc không tải được, UI phải báo lỗi nguồn đó. Không chuyển lỗi mạng/5xx thành danh sách rỗng, vì như vậy người dùng hiểu nhầm rằng chưa có sự kiện.

## 10. Quy tắc chuyển trạng thái và audit

State transition thuộc application/domain service sở hữu entity. Controller nhận command có ý nghĩa; không nhận status tùy ý để ghi trực tiếp.

Mọi transition quan trọng lưu:

```text
actor
entity
action
old state
new state
reason
timestamp (UTC)
correlation id
```
