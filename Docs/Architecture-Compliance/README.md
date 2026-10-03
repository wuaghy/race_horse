# Backend Architecture Compliance

This check uses `Docs/racehorse-transport-team-docs/racehorse-transport-docs/docs/BACKEND_STRUCTURE.md` as its reference. It reflects the repository after the Identity and shared-health alignment work on 2026-10-03.


## Summary

The repository has the prescribed project layout, but it does not fully implement every layer guideline across all services. Identity's authentication path and the shared API shell now follow the intended boundaries more closely. Booking still has workflow orchestration in Infrastructure, and six other services remain shells rather than business implementations.

## Conforming Areas

- The solution has separate API, Application, Domain, and Infrastructure projects for each service, plus BuildingBlocks, Gateway, and integration-event contracts.
- Identity HTTP handlers now bind/validate API request DTOs and map application results to response DTOs. Registration, login, refresh rotation, and logout decisions live in `Identity.Application`; SQL persistence, password hashing, and JWT token issuance live in `Identity.Infrastructure`.
- Dependency registrations are layer-owned through `AddIdentityApplication`, `AddIdentityInfrastructure`, and `AddBookingInfrastructure`; API `Program.cs` retains HTTP, authentication, and authorization composition.
- Gateway contains routing, JWT validation, correlation/error middleware, and health only. It has no business workflow.
- Booking uses service-focused interfaces and SQL projection/pagination. It does not expose EF entities, and its local multi-table approval writes use a BookingDb transaction with audit/outbox rows.
- The Gateway, Identity, and Booking Dockerfiles use SDK build/publish stages followed by ASP.NET runtime stages.
- All API projects use the shared error and correlation middleware. The six not-yet-implemented service APIs now expose truthful `/health` endpoints rather than framework weather samples.
- `.env.example` now names the JWT settings consumed by Compose and warns that its defaults are local-development examples. Replace the JWT signing-key placeholder before starting services.

## Remaining Gaps

- `Booking.Infrastructure.TransportRequests.SqlTransportRequestService` still combines SQL persistence with request-state decisions, validation, transaction orchestration, audit, and outbox construction. Move those rules into Booking Application/Domain use cases and keep Infrastructure focused on persistence adapters; preserve approval atomicity while doing so.
- Booking application response records are currently returned directly by API endpoints. Add API-specific response DTOs when stabilizing the public contract so transport schemas can evolve independently from Application contracts.
- Identity and Booking Domain projects still have little or no substantive domain model. Introduce domain types where they provide real invariants; avoid adding empty abstractions solely to populate folders.
- Compliance, Planning, Tracking, Incident, Handover, and Notification expose health only. Their business endpoints, Application use cases, Domain rules, persistence, and tests remain unimplemented.
- Automated test projects are not present. Add focused unit tests for domain transitions and integration tests for ownership, optimistic concurrency, transactional approval, and outbox behavior.
- Structured logging covers correlation IDs and outbox publishing but is not yet consistent across command handlers with safe actor/entity/action/duration context.

## Verification

`dotnet build backend/RacehorseTransport.sln` succeeded after the shared health and Identity layer changes. This verifies compilation, not behavioral correctness; the remaining gaps above still need implementation and tests.