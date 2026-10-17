# Assessment Criteria Evidence

This checklist maps the implementation to the seven assessment criteria and provides a short, repeatable demo path. It is intended for the final report and presentation; every claim below points to runnable code or configuration in this repository.

## 1. System architecture and design — 20%

Evidence:

- Eight independently deployable business microservices plus a YARP API Gateway.
- Clean Architecture separation into Domain, Application, Infrastructure and API projects.
- Database-per-service ownership across eight SQL Server databases.
- JWT authentication and role policies enforced at the Gateway and service boundaries.
- C4 diagrams, service boundaries, database design, workflow states, security and API standards under `Docs/racehorse-transport-docs/docs/`.

Primary references:

- `Docs/racehorse-transport-docs/docs/C4_ARCHITECTURE.md`
- `Docs/racehorse-transport-docs/docs/ARCHITECTURE.md`
- `Docs/racehorse-transport-docs/docs/DATABASE.md`
- `backend/Gateway/Gateway.Api/appsettings.json`

## 2. REST API implementation — 20%

Evidence:

- Versioned `/api/v1` endpoints across Identity, Booking, Compliance, Planning, Tracking, Incident, Handover and Notification.
- Consistent `ApiResponse<T>` envelope, request/correlation IDs and centralized exception handling.
- JWT/RBAC authorization policies, validation, optimistic concurrency and service-owned persistence.
- Server-side pagination, search and status filtering for Booking queries.
- OpenAPI endpoints in Development and Swagger UI for Tracking.

Demo:

1. Authenticate through `POST /api/v1/auth/login`.
2. Call a protected paged endpoint such as `GET /api/v1/transport-requests?page=0&pageSize=10&search=QUEUE&status=SUBMITTED`.
3. Show `meta.pagination`, correlation headers and a validation/authorization failure response.

## 3. Background Job — 10%

Evidence:

- Planning, Compliance, Incident, Handover and Tracking run hosted outbox dispatchers derived from `BackgroundService`.
- RabbitMQ consumers run as hosted services in Planning, Handover and Notification.
- Dispatchers poll pending outbox messages, publish them, record status and retry failures with a bounded retry count.
- Unit tests cover successful dispatch and failure/retry behavior.

Primary references:

- `backend/Services/Planning/Planning.Infrastructure/Messaging/OutboxDispatcherBackgroundService.cs`
- `backend/Services/Compliance/Compliance.Infrastructure/Messaging/ComplianceOutboxDispatcher.cs`
- `backend/Services/Incident/Incident.Infrastructure/Messaging/IncidentOutboxDispatcher.cs`
- `backend/Services/Handover/Handover.Infrastructure/Messaging/HandoverOutboxDispatcher.cs`
- `backend/Services/Tracking/Tracking.Infrastructure/Messaging/TrackingOutboxDispatcher.cs`
- `backend/Services/Planning/Planning.Tests/OutboxDispatcherTests.cs`

## 4. Message Broker integration — 15%

Evidence:

- RabbitMQ topic exchange `racehorse.events` is wired through Docker Compose.
- Transactional Outbox prevents a domain change from being committed without its integration event.
- Notification uses an Inbox table for consumer idempotency.
- Consumers use durable queues, acknowledgements and retry/requeue handling.
- Shared event contracts are maintained under `backend/Contracts/IntegrationEvents` and documented in `INTEGRATION_EVENTS.md`.

Demo:

1. Approve a transport request or confirm a trip-ready workflow.
2. Show the Outbox row moving from `PENDING` to `PUBLISHED`.
3. Show the RabbitMQ exchange/queue in the management UI and the resulting Notification/Planning state.

## 5. gRPC service — 15%

Implemented flow:

```text
REST client
  -> Tracking GET /api/v1/trips/{tripId}/planning-summary
  -> PlanningGrpcClient (HTTP/2)
  -> PlanningQuery.GetTripSummary
  -> PlanningDb.TransportTrips
```

Evidence:

- Contract-first protobuf: `backend/Contracts/Grpc/planning.proto`.
- Planning hosts `PlanningQueryGrpcService` on the internal HTTP/2 port `8081`.
- Tracking uses a generated strongly typed gRPC client.
- Docker exposes the gRPC port only to the Compose network; clients still use the public REST Gateway.
- `NotFound` and invalid UUID cases use canonical gRPC status codes.

Demo:

1. Create or obtain a Planning Trip ID.
2. Call `GET /api/v1/trips/{tripId}/planning-summary` through the Gateway.
3. Verify the response contains `source: "planning-grpc"` and check the Planning log entry.

## 6. Docker/Cloud deployment — 10%

Evidence:

- Multi-stage Dockerfiles build minimal ASP.NET runtime images.
- Docker Compose starts Gateway, eight services, SQL Server, RabbitMQ and MinIO.
- Health checks and dependency ordering protect startup.
- Configuration and secrets are supplied through environment variables.
- Application containers are stateless; SQL, RabbitMQ and object-storage data use named volumes.

Demo:

```bash
docker compose ps
curl http://localhost:8080/health
```

The submitted deployment evidence is Docker Compose. No unsupported cloud-deployment claim should be made unless a cloud environment is actually provisioned before presentation.

## 7. Documentation and presentation — 10%

Evidence:

- C4 architecture report and diagrams.
- Local-development, security, database, testing, frontend, API and integration-event documentation.
- Deterministic demo accounts and local-testing guide.
- This rubric mapping provides direct traceability from each scoring item to code and a demo step.

Recommended presentation order:

1. Problem and business workflow.
2. C4 architecture and service/data ownership.
3. REST request through Gateway.
4. Transactional Outbox and RabbitMQ event flow.
5. Background dispatcher retry/idempotency.
6. Live Tracking-to-Planning gRPC call.
7. Docker Compose topology, health and test summary.

## Final verification checklist

Verified baseline on 2026-10-17:

- Backend solution: **65 tests passed, 0 failed**.
- Planning, Tracking and Gateway Docker images: **built successfully**.
- Live Gateway → Tracking → Planning gRPC request: **HTTP 200** with `source: "planning-grpc"`.
- RabbitMQ: **3 durable queues with active consumers**; `Planning.TripCreated` reached `PUBLISHED` with retry count 0.
- Project Docker stack: **all application services running; SQL Server, RabbitMQ and MinIO healthy**.
- Frontend production build: **passed**.

- [x] `dotnet build RacehorseTransport.sln`
- [x] Backend automated tests pass.
- [x] Frontend production build passes.
- [x] All project Docker containers are running; infrastructure health checks pass.
- [x] REST login and one protected paged query pass.
- [x] RabbitMQ publish/consume path is demonstrated.
- [x] gRPC path returns a real Planning trip through Tracking.
- [ ] Presentation screenshots contain no credentials or secrets.
