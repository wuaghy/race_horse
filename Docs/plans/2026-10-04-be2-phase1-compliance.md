# BE2 Phase 1 — Compliance Service Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement BE2's Phase 1 services (T06: Compliance Master Data & Regulations, T07: Horse/Trip Documents & Storage, T08: Clearance & Compliance Readiness Engine) with Outbox Integration Event `Compliance.ComplianceReady` to unblock BE3's `TripReady` workflow.

**Architecture:** Clean Architecture (`Compliance.Domain`, `Compliance.Application`, `Compliance.Infrastructure`, `Compliance.Api`) with EF Core for `ComplianceDb` (SQL Server), MinIO/S3 file storage, Transactional Outbox pattern, and RabbitMQ topic publisher for `Compliance.ComplianceReady` and `Compliance.ClearanceApproved`.

**Tech Stack:** .NET 10, C# 13, EF Core 10 (SQL Server & In-Memory fallback), RabbitMQ.Client 7.x, MinIO / Local file storage, ASP.NET Core Minimal APIs / Endpoints with JWT Bearer auth and YARP Gateway routing.

---

### Task 1: Integration Event Contracts (`Contracts.IntegrationEvents`)

**Files:**
- Create: `backend/Contracts/IntegrationEvents/ComplianceEvents.cs`

**Step 1: Write the integration event contract**
Define `ComplianceComplianceReadyEvent` and `ComplianceClearanceApprovedEvent` inheriting from `IntegrationEvent<TData>` with topic routing keys `"Compliance.ComplianceReady"` and `"Compliance.ClearanceApproved"`.

**Step 2: Verify compilation**
Run: `dotnet build backend/Contracts/IntegrationEvents/Contracts.IntegrationEvents.csproj`
Expected: Build succeeded with 0 errors.

---

### Task 2: Domain Entities (`Compliance.Domain`)

**Files:**
- Create: `backend/Services/Compliance/Compliance.Domain/Entities/DocumentType.cs`
- Create: `backend/Services/Compliance/Compliance.Domain/Entities/Document.cs`
- Create: `backend/Services/Compliance/Compliance.Domain/Entities/DocumentReview.cs`
- Create: `backend/Services/Compliance/Compliance.Domain/Entities/RegulationRule.cs`
- Create: `backend/Services/Compliance/Compliance.Domain/Entities/RegulationDocumentRequirement.cs`
- Create: `backend/Services/Compliance/Compliance.Domain/Entities/ClearanceCase.cs`
- Create: `backend/Services/Compliance/Compliance.Domain/Entities/ClearanceCaseDocument.cs`
- Create: `backend/Services/Compliance/Compliance.Domain/Entities/OutboxMessage.cs`
- Create: `backend/Services/Compliance/Compliance.Domain/Entities/AuditLog.cs`

**Step 1: Implement domain entities matching `scripts/init-sql/03_compliance_db.sql`**
Ensure types, enums, nullability, and invariants:
- Scope: `HORSE`, `REQUEST`, `TRIP`
- Document Status: `UPLOADED`, `UNDER_REVIEW`, `APPROVED`, `REJECTED`, `NEED_ADDITIONAL_INFO`
- Clearance Status: `DRAFT`, `SUBMITTED`, `UNDER_REVIEW`, `NEED_ADDITIONAL_INFO`, `APPROVED`, `REJECTED`, `COMPLETED`
- AuditLog for status transitions.

**Step 2: Verify compilation**
Run: `dotnet build backend/Services/Compliance/Compliance.Domain/Compliance.Domain.csproj`
Expected: Build succeeded with 0 errors.

---

### Task 3: Application DTOs & Service Abstractions (`Compliance.Application`)

**Files:**
- Create: `backend/Services/Compliance/Compliance.Application/Abstractions/IComplianceDbContext.cs`
- Create: `backend/Services/Compliance/Compliance.Application/Abstractions/IFileStorageService.cs`
- Create: `backend/Services/Compliance/Compliance.Application/DTOs/MasterData/DocumentTypeDtos.cs`
- Create: `backend/Services/Compliance/Compliance.Application/DTOs/MasterData/RegulationRuleDtos.cs`
- Create: `backend/Services/Compliance/Compliance.Application/DTOs/Documents/DocumentDtos.cs`
- Create: `backend/Services/Compliance/Compliance.Application/DTOs/Clearance/ClearanceCaseDtos.cs`
- Create: `backend/Services/Compliance/Compliance.Application/DTOs/Readiness/ComplianceReadinessDtos.cs`
- Create: `backend/Services/Compliance/Compliance.Application/Services/IComplianceMasterDataService.cs`
- Create: `backend/Services/Compliance/Compliance.Application/Services/IDocumentService.cs`
- Create: `backend/Services/Compliance/Compliance.Application/Services/IClearanceService.cs`
- Create: `backend/Services/Compliance/Compliance.Application/Services/IComplianceReadinessService.cs`

**Step 1: Write application interfaces and DTOs**
Implement contracts for CRUD operations, document reviews, clearance management, and automated readiness evaluation.

**Step 2: Verify compilation**
Run: `dotnet build backend/Services/Compliance/Compliance.Application/Compliance.Application.csproj`
Expected: Build succeeded with 0 errors.

---

### Task 4: Infrastructure Persistence & Storage (`Compliance.Infrastructure`)

**Files:**
- Modify: `backend/Services/Compliance/Compliance.Infrastructure/Compliance.Infrastructure.csproj` (add EF Core & RabbitMQ)
- Create: `backend/Services/Compliance/Compliance.Infrastructure/Persistence/ComplianceDbContext.cs`
- Create: `backend/Services/Compliance/Compliance.Infrastructure/Persistence/DatabaseInitializer.cs`
- Create: `backend/Services/Compliance/Compliance.Infrastructure/Storage/LocalFileStorageService.cs`
- Create: `backend/Services/Compliance/Compliance.Infrastructure/Messaging/ComplianceOutboxPublisher.cs`
- Create: `backend/Services/Compliance/Compliance.Infrastructure/Services/ComplianceMasterDataService.cs`
- Create: `backend/Services/Compliance/Compliance.Infrastructure/Services/DocumentService.cs`
- Create: `backend/Services/Compliance/Compliance.Infrastructure/Services/ClearanceService.cs`
- Create: `backend/Services/Compliance/Compliance.Infrastructure/Services/ComplianceReadinessService.cs`
- Create: `backend/Services/Compliance/Compliance.Infrastructure/DependencyInjection.cs`

**Step 1: Add package dependencies**
Add `Microsoft.EntityFrameworkCore.SqlServer` (10.0.12) and `RabbitMQ.Client` (7.2.2).

**Step 2: Implement EF Core DbContext**
Configure Fluent API mappings matching `ComplianceDb` tables, keys, constraints, and indexes.

**Step 3: Implement Services and Outbox Dispatcher**
- Document uploading with file validation and storage.
- Document review state transitions with immutable `DocumentReviews` audit trail.
- Clearance workflow.
- Compliance Readiness Engine: Evaluate required documents for a trip and horses across Origin & Destination country regulations. If fully satisfied, trigger `Compliance.ComplianceReady` into `OutboxMessages`.
- Background Outbox dispatcher to publish messages to RabbitMQ topic exchange.

**Step 4: Verify compilation**
Run: `dotnet build backend/Services/Compliance/Compliance.Infrastructure/Compliance.Infrastructure.csproj`
Expected: Build succeeded with 0 errors.

---

### Task 5: API Endpoints & Wiring (`Compliance.Api`)

**Files:**
- Modify: `backend/Services/Compliance/Compliance.Api/Compliance.Api.csproj`
- Modify: `backend/Services/Compliance/Compliance.Api/Program.cs`
- Create: `backend/Services/Compliance/Compliance.Api/Endpoints/ComplianceMasterDataEndpoints.cs`
- Create: `backend/Services/Compliance/Compliance.Api/Endpoints/DocumentEndpoints.cs`
- Create: `backend/Services/Compliance/Compliance.Api/Endpoints/ClearanceEndpoints.cs`
- Create: `backend/Services/Compliance/Compliance.Api/Endpoints/ReadinessEndpoints.cs`

**Step 1: Configure Program.cs**
- Add JWT Bearer authentication matching Identity (`Jwt:SigningKey`, `Jwt:Issuer`, `Jwt:Audience`).
- Add Swagger/OpenAPI with Bearer token authentication.
- Register `ComplianceInfrastructure`.
- Add DB initialization and health endpoint.

**Step 2: Map API Endpoints**
- Master data: Document Types, Regulation Rules & Requirements.
- Documents: Upload, List, Get by Id, Review (Approve/Reject/Need Info).
- Clearance: Create case, Submit case, Review case.
- Readiness: Check trip readiness, Trigger automated readiness evaluation.

**Step 3: Verify compilation**
Run: `dotnet build backend/Services/Compliance/Compliance.Api/Compliance.Api.csproj`
Expected: Build succeeded with 0 errors.

---

### Task 6: Unit & Integration Tests (`Compliance.Tests`)

**Files:**
- Create: `backend/Services/Compliance/Compliance.Tests/Compliance.Tests.csproj`
- Create: `backend/Services/Compliance/Compliance.Tests/MasterDataServiceTests.cs`
- Create: `backend/Services/Compliance/Compliance.Tests/DocumentServiceTests.cs`
- Create: `backend/Services/Compliance/Compliance.Tests/ComplianceReadinessEngineTests.cs`

**Step 1: Implement Tests**
- Verify Document Type & Regulation Rules CRUD.
- Verify Document review status transitions and validation errors.
- Verify Compliance Readiness calculation:
  - Fails if mandatory documents are missing or expired.
  - Passes when all required documents are APPROVED and valid.
  - Generates `Compliance.ComplianceReady` outbox event.

**Step 2: Add project to solution and execute test run**
Run: `dotnet sln RacehorseTransport.sln add backend/Services/Compliance/Compliance.Tests/Compliance.Tests.csproj`
Run: `dotnet test backend/Services/Compliance/Compliance.Tests/Compliance.Tests.csproj`
Expected: All tests pass.

---

### Task 7: Gateway & Docker Integration

**Files:**
- Modify: `backend/Gateway/Gateway.Api/appsettings.json` (add routing for compliance service)
- Create: `backend/Services/Compliance/Compliance.Api/Dockerfile`
- Modify: `docker-compose.yml` (add `compliance-api` container service on port 8083)

**Step 1: Update Gateway routing**
Add YARP route `/api/v1/compliance/{**catch-all}` forwarding to `compliance-api`.

**Step 2: Verify full solution build**
Run: `dotnet build RacehorseTransport.sln`
Expected: Build succeeded with 0 warnings, 0 errors.
