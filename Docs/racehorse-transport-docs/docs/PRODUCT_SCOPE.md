# Product Scope

## 1. Mục tiêu

Hệ thống quản lý toàn bộ vòng đời vận chuyển ngựa đua trong nước và xuyên quốc gia:

```text
Transport Request
  → Approval
  → Legal / Quarantine
  → Route & Resource Planning
  → Trip Execution
  → Incident / Emergency Change
  → Handover
  → Completion & Reporting
```

## 2. Actors

| Actor | Mục đích |
|---|---|
| Customer | Tạo yêu cầu, quản lý ngựa, upload hồ sơ, theo dõi chuyến, nhận bàn giao |
| Logistics Manager | Phê duyệt request, điều hành trip, route change, KPI |
| Transport Specialist | Quy định, hồ sơ, kiểm dịch, customs/clearance |
| Fleet & Route Coordinator | Phương tiện, route, checkpoint, assignment |
| Driver / Escort | Chạy chuyến, checkpoint event, GPS, health log, incident |
| Admin | Quản trị account, role, master data và vận hành hệ thống |

## 3. Sáu flow

### Flow 1 — Tạo & phê duyệt yêu cầu

Customer tạo request → Manager review → approve/request-information/reject → approved request tạo order.

### Flow 2 — Pháp lý / kiểm dịch / thông quan

Regulation → required documents → upload → review → clearance → compliance readiness.

### Flow 3 — Lộ trình / phương tiện

Order → Trip → Route Plan/Version → Route Legs → Checkpoints → Vehicle/Flight/Staff/Stall assignment → Trip Ready.

### Flow 4 — Thực thi / tracking

Driver/Escort tạo milestones → GPS → ETA → timeline → horse health logs → customer tracking.

### Flow 5 — Incident / emergency route change

Incident → severity/notification → proposed route version → approval → apply → updated ETA/cost.

### Flow 6 — Handover / acceptance

Arrived → handover → horse inspection → accept/dispute → signature → handover completed → trip/order completed.

## 4. Ranh giới thuật ngữ

### Request

Yêu cầu do Customer gửi. Chưa phải chuyến thực tế.

### Order

Request đã được duyệt và trở thành nghĩa vụ vận hành.

### Trip

Một chuyến vận chuyển cụ thể được điều phối và thực thi.

### Route Plan

Kế hoạch tuyến của Trip. Có version để không sửa mất lịch sử.

### Route Leg

Một chặng trong route.

### Checkpoint

Một điểm mốc của Route Leg.

### Trip Event

Một sự kiện thực tế xảy ra trong chuyến.

### Incident

Một sự cố cần được xử lý hoặc theo dõi.

### Handover

Biên bản bàn giao cuối chuyến.

## 5. Scope GPS

MVP hỗ trợ:

- manual milestone
- current GPS location
- trip timeline
- ETA đơn giản

GPS thật cần thiết bị/driver app gửi location point. Nếu chưa có GPS source thì không gọi tính năng đó là "real-time GPS".

## 6. Scope optional

Flow 5 và Flow 6 vẫn phải có trong design. Nếu cần cắt MVP, có thể triển khai Flow 5 sau Flow 4 và Flow 6 sau khi Flow 4 đạt ổn định.
