# Cross-Border Racehorse Transport System — Team Engineering Docs

Bộ tài liệu triển khai cho hệ thống **Quản lý Vận chuyển Ngựa đua Xuyên Quốc gia**.

## 1. Công nghệ chuẩn

### Backend

- .NET 10 / ASP.NET Core Web API
- C#
- Entity Framework Core
- SQL Server
- JWT Bearer Authentication
- YARP API Gateway
- RabbitMQ cho integration events
- Docker / Docker Compose
- OpenAPI / Swagger cho API contract
- Object storage S3-compatible cho file/document

### Frontend

- React Native + TypeScript
- React Navigation
- TanStack Query cho server state
- Secure storage cho token
- Native push notification khi triển khai notification

### Kiến trúc

- Microservices
- Database per service
- API Gateway
- Event-driven integration
- Vertical-slice feature delivery

.NET 10 là LTS và hiện được Microsoft hỗ trợ đến tháng 11/2028. Xem `docs/ARCHITECTURE.md` để biết các quyết định công nghệ và nguồn chính thức.

## 2. Nguyên tắc bắt buộc

1. Một microservice sở hữu dữ liệu của chính nó. Service khác không truy cập database của service đó.
2. Không tạo foreign key xuyên database/service.
3. ID của entity thuộc service khác được lưu dưới dạng UUID tham chiếu logic.
4. API Gateway là entry point của React Native.
5. Service vẫn tự validate JWT và authorization; không coi Gateway là security boundary duy nhất.
6. Status nghiệp vụ quan trọng phải thay đổi qua command endpoint, không cho frontend PATCH trực tiếp status.
7. Integration giữa service dùng event khi phù hợp; không tạo chuỗi synchronous calls dài.
8. Task nghiệp vụ phải là vertical slice: Backend + Frontend + màn hình chạy được end-to-end qua Gateway.
9. Mọi response phải tuân `docs/API_STANDARD.md`.
10. Mọi state transition phải tuân `docs/WORKFLOW_STATE.md`.
11. Mọi thay đổi schema phải tuân database ownership trong `docs/DATABASE.md`.
12. Không đưa domain entity vào `BuildingBlocks`.

## 3. Thứ tự đọc

1. `docs/PRODUCT_SCOPE.md`
2. `docs/ARCHITECTURE.md`
3. `docs/DATABASE.md`
4. `docs/API_STANDARD.md`
5. `docs/INTEGRATION_EVENTS.md`
6. `docs/WORKFLOW_STATE.md`
7. `docs/BACKEND_STRUCTURE.md`
8. `docs/FRONTEND_STRUCTURE.md`
9. `docs/TASK_PLAN.md`
10. `docs/DEFINITION_OF_DONE.md`
11. `docs/LOCAL_DEVELOPMENT.md`
12. `docs/SECURITY.md`
13. `docs/TESTING.md`

## 4. Entry condition trước khi nhận task nghiệp vụ

Member phải đọc:

- API standard
- state machine của flow mình làm
- database ownership của service
- task dependencies
- Definition of Done

Nếu cần thay đổi contract, không tự ý đổi code. Tạo change request/PR cập nhật contract trước.

## 5. Quy ước branch

```text
main
  └── develop
       ├── feature/T01-auth-login
       ├── feature/T02-horse-management
       ├── feature/T03-transport-request
       └── ...
```

Commit type:

```text
feat
fix
refactor
perf
test
chore
docs
style
ci
```

Ví dụ:

```text
feat(booking): add transport request submission
feat(tracking): add horse health log screen
fix(compliance): prevent expired document approval
```

## 6. Definition of success của project

Một flow chỉ được xem là hoàn thành khi có thể chạy:

```text
React Native Screen
    ↓
Gateway
    ↓
Owning Microservice
    ↓
Own Database
    ↓
Event (nếu cần)
    ↓
Other Service(s)
    ↓
Updated UI
```

Không chấp nhận trạng thái "API xong, FE làm sau" đối với task nghiệp vụ.
