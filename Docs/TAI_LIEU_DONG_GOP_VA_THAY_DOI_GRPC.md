# TÀI LIỆU TỔNG HỢP CÁC NỘI DUNG MỚI VÀ CHỈNH SỬA (gRPC & PRE-FLIGHT GATE)

> **Dành cho:** Thành viên nhóm và Báo cáo đóng góp đồ án PRN232  
> **Dự án:** Racehorse Transport Logistics Platform  
> **Nhánh Git:** `feature/grpc-compliance-verification`  
> **Trạng thái kiểm thử:** 68/68 tests passed (100% Green), Docker 6 Microservices chạy ổn định.

---

## 1. TỔNG QUAN NỘI DUNG ĐÃ THỰC HIỆN

Trong đợt cập nhật này, nhánh `feature/grpc-compliance-verification` tập trung giải quyết toàn diện tính năng **Giao tiếp liên dịch vụ qua gRPC tốc độ cao** giữa **Planning Service** và **Compliance Service**, đóng góp vào các trọng số chấm điểm quan trọng của đồ án PRN232 (gRPC 15%, Architecture & Design 20%, Docker/Deployment 10%, Background Job 10%, Message Broker 15%):

1. **Hiện thực hóa gRPC Pre-Flight Gate (SPEC-001 & SPEC-002):**
   - Khởi tạo hợp đồng Protocol Buffers (`compliance.proto`) chuẩn hóa kiểm tra tuân thủ pháp lý theo thời gian thực (Point-in-Time).
   - Triển khai gRPC Server phía Compliance Service và gRPC Client phía Planning Service.
   - Tích hợp cổng kiểm soát an toàn trước xuất phát (**Pre-Flight Safety Gate - ADR-0001**): Ngăn chặn tuyệt đối việc xác nhận xuất phát (`confirm-ready`) nếu chưa vượt qua kiểm định tuân thủ kiểm dịch.
2. **Kiến trúc Dual-Port Kestrel cho Cleartext gRPC (HTTP/2 `h2c`):**
   - Giải quyết triệt để xung đột giao thức của ASP.NET Core Kestrel khi chạy không có TLS trong môi trường nội bộ Docker container (Port 8083 chuyên dụng cho HTTP/1.1 REST, Port 5083 chuyên dụng cho cleartext HTTP/2 gRPC).
3. **Phân phối Distributed Tracing (Correlation ID):**
   - Đính kèm và truyền tải `x-correlation-id` xuyên suốt từ HTTP Gateway -> Planning REST -> Planning gRPC Client Metadata -> Compliance gRPC Server -> AuditLog & Outbox Event.
4. **Cơ chế Fast-Reject Cục Bộ (SPEC-002 L21):**
   - Tự động phát hiện và chặn các chuyến đi chưa có lộ trình hoặc chưa gán ngựa ngay tại tầng Application của Planning, không phát sinh dữ liệu rác hoặc gọi gRPC không cần thiết qua mạng.
5. **Đồng bộ cơ sở dữ liệu và khóa ngoại EF Core:**
   - Bổ sung cấu hình cột `Payload` cho bảng `AuditLogs` trên SQL Server.
   - Khai báo quan hệ khóa ngoại giữa `RoutePlan` và `RoutePlanVersion` giúp EF Core sắp xếp đúng thứ tự insert, loại bỏ lỗi khóa ngoại SQL Server.

---

## 2. BẢNG CHI TIẾT CÁC FILE MỚI ĐƯỢC TẠO VÀ FILE ĐÃ SỬA

### A. Danh sách các File Mới Tạo (11 Files)

| STT | File / Đường dẫn | Thư mục & Module | Mục đích & Mô tả chi tiết |
|:---:|:---|:---|:---|
| 1 | [compliance.proto](file:///c:/1.University/C#/race_horse/backend/Contracts/Protos/compliance.proto) | `backend/Contracts/Protos/` | File định nghĩa Protocol Buffer gRPC (Service `ComplianceRpc`, Request `VerifyTripComplianceRequest`, Response `VerifyTripComplianceResponse`, CheckItems). |
| 2 | [ComplianceRpcService.cs](file:///c:/1.University/C#/race_horse/backend/Services/Compliance/Compliance.Api/Services/ComplianceRpcService.cs) | `Services/Compliance/Compliance.Api/Services/` | Triển khai gRPC Server thừa kế từ `ComplianceRpc.ComplianceRpcBase`, tiếp nhận request, trích xuất correlation ID, gọi `IComplianceReadinessService` kiểm tra luật kiểm dịch và trả về kết quả. |
| 3 | [ComplianceRpcServiceTests.cs](file:///c:/1.University/C#/race_horse/backend/Services/Compliance/Compliance.Tests/ComplianceRpcServiceTests.cs) | `Services/Compliance/Compliance.Tests/` | Unit tests kiểm thử độc lập gRPC Server: test compliant (đạt), non-compliant (thiếu giấy tờ, hết hạn, pending review). |
| 4 | [IComplianceVerificationClient.cs](file:///c:/1.University/C#/race_horse/backend/Services/Planning/Planning.Application/Abstractions/IComplianceVerificationClient.cs) | `Services/Planning/Planning.Application/Abstractions/` | Interface trừu tượng hóa client gRPC theo nguyên tắc Dependency Inversion của Clean Architecture. |
| 5 | [ComplianceTripQuery.cs](file:///c:/1.University/C#/race_horse/backend/Services/Planning/Planning.Application/DTOs/Readiness/ComplianceTripQuery.cs) | `Services/Planning/Planning.Application/DTOs/Readiness/` | DTO chứa thông tin truy vấn tuân thủ (TripId, OriginCountryId, DestinationCountryId, HorseIds, PlannedDepartureDate). |
| 6 | [TripComplianceVerificationDto.cs](file:///c:/1.University/C#/race_horse/backend/Services/Planning/Planning.Application/DTOs/Readiness/TripComplianceVerificationDto.cs) | `Services/Planning/Planning.Application/DTOs/Readiness/` | DTO kết quả xác minh trả về cho client/REST API (IsCompliant, TotalRequired, TotalMet, BlockerMessages, CheckItems). |
| 7 | [ComplianceGrpcClient.cs](file:///c:/1.University/C#/race_horse/backend/Services/Planning/Planning.Infrastructure/Clients/ComplianceGrpcClient.cs) | `Services/Planning/Planning.Infrastructure/Clients/` | Triển khai gRPC Client, đóng gói cuộc gọi RPC, tự động đính kèm metadata `x-correlation-id`, cấu hình deadline timeout 5s. |
| 8 | [ComplianceGrpcClientTests.cs](file:///c:/1.University/C#/race_horse/backend/Services/Planning/Planning.Tests/ComplianceGrpcClientTests.cs) | `Services/Planning/Planning.Tests/` | Unit test cho gRPC Client sử dụng mock CallInvoker để kiểm tra mapping request, response và xử lý ngoại lệ. |
| 9 | [SPEC-001-grpc-compliance-verification.md](file:///c:/1.University/C#/race_horse/Docs/specs/SPEC-001-grpc-compliance-verification.md) | `Docs/specs/` | Đặc tả kỹ thuật ban đầu cho tính năng tích hợp gRPC Pre-Departure Verification. |
| 10 | [SPEC-002-grpc-compliance-refinement-and-standards.md](file:///c:/1.University/C#/race_horse/Docs/specs/SPEC-002-grpc-compliance-refinement-and-standards.md) | `Docs/specs/` | Đặc tả tinh chỉnh chuẩn hóa Clean Architecture, CQRS Query và loại bỏ code trùng lặp. |
| 11 | [0001 & 0002 ADRs](file:///c:/1.University/C#/race_horse/Docs/adr/0001-dual-mode-consistency-grpc-preflight-gate.md) | `Docs/adr/` | 2 Architecture Decision Records: ADR-0001 (Pre-Flight Safety Gate) và ADR-0002 (Stateless gRPC Client Adapter). |

---

### B. Danh sách các File Đã Chỉnh Sửa (19 Files)

| STT | File / Đường dẫn | Thay đổi cốt lõi & Lý do chỉnh sửa |
|:---:|:---|:---|
| 1 | [docker-compose.yml](file:///c:/1.University/C#/race_horse/docker-compose.yml) | Cấu hình cổng cho `compliance-api`: expose `8083` (REST) và `5083` (gRPC). Đặt `GrpcServices__ComplianceUrl: "http://compliance:5083"` cho `planning-api`. Đặt `ASPNETCORE_HTTP_PORTS: ""` để tránh đè cấu hình Kestrel. |
| 2 | [Compliance.Api/Dockerfile](file:///c:/1.University/C#/race_horse/backend/Services/Compliance/Compliance.Api/Dockerfile) | `EXPOSE 8083 5083` và đặt `ENV ASPNETCORE_HTTP_PORTS=""` để image base .NET 10 không chiếm quyền cấu hình cổng của Kestrel. |
| 3 | [Compliance.Api/appsettings.json](file:///c:/1.University/C#/race_horse/backend/Services/Compliance/Compliance.Api/appsettings.json) | Cấu hình phân tách Kestrel Endpoints: `Http` (`http://*:8083`, Protocols: `Http1`) và `Grpc` (`http://*:5083`, Protocols: `Http2`). |
| 4 | [Compliance.Api/Program.cs](file:///c:/1.University/C#/race_horse/backend/Services/Compliance/Compliance.Api/Program.cs) | Đăng ký `builder.Services.AddGrpc()`, `AddHttpContextAccessor()`, ánh xạ endpoint `app.MapGrpcService<ComplianceRpcService>()`. |
| 5 | [Compliance.Api.csproj](file:///c:/1.University/C#/race_horse/backend/Services/Compliance/Compliance.Api/Compliance.Api.csproj) | Tham chiếu Protobuf `compliance.proto` với thuộc tính `GrpcServices="Server"`. |
| 6 | [Planning.Infrastructure.csproj](file:///c:/1.University/C#/race_horse/backend/Services/Planning/Planning.Infrastructure/Planning.Infrastructure.csproj) | Tham chiếu Protobuf `compliance.proto` với thuộc tính `GrpcServices="Client"`, cài đặt thư viện `Grpc.Net.ClientFactory`. |
| 7 | [Planning.Infrastructure/Persistence/ServiceCollectionExtensions.cs](file:///c:/1.University/C#/race_horse/backend/Services/Planning/Planning.Infrastructure/Persistence/ServiceCollectionExtensions.cs) | Bổ sung extension `AddPlanningGrpcClients` đăng ký `AddGrpcClient<ComplianceRpcClient>` trỏ đến `http://compliance:5083`. |
| 8 | [Planning.Infrastructure/Persistence/PlanningDbContext.cs](file:///c:/1.University/C#/race_horse/backend/Services/Planning/Planning.Infrastructure/Persistence/PlanningDbContext.cs) | Khai báo quan hệ khóa ngoại giữa `RoutePlan` và `RoutePlanVersion`, cấu hình cột `Payload` cho entity `AuditLog`. |
| 9 | [Planning.Api/Program.cs](file:///c:/1.University/C#/race_horse/backend/Services/Planning/Planning.Api/Program.cs) | Đăng ký `AddHttpContextAccessor()` và `builder.Services.AddPlanningGrpcClients(builder.Configuration)`. |
| 10 | [Planning.Api/appsettings.json](file:///c:/1.University/C#/race_horse/backend/Services/Planning/Planning.Api/appsettings.json) | Cập nhật cấu hình mặc định `"ComplianceUrl": "http://localhost:5083"`. |
| 11 | [PlanningCommandEndpoints.cs](file:///c:/1.University/C#/race_horse/backend/Services/Planning/Planning.Api/PlanningCommandEndpoints.cs) | Bổ sung endpoint `GET /api/v1/trips/{id}/compliance-verification` giao phó trực tiếp cho `ITripReadinessService.VerifyTripComplianceAsync`. |
| 12 | [ITripReadinessService.cs](file:///c:/1.University/C#/race_horse/backend/Services/Planning/Planning.Application/Services/ITripReadinessService.cs) | Thêm phương thức `VerifyTripComplianceAsync(Guid tripId, CancellationToken ct)`. |
| 13 | [TripReadinessService.cs](file:///c:/1.University/C#/race_horse/backend/Services/Planning/Planning.Application/Services/TripReadinessService.cs) | Triển khai Fast-Reject cục bộ, loại bỏ code trùng lặp phân giải Country/Horse, lưu `CorrelationId` nhận từ HTTP request vào `AuditLog` và `OutboxMessage`. |
| 14 | [TripPlanningService.cs](file:///c:/1.University/C#/race_horse/backend/Services/Planning/Planning.Application/Services/TripPlanningService.cs) | Lưu `RoutePlan` trước khi lưu `RoutePlanVersion` để đảm bảo thứ tự khóa ngoại SQL Server. |
| 15 | [PlanningEntities.cs](file:///c:/1.University/C#/race_horse/backend/Services/Planning/Planning.Domain/PlanningEntities.cs) | Bổ sung thuộc tính `public string? Payload { get; set; }` vào entity `AuditLog` để lưu snapshot kiểm tra tuân thủ. |
| 16 | [TripReadinessServiceTests.cs](file:///c:/1.University/C#/race_horse/backend/Services/Planning/Planning.Tests/TripReadinessServiceTests.cs) | Thêm các bài test: fast-reject khi thiếu route/horse, dispatch gRPC khi route hợp lệ, graceful degradation khi gRPC lỗi, ghi audit log snapshot. |
| 17 | [04_planning_db.sql](file:///c:/1.University/C#/race_horse/scripts/init-sql/04_planning_db.sql) | Thêm cột `Payload NVARCHAR(MAX) NULL` vào bảng `AuditLogs` của database `PlanningDb`. |
| 18 | [.env & .env.example](file:///c:/1.University/C#/race_horse/.env.example) | Khởi tạo cấu hình biến môi trường chuẩn mẫu, đặc biệt là `JWT_SIGNING_KEY` 256-bit. |
| 19 | [GLOSSARY.md](file:///c:/1.University/C#/race_horse/GLOSSARY.md) | Cập nhật định nghĩa thuật ngữ kiến trúc: Pre-Flight Gate, Compliance Clearance, Dual-Mode Consistency, Stateless Protocol Adapter. |

---

## 3. KIẾN TRÚC KỸ THUẬT & VẤN ĐỀ ĐÃ XỬ LÝ (HIGHLIGHTS)

### 1. Kiến trúc Dual-Port Kestrel cho Cleartext gRPC
- **Vấn đề thực tế:** Khi chạy microservice trong Docker không dùng HTTPS/TLS, Kestrel tuân thủ nghiêm ngặt RFC 7540: **không cho phép chạy chung HTTP/1.1 và cleartext HTTP/2 (`h2c`) trên cùng một cổng**. Khi Planning gửi HTTP/2 frame sang cổng 8083 (đang phục vụ REST), kết nối bị ngắt với mã lỗi `HTTP_1_1_REQUIRED (0xd)`.
- **Giải pháp:** Cấu hình Kestrel mở **2 cổng độc lập**:
  - **Port 8083 (HTTP/1.1):** Phục vụ các endpoint REST API Minimal APIs, Swagger UI và Gateway YARP proxy.
  - **Port 5083 (HTTP/2 Cleartext):** Phục vụ riêng các cuộc gọi RPC nội bộ từ các microservices khác (`http://compliance:5083`).

### 2. Mô hình Nhất Quán Kép (Dual-Mode Consistency) & Pre-Flight Gate (ADR-0001)
- **Chế độ Bất đồng bộ (RabbitMQ):** Khi giấy tờ kiểm dịch được cấp duyệt ở Compliance Service, hệ thống phát tán event `Compliance.ComplianceReady` qua RabbitMQ để cập nhật cờ `trip.ComplianceReady = true` (Eventual Consistency).
- **Chế độ Đồng bộ Thời gian thực (gRPC Pre-Flight Gate):** Ngay trước khi xuất phát, điều phối viên gọi `POST /api/v1/trips/{id}/confirm-ready`. Planning Service gọi tức thì gRPC sang Compliance để tái thẩm định giấy tờ ngay tại thời điểm hiện tại (Point-in-Time). Nếu có giấy phép bị hết hạn trong thời gian chờ hoặc thiếu điều kiện -> Chặn đứng xuất phát với mã lỗi `HTTP 422 Unprocessable Entity`!

### 3. Phân phối Distributed Tracing (Correlation ID)
- Toàn bộ luồng từ Client gọi Gateway -> Planning API -> Metadata gRPC `x-correlation-id` -> Compliance Server -> Outbox Message -> Audit Log đều mang chung một mã `CorrelationId`. Giúp việc soi log trên Docker hoặc các công cụ APM (Jaeger, Seq, ELK) cực kỳ chính xác và rõ ràng.

---

## 4. HƯỚNG DẪN BẠN BÈ TỰ CHẠY VÀ KIỂM TRA (DEMO SCRIPT)

### Bước 1: Checkout branch và chạy Unit Tests
```bash
git checkout feature/grpc-compliance-verification
dotnet test backend/RacehorseTransport.sln
# Kết quả: 68/68 tests passed (Planning: 29/29, Compliance: 25/25, Incident: 7/7, Notification: 7/7)
```

### Bước 2: Khởi động hệ thống Docker Compose
```bash
docker compose up -d --build
# Kiểm tra trạng thái:
docker compose ps
# Tất cả 6 microservices + Gateway + SQL Server + RabbitMQ + MinIO đều Up và Healthy
```

### Bước 3: Chạy kịch bản Demo tự động bằng PowerShell
Mở PowerShell tại thư mục gốc của project và chạy đoạn script sau:

```powershell
# 1. Đăng nhập lấy JWT Token qua Gateway
$loginBody = @{ email = "manager.demo@racehorse.local"; password = "HorseDev2026!" } | ConvertTo-Json
$loginRes = Invoke-RestMethod -Uri "http://localhost:8080/api/v1/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
$token = $loginRes.data.accessToken
$headers = @{ "Authorization" = "Bearer $token"; "Content-Type" = "application/json" }
Write-Host "Đăng nhập thành công!" -ForegroundColor Green

# 2. Tạo một chuyến đi mới (Trip)
$tripRes = (Invoke-RestMethod -Uri "http://localhost:8080/api/v1/trips" -Method Post -Headers $headers -Body (@{ orderId = [Guid]::NewGuid().ToString() } | ConvertTo-Json)).data
$tripId = $tripRes.id
Write-Host "Tạo Trip thành công: $tripId" -ForegroundColor Cyan

# 3. Test Fast-Reject khi chưa có lộ trình
$fastReject = Invoke-RestMethod -Uri "http://localhost:8080/api/v1/trips/$tripId/compliance-verification" -Method Get -Headers $headers
Write-Host "Fast-Reject trả về trong <1ms:" -ForegroundColor Yellow
$fastReject.data.blockerMessages

# 4. Thử bấm Xác nhận xuất phát khi chưa đủ điều kiện -> Phải bị chặn với HTTP 422!
try {
    Invoke-RestMethod -Uri "http://localhost:8080/api/v1/trips/$tripId/confirm-ready" -Method Post -Headers $headers
} catch {
    $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
    Write-Host "Cổng Pre-Flight Gate CHẶN THÀNH CÔNG (HTTP 422):" -ForegroundColor Red
    Write-Host $reader.ReadToEnd()
}
```

### Bước 4: Soi log minh chứng gRPC trên Docker
```bash
# Xem log phía gRPC Client (Planning):
docker compose logs --tail 20 planning-api

# Xem log phía gRPC Server (Compliance):
docker compose logs --tail 20 compliance-api
```
Log sẽ hiển thị rõ request `VerifyTripCompliance` được gửi qua `http://compliance:5083` và trả về mã `200` với đầy đủ Correlation ID phân tán!

---

## 5. ĐÁNH GIÁ THƯ MỤC NỘP BÀI `PRN232_Evidence_Based_Submission_Package`

### A. Hiện trạng trước kiểm tra
- Thư mục ban đầu chỉ chứa 2 file đề bài:
  - `PRN232_Final_Assignment_Evidence_Based.pdf`: Quy chế nộp bài và hướng dẫn chuẩn bị minh chứng.
  - `PRN232_Checklist_CongNghe_DongGop.xlsx`: Template bảng phân công tỷ lệ thành viên và checklist công nghệ.

### B. Những gì cần bổ sung để "ĐỦ" nộp bài theo quy chế PDF
Để hồ sơ hoàn chỉnh và đạt điểm tối đa (A+), cần hoàn thiện các mục sau:
1. **Thư mục Evidence cấu trúc chuẩn (ĐÃ TẠO SẴN):**
   - `Evidence/01_Workflows/`: Cần quay 2-3 clip ngắn (`.mp4`) mô phỏng 3 luồng: Đặt đơn (`WF01`), Lập lộ trình (`WF02`), và Gọi gRPC Pre-Flight Gate (`WF03`).
   - `Evidence/02_REST_API/`: Chụp ảnh Swagger UI, JWT Login, phân trang Master data.
   - `Evidence/03_gRPC/`: Chụp ảnh file `.proto`, gRPC Server `ComplianceRpcService`, gRPC Client `ComplianceGrpcClient`, log gọi gRPC live.
   - `Evidence/04_MessageBroker/`: Chụp ảnh RabbitMQ UI (các exchange `racehorse.events`, queue `planning-service.inbox`), code Outbox và log consumer.
   - `Evidence/05_BackgroundJob/`: Chụp ảnh code `OutboxDispatcherBackgroundService` và log service quét bản tin định kỳ.
   - `Evidence/06_Deployment/`: Chụp ảnh lệnh `docker compose ps` các container đang chạy và sơ đồ C2 Container.
2. **File `Report.pdf`:**
   - Dự thảo báo cáo gồm 5 phần theo mẫu trang 1 và 2 của PDF, trong đó điền bảng **Technology Checklist (PRN232) 12/12 tiêu chí đều ĐẠT** và bảng **Tỷ lệ đóng góp thành viên** từ file Excel.
