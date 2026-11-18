# HƯỚNG DẪN BỘ HỒ SƠ MINH CHỨNG NỘP BÀI PRN232 (EVIDENCE-BASED SUBMISSION)

> **Dự án:** Racehorse Transport Logistics Platform  
> **Áp dụng theo:** Quy định nộp bài và đánh giá theo Evidence (`PRN232_Final_Assignment_Evidence_Based.pdf`)

---

## 1. Cấu Trúc Bộ Hồ Sơ Nộp Bài
Khi nộp bài, nén toàn bộ thư mục `PRN232_GroupXX` (hoặc đặt theo tên nhóm quy định):
```text
PRN232_GroupXX/
├── Report.pdf                       # Báo cáo tổng hợp dự án + Sơ đồ C1/C2 + Bảng Checklist công nghệ
├── SourceCode/                      # Toàn bộ source code (solution, Dockerfile, docker-compose, scripts)
└── Evidence/
    ├── 01_Workflows/                # 2–3 video quay nghiệp vụ thực tế
    │   ├── WF01_CreateBooking_Order.mp4
    │   ├── WF02_RoutePlanning_Activation.mp4
    │   └── WF03_gRPC_Compliance_PreFlightGate.mp4
    ├── 02_REST_API/                 # Minh chứng CRUD, Swagger, JWT, Paging
    │   ├── 01_Swagger_UI.png
    │   ├── 02_JWT_Login_AuthMe.png
    │   ├── 03_Paging_Search_Filter.png
    │   └── 04_REST_CRUD_Operations.png
    ├── 03_gRPC/                     # Minh chứng gRPC Server, Client, Proto, Logs
    │   ├── 01_Compliance_Proto.png
    │   ├── 02_ComplianceRpcService_Server.png
    │   ├── 03_ComplianceGrpcClient_Client.png
    │   ├── 04_gRPC_LiveExecution_Logs.png
    │   └── 05_FastReject_LocalBlocker.png
    ├── 04_MessageBroker/            # Minh chứng RabbitMQ Producer, Consumer, Outbox
    │   ├── 01_RabbitMQ_Management_Queues.png
    │   ├── 02_Outbox_Publisher_Producer.png
    │   ├── 03_Event_Consumer_Listener.png
    │   └── 04_Message_Exchange_Trace_Logs.png
    ├── 05_BackgroundJob/            # Minh chứng Background Service, Hosted Service
    │   ├── 01_OutboxDispatcher_Code.png
    │   ├── 02_OutboxDispatcher_Running_Logs.png
    │   └── 03_RabbitMqConsumer_BackgroundService.png
    └── 06_Deployment/               # Minh chứng Docker Compose, Logs Container
        ├── 01_Docker_Containers_Running.png
        ├── 02_Docker_Compose_Up_Logs.png
        └── 03_C2_Architecture_Mapping.png
```

---

## 2. Bảng Tự Đánh Giá 12 Tiêu Chí Công Nghệ (Technology Checklist)

| STT | Công nghệ / Yêu cầu | Trạng thái | Nơi triển khai trong Source Code | Đường dẫn Evidence |
|:---:|:---|:---:|:---|:---|
| **1** | **ASP.NET Core .NET 10 / Clean Architecture** | **ĐẠT** | Kiến trúc Clean Architecture tách biệt Domain, Application, Infrastructure, Api cho cả 6 microservices | `Evidence/02_REST_API/` |
| **2** | **REST API CRUD + Swagger** | **ĐẠT** | Đầy đủ Minimal APIs CRUD + Swagger OpenAPI documentation qua Gateway và từng Service | `Evidence/02_REST_API/` |
| **3** | **JWT Authentication & RBAC** | **ĐẠT** | `Identity.Api` phát hành JWT Bearer Token (Roles: `LOGISTICS_MANAGER`, `TRANSPORT_SPECIALIST`, `ADMIN`), kiểm tra qua Gateway và Middleware | `Evidence/02_REST_API/` |
| **4** | **Search / Filter / Sort / Paging** | **ĐẠT** | Master Data APIs (`/countries`, `/locations`, `/vehicles`, `/stalls`) hỗ trợ `PaginationFilter` phân trang và search text | `Evidence/02_REST_API/` |
| **5** | **EF Core + MSSQL Database** | **ĐẠT** | Entity Framework Core 10, quản lý 7 database riêng biệt trên SQL Server 2022 qua `init-sql` scripts | `Evidence/02_REST_API/` |
| **6** | **Dependency Injection + Config + Logging** | **ĐẠT** | Sử dụng IServiceCollection extensions, `appsettings.json`, environment variables, Serilog/Microsoft Logging | `Evidence/02_REST_API/` |
| **7** | **Background Job / Hosted Service** | **ĐẠT** | `OutboxDispatcherBackgroundService` quét bảng `OutboxMessages` mỗi 5s và tự động phát tán sự kiện | `Evidence/05_BackgroundJob/` |
| **8** | **Message Broker - Producer** | **ĐẠT** | `RabbitMqMessageBus` publish sự kiện (`Planning.TripCreated`, `Planning.RouteActivated`, `Booking.OrderCreated`) lên exchange `racehorse.events` | `Evidence/04_MessageBroker/` |
| **9** | **Message Broker - Consumer** | **ĐẠT** | `RabbitMqEventConsumerService` tiêu thụ sự kiện từ queue `planning-service.inbox` | `Evidence/04_MessageBroker/` |
| **10** | **gRPC - .proto + Server** | **ĐẠT** | File `compliance.proto` và service `ComplianceRpcService` lắng nghe HTTP/2 Cleartext trên port `5083` | `Evidence/03_gRPC/` |
| **11** | **gRPC - Client / REST Integration** | **ĐẠT** | `ComplianceGrpcClient` trong `Planning.Infrastructure` gọi `http://compliance:5083`, tích hợp vào REST `/trips/{id}/compliance-verification` | `Evidence/03_gRPC/` |
| **12** | **Docker + End-to-end Deployment** | **ĐẠT** | `docker-compose.yml` khởi chạy 6 microservices + Gateway + SQL Server 2022 + RabbitMQ + MinIO | `Evidence/06_Deployment/` |

---

## 3. Danh Sách Video Minh Chứng Nghiệp Vụ Cần Quay (01_Workflows)

1. **`WF01_CreateBooking_Order.mp4` (2-3 phút):**
   - Đăng nhập tài khoản Customer/Manager qua Gateway (`/api/v1/auth/login`).
   - Tạo yêu cầu vận chuyển (Transport Request) -> Tạo Transport Order.
   - Minh chứng event `Booking.OrderCreated` được bắn qua RabbitMQ.

2. **`WF02_RoutePlanning_Activation.mp4` (2-3 phút):**
   - Lập kế hoạch lộ trình: Tạo Route Plan -> Thêm Route Leg (Origin -> Destination) -> Gán xe, tài xế, áp tải viên, ngựa đua.
   - Thực hiện quy trình duyệt phiên bản lộ trình: `Draft` -> `Submit` -> `Approve` -> `Activate`.
   - Minh chứng sinh sự kiện `Planning.RouteActivated`.

3. **`WF03_gRPC_Compliance_PreFlightGate.mp4` (3 phút) - *ĐIỂM SÁNG KỸ THUẬT*:**
   - **Thao tác 1 (Fast-Reject):** Gọi kiểm tra compliance trên Trip chưa có lộ trình -> Phản hồi lập tức mã lỗi và danh sách lý do cục bộ trong `< 1ms`, không bắn gRPC rác.
   - **Thao tác 2 (Live gRPC Call):** Gọi kiểm tra compliance trên Trip đã active lộ trình -> Mở log 2 màn hình cho thấy Planning gửi request qua port `5083` gRPC sang Compliance, Compliance truy vấn cơ sở dữ liệu luật kiểm dịch và trả lời HTTP 200 OK kèm `x-correlation-id` phân tán.
   - **Thao tác 3 (Pre-Flight Safety Gate):** Bấm xác nhận xuất phát (`confirm-ready`) khi chưa đủ giấy tờ kiểm dịch -> Hệ thống chặn xuất phát với mã lỗi `HTTP 422 Unprocessable Entity` và thông báo `"Compliance chưa ready"`.
