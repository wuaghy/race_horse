# Racehorse Transport System — Database Design (bản hoàn chỉnh)

Nguồn: `racehorse-transport-team-docs.zip` (đặc biệt là `docs/PRODUCT_SCOPE.md`,
`docs/DATABASE.md`, `docs/WORKFLOW_STATE.md`, `docs/INTEGRATION_EVENTS.md`,
`docs/ARCHITECTURE.md`, `database/racehorse_microservices.dbml`) và mục
"Cross-Border Racehorse Transport System" (HoangNT20) trong `Project_Ideas_FA26.xlsx`.

DBML gốc trong repo đã khá đầy đủ về mặt entity nghiệp vụ. Phần này giữ nguyên
toàn bộ thiết kế gốc và **bổ sung những gì tài liệu yêu cầu nhưng schema gốc
chưa có**, để có một bộ DB thực sự triển khai được (chạy migration, pass code
review) chứ không chỉ là ERD minh họa.

## 1. Những gì đã đủ trong bản gốc (giữ nguyên)

- 8 database theo đúng service boundary trong `ARCHITECTURE.md` §2–3.
- Không có FK xuyên service; ID logic (Horse, Trip, User…) chỉ là UUID tham chiếu.
- 6 flow nghiệp vụ trong `PRODUCT_SCOPE.md` đều map được vào bảng:
  Request → Order → Trip → RoutePlan/Version → RouteLeg → Checkpoint → TripEvent
  → Incident → HandoverRecord.
- Versioning cho RoutePlan (mỗi lần đổi lộ trình khẩn cấp = version mới, không
  sửa version cũ) — đúng yêu cầu "Route version is immutable after activation".

## 2. Gap đã phát hiện và cách bổ sung

| Gap | Tài liệu yêu cầu | Đã thêm |
|---|---|---|
| Không có bảng Audit Log | `WORKFLOW_STATE.md` §10: mọi transition quan trọng phải log actor/entity/action/old-new state/reason/correlationId | Thêm `AuditLogs` vào Identity, Booking, Compliance, Planning, Incident, Handover (6 service có state machine thật sự; Tracking/Notification chỉ ghi log append-only nên không cần) |
| Không có Outbox pattern | `INTEGRATION_EVENTS.md` §5: "Do not claim an event was published simply because a DB save succeeded" | Thêm `OutboxMessages` vào 6 service **producer** sự kiện (Booking, Compliance, Planning, Tracking, Incident, Handover) |
| Không có cơ chế idempotent consumer | `ARCHITECTURE.md` §8: "idempotent event handlers" | Thêm `InboxMessages` vào NotificationDb (service tiêu thụ gần như mọi event) — unique trên `EventId` để loại message trùng do RabbitMQ at-least-once |
| "Chỉ 1 RoutePlanVersion ACTIVE tại 1 thời điểm" chưa được ép ở DB | `DATABASE.md` §5 | Filtered unique index: `CREATE UNIQUE INDEX ... ON RoutePlanVersions(RoutePlanId) WHERE Status='ACTIVE'` |
| Thiếu VersionNo (optimistic concurrency) ở vài bảng bị nhiều actor sửa | `DATABASE.md` §7: "Entities frequently updated by multiple actors should have VersionNo" | Thêm `VersionNo` vào `ClearanceCases` (specialist + cơ quan xét duyệt), `RoutePlans` (con trỏ ActiveVersionId), `Incidents` (người báo cáo + người xử lý), `HandoverRecords` (driver tạo + manager hoàn tất) |
| Không có cột mật khẩu/rotation cho login thật | `SECURITY.md` §1 nói tới refresh-token rotation | Thêm `Users.PasswordHash`, `RefreshTokens.DeviceInfo`, `RefreshTokens.ReplacedByTokenHash` |
| Kiểu dữ liệu text dùng `varchar` chung chung | Toàn bộ nghiệp vụ có tên người/ngựa/địa chỉ tiếng Việt | Trong DDL T-SQL, tách rõ: `NVARCHAR` cho các trường có thể chứa tiếng Việt/Unicode (tên, ghi chú, địa chỉ...), `VARCHAR` cho mã, email, URL, enum — tránh lỗi hiển thị `???` khi lưu tiếng Việt |
| Không có constraint chống double-booking | `DATABASE.md` §5: Vehicle/Stall "cannot be double-booked for overlapping intervals" | SQL Server không có kiểu range-exclusion constraint như PostgreSQL `EXCLUDE USING gist`; ghi rõ trong comment DDL: phải kiểm tra overlap trong transaction `SERIALIZABLE` ở tầng command handler của Planning service trước khi insert `TripLegAssignments` / `HorseLegAssignments` / `StaffAssignments` |
| PK dùng `NEWID()` | — | Đổi sang `NEWSEQUENTIALID()` cho PK UNIQUEIDENTIFIER — giảm phân mảnh clustered index khi insert nhiều, tốt hơn cho production SQL Server |
| Vài CHECK constraint còn thiếu | — | Thêm CHECK cho: khoảng ngày hợp lệ (`RequestedArrivalAt > RequestedDepartureAt`, `EffectiveTo > EffectiveFrom`), sức chứa dương (`HorseCapacity > 0`), ngày sinh không ở tương lai, số tiền `>= 0` |

## 3. File đính kèm

```
db_design/
├── racehorse_microservices_complete.dbml   # ERD tổng hợp — import thẳng vào dbdiagram.io
├── DATABASE_DESIGN_NOTES.md                # file này
└── sql/
    ├── 01_identity_db.sql
    ├── 02_booking_db.sql
    ├── 03_compliance_db.sql
    ├── 04_planning_db.sql        # DB lớn nhất — Route/Vehicle/Stall/Assignment
    ├── 05_tracking_db.sql
    ├── 06_incident_db.sql
    ├── 07_handover_db.sql
    └── 08_notification_db.sql
```

Mỗi file `.sql` là T-SQL (SQL Server) chạy độc lập cho đúng 1 database —
khớp với nguyên tắc "mỗi service sở hữu 1 database, migration không được đụng
sang service khác" (`DATABASE.md` §8). Có thể convert 1-1 sang EF Core
`Migrations` bằng cách map từng `CREATE TABLE` thành 1 entity + `Fluent API`
tương ứng.

## 4. Lưu ý khi hiện thực

- **VersionNo là cột nghiệp vụ song song với optimistic concurrency token**,
  ngoại trừ `RoutePlanVersions.VersionNo` — cột đó là **số thứ tự version
  nghiệp vụ** (v1, v2, v3...), không phải concurrency token; nếu cần khóa
  đồng thời cho việc approve/activate version, dùng thêm `ROWVERSION` (SQL
  Server timestamp) hoặc kiểm tra `Status` hiện tại trong transaction.
- **Xung đột (409)**: mọi UPDATE có `VersionNo` phải kèm `WHERE VersionNo = @old`
  trong câu lệnh, nếu số dòng ảnh hưởng = 0 thì trả `COMMON_CONCURRENCY_CONFLICT`
  đúng như `DATABASE.md` §7 quy định.
- **Outbox publisher**: cần 1 background worker (hoặc `IHostedService` trong
  .NET) polling `OutboxMessages WHERE Status = 'PENDING'`, publish lên RabbitMQ,
  rồi update `Status = 'PUBLISHED'`. Không publish trực tiếp trong request thread.
- **Seed data**: chỉ seed dữ liệu master ổn định (Roles, DocumentTypes,
  Countries) — đúng `DATABASE.md` §8 "Seed only stable reference/master data".
