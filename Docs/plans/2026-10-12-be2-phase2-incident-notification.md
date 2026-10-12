# BE2 Phase 2 — Incident & Notification Services Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement Backend Engineer 2 (BE2) Phase 2 responsibilities: **T15 — Incident Management Service** (lifecycle, severity, attachments, outbox events) and **T19 — Notification Service** (idempotent inbox consumer, device token management, notification center API) with YARP Gateway routing and automated test suites.

**Architecture:** Clean Architecture (`Domain`, `Application`, `Infrastructure`, `Api`) per service, conforming to the PRN232 .NET 10 LTS microservice blueprint. Incident Service publishes `Incident.IncidentCreated` and `Incident.IncidentResolved` via Transactional Outbox. Notification Service consumes cross-domain events (`Booking.*`, `Compliance.*`, `Tracking.*`, `Incident.*`) via RabbitMQ topic exchange with an idempotent `InboxMessages` dedup store.

**Tech Stack:** .NET 10 (C# 13), ASP.NET Core Minimal APIs, Entity Framework Core 10 (SQL Server & InMemory provider), RabbitMQ.Client 7.x, FluentValidation, YARP Reverse Proxy, xUnit, FluentAssertions.

---

## Plan Overview & Task Breakdown

### Phase 2.1: Incident Management Service (T15)
- **Task 1:** Integration Event Contracts (`Contracts.IntegrationEvents/IncidentEvents.cs`)
- **Task 2:** Domain Entities (`Incident.Domain`)
- **Task 3:** Application Layer: DTOs, Validators & Interfaces (`Incident.Application`)
- **Task 4:** Infrastructure Layer: EF Core DbContext, Local File Storage & Outbox Dispatcher (`Incident.Infrastructure`)
- **Task 5:** Application Services Implementation (`Incident.Infrastructure/Services`)
- **Task 6:** API Minimal Endpoints & Program Configuration (`Incident.Api`)
- **Task 7:** Comprehensive Unit & Integration Tests (`Incident.Tests`)

### Phase 2.2: Notification Service (T19)
- **Task 8:** Domain Entities (`Notification.Domain`)
- **Task 9:** Application Layer: DTOs & Interfaces (`Notification.Application`)
- **Task 10:** Infrastructure Layer: EF Core DbContext & Inbox Idempotency (`Notification.Infrastructure`)
- **Task 11:** RabbitMQ Consumer Background Worker (`Notification.Infrastructure/Messaging`)
- **Task 12:** Application Services Implementation (`Notification.Infrastructure/Services`)
- **Task 13:** API Minimal Endpoints & Program Configuration (`Notification.Api`)
- **Task 14:** Comprehensive Unit Tests (`Notification.Tests`)

### Phase 2.3: API Gateway & Solution Integration
- **Task 15:** Gateway Routing & Cluster Configuration (`Gateway.Api/appsettings.json`)
- **Task 16:** Solution Build, Verification & Test Execution

---

## Detailed Tasks

### Task 1: Integration Event Contracts (`Contracts.IntegrationEvents`)

**Files:**
- Create: `backend/Contracts/IntegrationEvents/IncidentEvents.cs`

**Step 1: Write integration event contracts**
Create records inheriting from `IntegrationEvent<TData>`:
- `IncidentCreatedData`: `IncidentId`, `IncidentNo`, `TripId`, `HorseId`, `Type`, `Severity`, `Title`, `OccurredAt`, `ReportedByUserId`.
- `IncidentCreatedEvent`: `EventType => "Incident.IncidentCreated"`, `Source => "incident-service"`.
- `IncidentResolvedData`: `IncidentId`, `IncidentNo`, `TripId`, `ResolvedAt`, `ResolutionNote`, `ResolvedByUserId`.
- `IncidentResolvedEvent`: `EventType => "Incident.IncidentResolved"`, `Source => "incident-service"`.

**Step 2: Verify compilation**
Run: `dotnet build backend/Contracts/IntegrationEvents/Contracts.IntegrationEvents.csproj`
Expected: Build succeeded with 0 errors.

---

### Task 2: Domain Entities (`Incident.Domain`)

**Files:**
- Create: `backend/Services/Incident/Incident.Domain/Entities/Incident.cs`
- Create: `backend/Services/Incident/Incident.Domain/Entities/IncidentAttachment.cs`
- Create: `backend/Services/Incident/Incident.Domain/Entities/OutboxMessage.cs`
- Create: `backend/Services/Incident/Incident.Domain/Entities/AuditLog.cs`

**Step 1: Write domain entities matching `scripts/init-sql/06_incident_db.sql`**
- `Incident`:
  - `Id` (Guid), `IncidentNo` (string, unique), `TripId` (Guid), `RouteLegId` (Guid?), `CheckpointId` (Guid?), `HorseId` (Guid?).
  - `ReportedByUserId` (Guid), `Type` (`VEHICLE_BREAKDOWN`, `TRAFFIC_DELAY`, `WEATHER`, `HORSE_MEDICAL`, `BORDER_ISSUE`, `OTHER`).
  - `Severity` (`LOW`, `MEDIUM`, `HIGH`, `CRITICAL`).
  - `Status` (`OPEN`, `INVESTIGATING`, `RESOLVED`, `CANCELLED`, `CLOSED`).
  - `Latitude`, `Longitude` (decimal?), `Title` (string), `Description` (string).
  - `OccurredAt` (DateTime), `ResolvedAt` (DateTime?), `ResolutionNote` (string?).
  - `CreatedAt`, `UpdatedAt` (DateTime), `VersionNo` (int).
  - Navigation: `ICollection<IncidentAttachment> Attachments`.
- `IncidentAttachment`:
  - `Id`, `IncidentId`, `FileName`, `FileUrl`, `MimeType`, `UploadedByUserId`, `UploadedAt`.
- `OutboxMessage`:
  - `Id`, `EventType`, `AggregateType`, `AggregateId`, `Payload`, `CorrelationId`, `OccurredAt`, `Status` (`PENDING`, `PROCESSED`, `FAILED`), `RetryCount`, `ProcessedAt`, `CreatedAt`.
- `AuditLog`:
  - `Id`, `EntityType`, `EntityId`, `Action`, `OldState`, `NewState`, `ActorUserId`, `Reason`, `CorrelationId`, `OccurredAt`, `CreatedAt`.

**Step 2: Verify compilation**
Run: `dotnet build backend/Services/Incident/Incident.Domain/Incident.Domain.csproj`
Expected: Build succeeded with 0 errors.

---

### Task 3: Application DTOs, Validators & Interfaces (`Incident.Application`)

**Files:**
- Create: `backend/Services/Incident/Incident.Application/Abstractions/IIncidentDbContext.cs`
- Create: `backend/Services/Incident/Incident.Application/Abstractions/IFileStorageService.cs`
- Create: `backend/Services/Incident/Incident.Application/Abstractions/IMessageBus.cs`
- Create: `backend/Services/Incident/Incident.Application/DTOs/IncidentDtos.cs`
- Create: `backend/Services/Incident/Incident.Application/Validators/IncidentValidators.cs`
- Create: `backend/Services/Incident/Incident.Application/Services/IIncidentService.cs`

**Step 1: Write application interfaces, DTOs, and Validators**
- DTOs:
  - `CreateIncidentRequest`: `TripId`, `RouteLegId?`, `CheckpointId?`, `HorseId?`, `Type`, `Severity`, `Title`, `Description`, `OccurredAt`, `Latitude?`, `Longitude?`.
  - `UpdateIncidentStatusRequest`: `NewStatus`, `Reason?`.
  - `ResolveIncidentRequest`: `ResolutionNote`.
  - `IncidentDto`: Full details including attachment list.
  - `IncidentSummaryDto`: Compact listing representation.
  - `IncidentFilterDto`: Filters by `TripId`, `HorseId`, `Severity`, `Status`, Date range, Page, PageSize.
  - `IncidentAttachmentDto`: Attachment details.
- `IIncidentService`:
  - `Task<IncidentDto> CreateIncidentAsync(CreateIncidentRequest request, Guid reportedByUserId, CancellationToken ct)`
  - `Task<PagedResult<IncidentSummaryDto>> GetIncidentsAsync(IncidentFilterDto filter, CancellationToken ct)`
  - `Task<IncidentDto?> GetIncidentByIdAsync(Guid id, CancellationToken ct)`
  - `Task<IncidentDto> UpdateStatusAsync(Guid id, UpdateIncidentStatusRequest request, Guid actorUserId, CancellationToken ct)`
  - `Task<IncidentDto> ResolveIncidentAsync(Guid id, ResolveIncidentRequest request, Guid actorUserId, CancellationToken ct)`
  - `Task<IncidentAttachmentDto> AddAttachmentAsync(Guid incidentId, string fileName, string contentType, Stream fileStream, Guid uploadedByUserId, CancellationToken ct)`

**Step 2: Verify compilation**
Run: `dotnet build backend/Services/Incident/Incident.Application/Incident.Application.csproj`
Expected: Build succeeded with 0 errors.

---

### Task 4: Infrastructure Persistence, Storage & Outbox Dispatcher (`Incident.Infrastructure`)

**Files:**
- Modify: `backend/Services/Incident/Incident.Infrastructure/Incident.Infrastructure.csproj` (add `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.InMemory`, `RabbitMQ.Client`)
- Create: `backend/Services/Incident/Incident.Infrastructure/Persistence/IncidentDbContext.cs`
- Create: `backend/Services/Incident/Incident.Infrastructure/Persistence/DatabaseInitializer.cs`
- Create: `backend/Services/Incident/Incident.Infrastructure/Storage/LocalFileStorageService.cs`
- Create: `backend/Services/Incident/Incident.Infrastructure/Messaging/RabbitMqOptions.cs`
- Create: `backend/Services/Incident/Incident.Infrastructure/Messaging/RabbitMqMessageBus.cs`
- Create: `backend/Services/Incident/Incident.Infrastructure/Messaging/IncidentOutboxDispatcher.cs`
- Create: `backend/Services/Incident/Incident.Infrastructure/DependencyInjection.cs`

**Step 1: Configure EF Core mappings & Outbox publisher**
- Map Fluent API for tables `Incidents`, `IncidentAttachments`, `OutboxMessages`, `AuditLogs`.
- Include seed data helper in `DatabaseInitializer.cs`.
- Background worker `IncidentOutboxDispatcher` to poll `OutboxMessages` with status `PENDING` and publish to RabbitMQ exchange `racehorse.events`.

**Step 2: Verify compilation**
Run: `dotnet build backend/Services/Incident/Incident.Infrastructure/Incident.Infrastructure.csproj`
Expected: Build succeeded with 0 errors.

---

### Task 5: Incident Service Business Logic Implementation

**Files:**
- Create: `backend/Services/Incident/Incident.Infrastructure/Services/IncidentService.cs`

**Step 1: Write business logic**
- Incident generation with auto-generated formatted `IncidentNo` (`INC-yyyyMMdd-XXXX`).
- Validate valid status transitions (`OPEN` -> `INVESTIGATING` -> `RESOLVED`/`CANCELLED` -> `CLOSED`).
- On Create: insert audit log + enqueue `Incident.IncidentCreated` into `OutboxMessages`.
- On Resolve: update `ResolvedAt`, `ResolutionNote`, insert audit log + enqueue `Incident.IncidentResolved` into `OutboxMessages`.
- Handle file attachment uploads through `IFileStorageService`.

**Step 2: Verify compilation**
Run: `dotnet build backend/Services/Incident/Incident.Infrastructure/Incident.Infrastructure.csproj`
Expected: Build succeeded with 0 errors.

---

### Task 6: API Minimal Endpoints & Configuration (`Incident.Api`)

**Files:**
- Modify: `backend/Services/Incident/Incident.Api/Incident.Api.csproj` (add `Microsoft.AspNetCore.Authentication.JwtBearer`, `Swashbuckle.AspNetCore`)
- Create: `backend/Services/Incident/Incident.Api/Endpoints/IncidentEndpoints.cs`
- Modify: `backend/Services/Incident/Incident.Api/Program.cs`
- Modify: `backend/Services/Incident/Incident.Api/appsettings.json`

**Step 1: Implement endpoints**
- `POST /api/v1/incidents` — Create incident.
- `GET /api/v1/incidents` — List/filter incidents (paged).
- `GET /api/v1/incidents/{id}` — Get detail by ID.
- `PATCH /api/v1/incidents/{id}/status` — Transition status (`INVESTIGATING`, `CANCELLED`, `CLOSED`).
- `POST /api/v1/incidents/{id}/resolve` — Resolve incident with resolution note.
- `POST /api/v1/incidents/{id}/attachments` — Upload multipart file attachment.
- `GET /api/v1/incidents/trips/{tripId}` — Shortcut to get all incidents for a specific trip.

**Step 2: Verify compilation & startup**
Run: `dotnet build backend/Services/Incident/Incident.Api/Incident.Api.csproj`
Expected: Build succeeded with 0 errors.

---

### Task 7: Comprehensive Incident Unit & Integration Tests (`Incident.Tests`)

**Files:**
- Create: `backend/Services/Incident/Incident.Tests/Incident.Tests.csproj`
- Create: `backend/Services/Incident/Incident.Tests/TestDbContextFactory.cs`
- Create: `backend/Services/Incident/Incident.Tests/IncidentServiceTests.cs`
- Modify: `RacehorseTransport.sln` (add `Incident.Tests` project)

**Step 1: Write tests**
- `CreateIncidentAsync_WithValidData_CreatesIncidentAndOutboxMessage`
- `CreateIncidentAsync_WithInvalidSeverity_ThrowsValidationException`
- `UpdateStatusAsync_ValidTransition_UpdatesStatusAndAuditLog`
- `UpdateStatusAsync_InvalidTransition_ThrowsBusinessRuleException`
- `ResolveIncidentAsync_SetsResolvedAtAndResolutionNote_AndEnqueuesOutboxEvent`
- `AddAttachmentAsync_ValidFile_SavesAttachmentMetadata`
- `GetIncidentsAsync_FilteringBySeverityAndStatus_ReturnsFilteredList`

**Step 2: Run test suite**
Run: `dotnet test backend/Services/Incident/Incident.Tests/Incident.Tests.csproj`
Expected: All tests PASS.

---

### Task 8: Domain Entities (`Notification.Domain`)

**Files:**
- Create: `backend/Services/Notification/Notification.Domain/Entities/Notification.cs`
- Create: `backend/Services/Notification/Notification.Domain/Entities/DeviceToken.cs`
- Create: `backend/Services/Notification/Notification.Domain/Entities/InboxMessage.cs`

**Step 1: Write domain entities matching `scripts/init-sql/08_notification_db.sql`**
- `Notification`:
  - `Id`, `RecipientUserId`, `Type` (`REQUEST_APPROVED`, `DOCUMENT_NEED_INFO`, `TRIP_DEPARTED`, `HEALTH_ALERT`, `INCIDENT`, `ROUTE_CHANGED`, `HANDOVER_READY`).
  - `Title`, `Message`, `ReferenceType`, `ReferenceId`, `Status` (`PENDING`, `SENT`, `READ`, `FAILED`), `ReadAt`, `CreatedAt`.
- `DeviceToken`:
  - `Id`, `UserId`, `Platform` (`IOS`, `ANDROID`), `Token`, `Active`, `CreatedAt`, `UpdatedAt`.
- `InboxMessage`:
  - `Id`, `EventId` (unique constraint), `EventType`, `ProcessedAt`.

**Step 2: Verify compilation**
Run: `dotnet build backend/Services/Notification/Notification.Domain/Notification.Domain.csproj`
Expected: Build succeeded with 0 errors.

---

### Task 9: Application Layer DTOs & Interfaces (`Notification.Application`)

**Files:**
- Create: `backend/Services/Notification/Notification.Application/Abstractions/INotificationDbContext.cs`
- Create: `backend/Services/Notification/Notification.Application/DTOs/NotificationDtos.cs`
- Create: `backend/Services/Notification/Notification.Application/Services/INotificationService.cs`

**Step 1: Write application interfaces and DTOs**
- `NotificationDto`, `NotificationFilterDto`, `RegisterDeviceTokenRequest`, `UnreadCountDto`.
- `INotificationService`:
  - `Task<PagedResult<NotificationDto>> GetUserNotificationsAsync(Guid userId, bool? unreadOnly, int page, int pageSize, CancellationToken ct)`
  - `Task<UnreadCountDto> GetUnreadCountAsync(Guid userId, CancellationToken ct)`
  - `Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId, CancellationToken ct)`
  - `Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken ct)`
  - `Task RegisterDeviceTokenAsync(Guid userId, RegisterDeviceTokenRequest request, CancellationToken ct)`
  - `Task UnregisterDeviceTokenAsync(Guid userId, string token, CancellationToken ct)`
  - `Task CreateNotificationAsync(Guid recipientUserId, string type, string title, string message, string? refType, Guid? refId, CancellationToken ct)`

**Step 2: Verify compilation**
Run: `dotnet build backend/Services/Notification/Notification.Application/Notification.Application.csproj`
Expected: Build succeeded with 0 errors.

---

### Task 10: Infrastructure Layer Persistence & Inbox Idempotency (`Notification.Infrastructure`)

**Files:**
- Modify: `backend/Services/Notification/Notification.Infrastructure/Notification.Infrastructure.csproj` (add EF Core & RabbitMQ)
- Create: `backend/Services/Notification/Notification.Infrastructure/Persistence/NotificationDbContext.cs`
- Create: `backend/Services/Notification/Notification.Infrastructure/Persistence/DatabaseInitializer.cs`
- Create: `backend/Services/Notification/Notification.Infrastructure/DependencyInjection.cs`

**Step 1: EF Core configuration**
- Configure table mappings for `Notifications`, `DeviceTokens`, `InboxMessages`.
- Add unique constraint on `InboxMessages.EventId` for idempotency protection.

**Step 2: Verify compilation**
Run: `dotnet build backend/Services/Notification/Notification.Infrastructure/Notification.Infrastructure.csproj`
Expected: Build succeeded with 0 errors.

---

### Task 11: RabbitMQ Consumer Background Worker (`Notification.Infrastructure/Messaging`)

**Files:**
- Create: `backend/Services/Notification/Notification.Infrastructure/Messaging/RabbitMqOptions.cs`
- Create: `backend/Services/Notification/Notification.Infrastructure/Messaging/NotificationEventConsumer.cs`

**Step 1: Implement consumer**
- Bind queue `notification.service.queue` to exchange `racehorse.events` with topic patterns:
  - `Booking.*`
  - `Compliance.*`
  - `Tracking.*`
  - `Incident.*`
- For incoming message:
  1. Inspect `eventId`. Check if already present in `InboxMessages`. If so, ACK and ignore (idempotent duplicate drop).
  2. Map domain event to user notifications:
     - `Tracking.HealthAlertRaised` -> Alert to Veterinarian / Driver / Coordinator.
     - `Incident.IncidentCreated` -> Alert to Coordinator / Manager / Driver.
     - `Incident.IncidentResolved` -> Alert to Coordinator / Customer.
     - `Compliance.ComplianceReady` -> Alert to Trip Planner / Coordinator.
     - `Booking.RequestApproved` -> Alert to Customer.
  3. Save notification & record `InboxMessage` in a single database transaction.
  4. ACK RabbitMQ message.

**Step 2: Verify compilation**
Run: `dotnet build backend/Services/Notification/Notification.Infrastructure/Notification.Infrastructure.csproj`
Expected: Build succeeded with 0 errors.

---

### Task 12: Notification Application Services Implementation

**Files:**
- Create: `backend/Services/Notification/Notification.Infrastructure/Services/NotificationService.cs`

**Step 1: Implement user notification querying, token management & read flags**
- Paginated listing with read/unread filtering.
- Device token upsert (deactivate old token on same device, activate new token).
- Bulk and individual mark-as-read.

**Step 2: Verify compilation**
Run: `dotnet build backend/Services/Notification/Notification.Infrastructure/Notification.Infrastructure.csproj`
Expected: Build succeeded with 0 errors.

---

### Task 13: Notification API Minimal Endpoints & Program (`Notification.Api`)

**Files:**
- Modify: `backend/Services/Notification/Notification.Api/Notification.Api.csproj` (add `Microsoft.AspNetCore.Authentication.JwtBearer`, `Swashbuckle.AspNetCore`)
- Create: `backend/Services/Notification/Notification.Api/Endpoints/NotificationEndpoints.cs`
- Modify: `backend/Services/Notification/Notification.Api/Program.cs`
- Modify: `backend/Services/Notification/Notification.Api/appsettings.json`

**Step 1: Implement endpoints**
- `GET /api/v1/notifications` — Get current user notifications (query: `unreadOnly`, `page`, `pageSize`).
- `GET /api/v1/notifications/unread-count` — Get unread notification badge count.
- `PATCH /api/v1/notifications/{id}/read` — Mark single notification as read.
- `POST /api/v1/notifications/read-all` — Mark all user notifications as read.
- `POST /api/v1/notifications/device-tokens` — Register mobile push token.
- `DELETE /api/v1/notifications/device-tokens/{token}` — Unregister token on logout.

**Step 2: Verify compilation**
Run: `dotnet build backend/Services/Notification/Notification.Api/Notification.Api.csproj`
Expected: Build succeeded with 0 errors.

---

### Task 14: Notification Unit Tests (`Notification.Tests`)

**Files:**
- Create: `backend/Services/Notification/Notification.Tests/Notification.Tests.csproj`
- Create: `backend/Services/Notification/Notification.Tests/TestDbContextFactory.cs`
- Create: `backend/Services/Notification/Notification.Tests/NotificationServiceTests.cs`
- Create: `backend/Services/Notification/Notification.Tests/NotificationConsumerTests.cs`
- Modify: `RacehorseTransport.sln` (add `Notification.Tests` project)

**Step 1: Write test cases**
- `GetUserNotificationsAsync_ReturnsPagedUserNotifications`
- `MarkAsReadAsync_UpdatesStatusAndReadAt`
- `MarkAllAsReadAsync_MarksAllUnreadToRead`
- `RegisterDeviceToken_UpsertsTokenRecord`
- `Consumer_DuplicateEventId_DropsDuplicateWithoutCreatingNotification`
- `Consumer_HealthAlertRaised_CreatesNotificationForAssignedRoles`
- `Consumer_IncidentCreated_CreatesNotificationForCoordinator`

**Step 2: Run test suite**
Run: `dotnet test backend/Services/Notification/Notification.Tests/Notification.Tests.csproj`
Expected: All tests PASS.

---

### Task 15: API Gateway YARP Routing Update (`Gateway.Api`)

**Files:**
- Modify: `backend/Gateway/Gateway.Api/appsettings.json`

**Step 1: Add Incident and Notification routes and clusters**
- Route `incident`: `/api/v1/incidents/{**catch-all}` -> Cluster `incident` (port `5181`)
- Route `notification`: `/api/v1/notifications/{**catch-all}` -> Cluster `notification` (port `5153`)

**Step 2: Verify Gateway compilation**
Run: `dotnet build backend/Gateway/Gateway.Api/Gateway.Api.csproj`
Expected: Build succeeded with 0 errors.

---

### Task 16: Solution-Wide Verification & Test Execution

**Step 1: Build entire solution**
Run: `dotnet build RacehorseTransport.sln`
Expected: 0 Warnings, 0 Errors.

**Step 2: Run all test suites in the solution**
Run: `dotnet test RacehorseTransport.sln`
Expected: All tests in `Compliance.Tests`, `Incident.Tests`, `Notification.Tests` (and others) PASS.
