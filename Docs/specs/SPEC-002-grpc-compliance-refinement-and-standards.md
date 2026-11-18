# SPEC-002: gRPC Compliance Verification Refinement and Standards Alignment

## Problem Statement

During the code review of the initial gRPC implementation (SPEC-001), several architectural, standards-compliance, and operational edge-case flaws were detected:

1. **CQRS & REST Idempotency Violation:** Querying trip readiness (`GET /trips/{id}/readiness` and `GET /trips/{id}/blockers`) performs hidden, mutating database writes (`trackedTrip.ComplianceReady = true; await db.SaveChangesAsync();`) without audit logging. In RESTful systems, `GET` operations must remain strictly read-only and idempotent.
2. **Clean Architecture Boundary Leak:** `ComplianceGrpcClient` in `Planning.Infrastructure` injects `IPlanningDbContext` and independently queries five entity tables (`TransportTrips`, `RoutePlans`, `RouteLegs`, `HorseLegAssignments`, `Locations`) to discover countries and horses. This violates layer boundaries (`BACKEND_STRUCTURE.md`), envies domain data, and forces redundant duplicate database queries since `TripReadinessService` already resolves these entities.
3. **Cache Staleness & False-Positive Vulnerabilities:** When the live gRPC call reports regulatory violations (`IsCompliant == false`), Planning records blockers in memory but fails to invalidate any previously cached positive approval (`trip.ComplianceReady = false`). If gRPC later experiences a transient outage, fallback logic mistakenly relies on the outdated positive flag. Furthermore, trips lacking active route legs send empty country identifiers to Compliance, causing unconfigured rule evaluation issues.
4. **Scope Creep & Duplicate Code:** `PlanningCommandEndpoints.cs` maps two identical routes (`/trips/{id}/compliance-verification` and `/planning/trips/{id}/compliance-verification`), the latter being an unroutable alias through YARP Gateway. Additionally, `docker-compose.yml` injected an unnecessary gRPC configuration into `tracking-api`.
5. **Missing Resilience Deadlines & Audit Trail:** The gRPC client lacks explicit deadline timeouts (violating `ARCHITECTURE.md §8`), and trip departure confirmation (`ConfirmReadyAsync`) does not record a compliance verification snapshot in its audit log.

---

## Solution

Refactor the gRPC pre-flight verification subsystem to align with repository coding standards, CQRS principles, and Clean Architecture guidelines:

1. **Pure Read-Only Readiness Checks:** Remove all database mutations from `EvaluateReadinessAsync`. The method performs on-demand gRPC queries and returns current blockers without modifying `TransportTrips`.
2. **Stateless gRPC Protocol Adapter (ADR-0002):** Remove `IPlanningDbContext` injection from `ComplianceGrpcClient`. The Application layer (`TripReadinessService`) prepares an explicit input query model (`ComplianceTripQuery`) containing already-resolved country IDs, horse IDs, and departure date, and hands it to the client adapter.
3. **Rigorous Cache Invalidation & Gate Check:** When confirming readiness (`ConfirmReadyAsync`), if gRPC reports compliance violations, the service explicitly sets `trip.ComplianceReady = false` in database storage. If a trip has no active route plan or legs, Planning rejects compliance immediately as a local blocker without querying Compliance with blank data.
4. **Resilience Timeout & Audit Telemetry:** Configure an explicit 5-second deadline for outbound gRPC requests. Include the gRPC verification outcome, timestamp, and met/missing statistics within the `CONFIRM_READY` audit log payload.
5. **Codebase Cleanup:** Eliminate the redundant `/planning/trips/...` route alias and remove the extraneous gRPC environment variable from `tracking-api` in `docker-compose.yml`.

---

## User Stories

1. As a Transport Coordinator, I want `GET /trips/{id}/readiness` to be completely idempotent, so that viewing readiness or refreshing the screen never causes unexpected background database mutations.
2. As a Transport Coordinator, I want any regulatory violation detected by gRPC to immediately invalidate any previously stored `ComplianceReady` status, so that horses with revoked permits can never be dispatched.
3. As a Transport Coordinator, I want departure confirmation (`POST /trips/{id}/confirm-ready`) to record the exact compliance clearance snapshot in the audit log, so that regulatory compliance is permanently provable.
4. As a Logistics Planner, I want trips without an active route version or route legs to immediately report missing route blockers without sending invalid empty country IDs over gRPC, so that errors are caught at the responsible domain layer.
5. As a Software Architect, I want `ComplianceGrpcClient` to be a pure, stateless protocol adapter without database access, so that layer boundaries between Application and Infrastructure are cleanly preserved.
6. As a Software Architect, I want all data required for the gRPC request to be resolved once in the Application layer, so that the database is not subjected to redundant, duplicate queries for the same trip.
7. As a Systems Operator, I want outbound gRPC calls from Planning to Compliance to enforce a 5-second deadline, so that network partitions or slow responses fail fast and trigger graceful fallback rather than hanging worker threads.
8. As an API Consumer, I want a single, canonical REST route `/api/v1/trips/{id}/compliance-verification` routed through YARP Gateway, so that the API surface remains clean and predictable.
9. As a Developer, I want integration tests to verify the REST-to-gRPC wire execution, so that contract regressions are caught before deployment.
10. As a PRN232 Examiner, I want to inspect clean code free of duplicate endpoints, unreferenced configuration variables, or layer violations, so that the architecture achieves top marks.
11. As a DevOps Engineer, I want container environment variables in `docker-compose.yml` to reflect only real service dependencies, preventing configuration confusion in production.
12. As a Security Auditor, I want state transitions to occur only in authenticated command endpoints (`POST`), ensuring all data mutations are tracked by actor user ID.
13. As a Fleet Manager, I want departure to be strictly blocked when gRPC reports missing documents, even if a previous asynchronous message had marked the trip ready.
14. As a Mobile Driver, I want readiness blockers to describe exactly which route legs or compliance checks failed, so that road transport is never delayed by ambiguous error messages.
15. As a Veterinarian, I want any sudden health test failure to block departure immediately without any residual cached clearance, protecting animal welfare and international biosecurity.

---

## Implementation Decisions

### 1. Architectural Boundaries & Data Flow (ADR-0002)
- **Application Layer (`Planning.Application`):**
  - Defines `ComplianceTripQuery`:
    ```csharp
    public record ComplianceTripQuery(
        Guid TripId,
        Guid OriginCountryId,
        Guid DestinationCountryId,
        IReadOnlyList<Guid> HorseIds,
        DateOnly? PlannedDepartureDate
    );
    ```
  - `TripReadinessService.EvaluateReadinessAsync`:
    - Resolves route, legs, country IDs, and horse IDs from existing local query.
    - If no route legs or no horses exist, adds local blockers and skips calling gRPC.
    - If valid, constructs `ComplianceTripQuery` and invokes `IComplianceVerificationClient.VerifyTripComplianceAsync(query, ct)`.
    - Merges returned blockers if non-compliant; does NOT call `db.SaveChangesAsync()`.
  - `TripReadinessService.ConfirmReadyAsync`:
    - Executes readiness evaluation.
    - If compliant, sets `trip.ComplianceReady = true`.
    - If non-compliant, sets `trip.ComplianceReady = false` and throws 422 `AppException`.
    - Records compliance telemetry snapshot in the `AuditLog` entity payload.
- **Infrastructure Layer (`Planning.Infrastructure`):**
  - `ComplianceGrpcClient`:
    - Removes `IPlanningDbContext` from constructor injection entirely.
    - Implements `VerifyTripComplianceAsync(ComplianceTripQuery query, CancellationToken ct)`.
    - Applies a 5-second deadline using `CallOptions.WithDeadline(DateTime.UtcNow.AddSeconds(5))`.
    - Only handles serialization of `ComplianceTripQuery` to proto message, invocation, and response mapping to `TripComplianceVerificationDto`.

### 2. Endpoint Consolidation & Docker Cleanliness
- Remove `api.MapGet("/planning/trips/{id}/compliance-verification", ...)` from `PlanningCommandEndpoints.cs`. Retain only `api.MapGet("/trips/{id}/compliance-verification", ...)`.
- Remove `GrpcServices__ComplianceUrl` under `tracking-api` in `docker-compose.yml`.

---

## Testing Decisions

### What Makes a Good Test
- Tests must verify observable system behavior through external boundaries:
  - Assert that calling `EvaluateReadinessAsync` does not change entity state in the database context.
  - Assert that `ComplianceGrpcClient` correctly maps query fields to proto messages and enforces timeouts.
  - Assert that when gRPC returns `IsCompliant = false`, `trip.ComplianceReady` is set to `false` upon confirmation attempt.
  - Assert that trips without route legs return descriptive blockers without calling gRPC with blank GUIDs.

### Seams to Test
1. **Primary Application Seam:** `IComplianceVerificationClient` mock seam in `TripReadinessServiceTests` verifying pure read-only evaluation, blocker formatting, and cache invalidation.
2. **Infrastructure Client Seam:** `ComplianceGrpcClientTests` verifying stateless proto message generation and timeout configuration.
3. **REST Seam:** Endpoint verification ensuring `/api/v1/trips/{id}/compliance-verification` returns `ApiResponse<TripComplianceVerificationDto>` with HTTP 200.

### Prior Art
- `backend/Services/Planning/Planning.Tests/TripReadinessServiceTests.cs`
- `backend/Services/Compliance/Compliance.Tests/ComplianceRpcServiceTests.cs`

---

## Out of Scope

- Changing the `compliance.proto` contract (the proto contract is verified and stable).
- Adding gRPC streaming (unary RPC remains optimal).
- Database migration scripts (no schema changes are needed).

---

## Further Notes

- Completes all findings from the Standards and Spec reviews, ensuring 100% adherence to repository documentation (`API_STANDARD.md`, `BACKEND_STRUCTURE.md`, `ARCHITECTURE.md`).
