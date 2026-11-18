# SPEC-001: High-Performance gRPC Pre-Departure Compliance Verification

## Problem Statement

In the Cross-Border Racehorse Transport System, transport coordinators and logistics planners must ensure that all racehorses meet rigorous veterinary, quarantine, and customs regulations before a trip is confirmed ready for departure (`PLANNED -> READY`).

Currently, compliance status updates are communicated solely through asynchronous RabbitMQ domain events (`Compliance.ComplianceReady`). While this maintains eventual consistency, it introduces operational and technical deficiencies:
1. **Stale Pre-Flight Verification:** An asynchronous event indicates past approval, but does not provide authoritative, point-in-time verification at the exact moment of departure authorization. Veterinary permits may have expired or been revoked between initial approval and departure day.
2. **Lack of Detailed Blocker Telemetry:** When a trip is blocked in Planning, the coordinator receives only a generic local flag `Compliance chưa ready` without actionable breakdowns of which horse is missing which specific certificate (e.g. Coggins test, import permit, or quarantine clearance).
3. **Curricular Non-Compliance:** The academic grading rubric for PRN232 mandates a 15% grade allocation for gRPC inter-service communication (including `.proto` contract definition, gRPC Server, and gRPC Client integrated with REST API), which is currently absent from the microservices landscape.

Transport coordinators need an instantaneous, synchronous, strongly typed verification channel between Planning Service and Compliance Service that delivers point-in-time regulatory clearance without disrupting the existing asynchronous event pipeline or database boundaries.

---

## Solution

Implement a dedicated gRPC communication channel between the **Planning Service** (Client) and **Compliance Service** (Server) governed by a shared Protocol Buffers contract (`compliance.proto`), adopting the **Dual-Mode Consistency** pattern (recorded in ADR-0001).

1. **Synchronous Pre-Flight Gate:** Before confirming a trip ready or while inspecting pre-departure readiness via REST API, Planning queries Compliance over HTTP/2 gRPC.
2. **Authoritative Evaluation:** Compliance executes real-time validation across all active bilateral regulation rules, required documents (Health Certificate, Coggins Test, Import/Export Permits), and horse quarantine statuses, returning an exhaustive list of met and missing items.
3. **Preserved Eventual Consistency:** The existing RabbitMQ event flow (`Compliance.ComplianceReady`) remains untouched as the background notification mechanism. The gRPC call acts as an authoritative pre-departure audit gate.
4. **Graceful Fallback:** If the gRPC communication channel experiences network partitioning or downtime, the Planning service falls back safely to its local cached state with diagnostic warnings, preserving high operational availability without crashing dispatch operations.
5. **REST API Exposure for Assessment:** A dedicated REST endpoint is exposed on Planning Service (`GET /api/v1/planning/trips/{id}/compliance-verification`) to allow frontend clients and evaluation test suites to trigger and observe the live gRPC call.

---

## User Stories

1. As a Transport Coordinator, I want to trigger a live compliance verification for a planned trip, so that I can see the exact real-time regulatory status of all horses before authorizing departure.
2. As a Transport Coordinator, I want to view a detailed breakdown of satisfied and unsatisfied document requirements (e.g., Coggins Test, Export Permit), so that I can contact the stable or veterinarian immediately about missing items.
3. As a Transport Coordinator, I want the system to block trip departure confirmation if any horse or leg has an expired or missing mandatory health certificate, so that our transport fleet never violates border quarantine laws.
4. As a Logistics Planner, I want the trip readiness evaluation endpoint to query Compliance via low-latency gRPC, so that pre-departure evaluation is instantaneous even with dozens of horses per convoy.
5. As a Logistics Planner, I want clear, localized blocker messages when compliance verification fails, so that I understand exactly what prerequisite must be resolved.
6. As a Veterinarian / Quarantine Specialist, I want the Compliance Service to serve authoritative clearance data over gRPC, so that other operational services cannot bypass medical quarantine rules.
7. As a Veterinarian, I want any document revocation or expiry to immediately reflect in subsequent gRPC pre-flight calls, so that compromised horses cannot be transported even if an earlier notification was processed.
8. As a Fleet Manager, I want to be confident that gRPC requests operate over secure and efficient HTTP/2 binary streams, so that network overhead between internal microservices remains minimal.
9. As a System Auditor, I want pre-departure gRPC verification results to be timestamped and logged, so that we have an immutable audit trail of who authorized departure and under what compliance state.
10. As a PRN232 Examiner, I want to inspect a clearly defined Protocol Buffers contract (`.proto`), so that I can verify the microservices adhere to industry-standard RPC contracts.
11. As a PRN232 Examiner, I want to invoke a REST API endpoint in Swagger that internally executes a gRPC call to another microservice, so that I can verify end-to-end REST-to-gRPC integration.
12. As a PRN232 Examiner, I want to see structured log entries showing outbound gRPC client requests and inbound gRPC server responses, so that proof of RPC execution is indisputable.
13. As a Mobile Driver / Escort, I want departure clearance to be verifiable from the mobile gateway, so that I do not commence road transit until all border authorities have cleared the transport.
14. As a DevOps Engineer, I want the gRPC endpoint to be exposed on port 8083 supporting both HTTP/1.1 and HTTP/2 cleartext (h2c), so that it integrates seamlessly into our containerized Docker Compose environment without extra port mapping.
15. As a Software Architect, I want the Planning Service to gracefully fall back to local cached compliance data if the gRPC service is temporarily unreachable, so that a transient network hiccup does not completely paralyze dispatch operations.
16. As a Software Architect, I want database sovereignty to be strictly preserved without any cross-service SQL queries or shared databases between Planning and Compliance.
17. As an Integration Developer, I want strong contract typing generated from Protocol Buffers, so that breaking changes in compliance data schemas are caught at compile time.
18. As a Transport Specialist, I want to perform dry-run compliance checks during route drafting, so that I can detect regulatory hurdles weeks before the scheduled departure date.
19. As a Quality Assurance Engineer, I want unit tests to mock the gRPC verification client cleanly, so that business readiness tests remain deterministic and fast.
20. As a Dispatcher, I want the compliance verification response to include total counts of required, met, missing, and expired documents, so that I can gauge readiness at a glance on the dispatch dashboard.

---

## Implementation Decisions

### 1. Architectural Boundaries & Communication Patterns
- **Protocol Buffers Contract:** Placed in a shared contracts directory (`Contracts/Protos/compliance.proto`) accessible by both services, generating strongly typed C# stubs at compile time.
- **Server Placement:** Hosted inside `Compliance.Api` Kestrel process, configured to listen for HTTP/2 gRPC traffic alongside existing HTTP/1.1 REST endpoints using `HttpProtocols.Http1AndHttp2`.
- **Client Placement:** Injected into `Planning.Infrastructure` and exposed to `Planning.Application` via the `IComplianceVerificationClient` abstraction.
- **Dual-Mode Consistency (ADR-0001):** Asynchronous messaging via RabbitMQ handles proactive background updates (`Compliance.ComplianceReady`), while synchronous gRPC handles reactive point-in-time validation during pre-departure gating.

### 2. Protocol Buffers Schema Definition
*(Decision-rich schema type shape from contract specification)*

```protobuf
syntax = "proto3";

package racehorse.compliance;

option csharp_namespace = "Compliance.Grpc";

service ComplianceRpc {
  rpc VerifyTripCompliance (VerifyTripComplianceRequest) returns (VerifyTripComplianceResponse);
}

message VerifyTripComplianceRequest {
  string trip_id = 1;
  string origin_country_id = 2;
  string destination_country_id = 3;
  repeated string horse_ids = 4;
  string planned_departure_date = 5; // ISO 8601 (YYYY-MM-DD)
}

message ComplianceCheckItemModel {
  string document_type_code = 1;
  string document_type_name = 2;
  string scope = 3; // HORSE, TRIP, REQUEST
  string target_id = 4;
  string status = 5; // VALID, MISSING, EXPIRED, UNDER_REVIEW
  bool is_met = 6;
  string details = 7;
}

message VerifyTripComplianceResponse {
  string trip_id = 1;
  bool is_compliant = 2;
  int32 total_required = 3;
  int32 total_met = 4;
  int32 total_missing = 5;
  int32 total_expired = 6;
  repeated string blocker_messages = 7;
  repeated ComplianceCheckItemModel check_items = 8;
  string evaluated_at = 9;
}
```

### 3. Service Interactions & Graceful Degradation
- When `TripReadinessService.EvaluateReadinessAsync(tripId)` is invoked:
  1. The service loads the trip, active route plan, and assigned horses.
  2. The service calls `IComplianceVerificationClient.VerifyTripComplianceAsync(...)`.
  3. The gRPC client transmits parameters over HTTP/2 to Compliance Service.
  4. The Compliance gRPC Server executes rules validation against `RegulationRules`, `RegulationDocumentRequirements`, and uploaded `Documents`.
  5. If `is_compliant == true`, Planning updates the local trip compliance readiness state.
  6. If `is_compliant == false`, the specific blockers returned over gRPC are merged into the trip readiness blockers collection.
  7. If gRPC fails (network error, timeout, or RpcException), the system logs a warning and falls back to the existing local `trip.ComplianceReady` flag (Graceful Degradation).

### 4. REST Exposure
- A dedicated REST endpoint is added: `GET /api/v1/planning/trips/{id}/compliance-verification`
- This endpoint invokes the gRPC client directly and returns a standardized `ApiResponse<TripComplianceVerificationDto>`, providing immediate visibility on Swagger UI for presentation and evidence capture for PRN232 submission package.

---

## Testing Decisions

### What Makes a Good Test
Tests must verify observable system behavior through established seams rather than asserting internal execution flow. Specifically:
- Given a trip with missing certificates, the readiness check must report `Ready = false` and output the exact compliance blockers.
- Given a trip where all certificates are valid, the readiness check must report `Ready = true` and allow departure confirmation.
- The gRPC contract serialization and deserialization must execute without data truncation.
- Network disconnection must trigger graceful degradation to local cached state rather than throwing an unhandled 500 error.

### Seams to Test
1. **Primary High-Level Seam (Application Boundary):** `IComplianceVerificationClient` in Planning Application. Allows testing readiness evaluation logic with stubbed compliance outcomes without spinning up real network sockets.
2. **gRPC Server Seam:** `ComplianceRpcService` in Compliance. Can be tested directly with an in-memory test database to verify proto mapping and rule evaluation logic.
3. **End-to-End REST Seam:** `GET /api/v1/planning/trips/{id}/compliance-verification` to verify full integration across REST, gRPC client, and response serialization.

### Prior Art
- Unit and integration tests in `backend/Services/Planning/Planning.Tests/TripReadinessServiceTests.cs` (seam testing with `TestDbContextFactory`).
- Unit tests in `backend/Services/Compliance/Compliance.Tests/ComplianceReadinessServiceTests.cs` (multi-document rule validation testing).

---

## Out of Scope

- Implementing bi-directional streaming gRPC (unary call is optimal and sufficient for request-response compliance validation).
- Cross-database transaction sharing between Planning and Compliance (database-per-service sovereignty is strictly preserved).
- Replacing RabbitMQ asynchronous domain events (RabbitMQ remains active for all decoupled event broadcasts).
- Modifying authentication mechanisms (gRPC calls operate within the trusted internal container network).

---

## Further Notes

- **Port Strategy:** In Docker Compose, Compliance Service will expose port 8083 for HTTP/1.1 REST and HTTP/2 gRPC traffic (`HttpProtocols.Http1AndHttp2`).
- **Evidence Gathering:** The REST-to-gRPC endpoint provides the exact evidence required for Section `03_gRPC/` of the PRN232 submission package (screenshot of `.proto` file, screenshot of Swagger call, and screenshot of server/client terminal logs).
