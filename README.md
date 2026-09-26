# Cross-Border Racehorse Transport System

Hệ thống Quản lý Vận chuyển Ngựa đua Xuyên Quốc gia (PRN232 - Microservices Architecture).

## 1. Công nghệ sử dụng

- **Backend**: .NET 10 / ASP.NET Core Web API (C#)
- **Kiến trúc**: Microservices (Clean Architecture: Domain, Application, Infrastructure, Api)
- **API Gateway**: YARP (Yet Another Reverse Proxy)
- **Message Broker**: RabbitMQ (Integration Events qua Outbox / Inbox Pattern)
- **Database**: SQL Server (Database-per-service: 8 cơ sở dữ liệu riêng biệt)
- **Object Storage**: S3-compatible (MinIO / AWS S3)
- **Client**: React Native (TypeScript)

## 2. Ranh giới Microservices

Hệ thống được module hóa thành 8 microservices độc lập:

1. **Identity Service** (`IdentityDb`): Xác thực người dùng, phân quyền RBAC (6 Roles), JWT token rotation, AuditLog.
2. **Booking Service** (`BookingDb`): Quản lý khách hàng, hồ sơ ngựa, yêu cầu vận chuyển (`TransportRequest`), đơn hàng (`TransportOrder`).
3. **Compliance Service** (`ComplianceDb`): Quy định kiểm dịch quốc tế, thẩm định giấy tờ y tế/hộ chiếu, hồ sơ thông quan (`ClearanceCase`).
4. **Planning Service** (`PlanningDb`): Lập kế hoạch chuyến đi (`TransportTrip`), lộ trình đa phiên bản (`RoutePlans` & `RoutePlanVersions`), phân bổ xe tải, khoang máy bay, chuồng (`Stall`) và nhân sự.
5. **Tracking Service** (`TrackingDb`): Mốc hành trình (`TripEvents`), tọa độ GPS (`TripLocations`), nhật ký sức khỏe định kỳ (`HorseHealthLogs`).
6. **Incident Service** (`IncidentDb`): Ghi nhận và xử lý sự cố khẩn cấp (`Incidents`), ảnh bằng chứng hiện trường.
7. **Handover Service** (`HandoverDb`): Biên bản nghiệm thu bàn giao (`HandoverRecords`), nghiệm thu thể trạng từng con ngựa, chữ ký số điện tử, chi phí & doanh thu chuyến đi.
8. **Notification Service** (`NotificationDb`): Trung tâm thông báo, push notification token, cơ chế Idempotent Consumer (`InboxMessages`).

## 3. Khởi chạy nhanh (Local Development)

### Bước 1: Khởi động Hạ tầng Docker (SQL Server, RabbitMQ, MinIO)
```bash
# Bật các container hạ tầng
docker compose up -d
```
Quá trình khởi tạo sẽ tự động:
- Khởi chạy SQL Server 2022 và tự động chạy script tạo đủ 8 database cùng schema DDL.
- Khởi chạy RabbitMQ (Cổng AMQP: `5672`, Management UI: `http://localhost:15672`).
- Khởi chạy MinIO (API: `9000`, Web Console: `http://localhost:9001`) và tạo sẵn 4 buckets: `documents`, `health-images`, `incident-attachments`, `signatures`.

### Bước 2: Build Solution .NET
```bash
# Build toàn bộ 35 projects
dotnet build RacehorseTransport.sln
```

## 4. Cấu trúc thư mục

```text
├── Docs/                              # Tài liệu phân tích nghiệp vụ, ERD và DBML
├── scripts/
│   └── init-sql/                      # Script T-SQL tự động khởi tạo 8 databases
├── backend/
│   ├── Directory.Build.props          # Cấu hình chung .NET 10 cho toàn bộ project
│   ├── BuildingBlocks/                # Thư viện dùng chung (Response envelope, Exceptions, Middlewares)
│   ├── Contracts/                     # Envelope Integration Events dùng chung
│   ├── Gateway/                       # Gateway YARP
│   └── Services/                      # 8 Microservices (Clean Architecture)
├── docker-compose.yml
├── .env.example
└── RacehorseTransport.sln
```
