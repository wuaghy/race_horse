# Cross-Border Racehorse Transport System

A distributed .NET 10 microservices platform for planning, dispatching, and monitoring international racehorse transport across borders under strict veterinary and quarantine regulations.

## Language

**Transport Trip**:
A scheduled transport execution conveying one or more racehorses across defined route legs from origin stable to destination venue.
_Avoid_: Shipment, delivery, ride

**Pre-Flight Gate**:
The synchronous verification checkpoint executed immediately prior to trip departure confirmation (`PLANNED` to `READY`) to ensure zero regulatory or resource violations.
_Avoid_: Final check, pre-trip inspection

**Compliance Clearance**:
Official regulatory certification that all required veterinary health tests, laboratory results, and border permits have been verified and approved.
_Avoid_: Medical check, pass, document approval

**Readiness Blocker**:
An unresolved operational or legal deficiency (e.g. unassigned escort, unallocated stall, missing Coggins test) preventing a trip from transitioning to `READY`.
_Avoid_: Error, bottleneck, obstacle

**Dual-Mode Consistency**:
An architectural pattern combining asynchronous event notification via RabbitMQ for eventual consistency with synchronous gRPC calls for point-in-time authoritative gating.
_Avoid_: Hybrid messaging, mixed sync-async

**Stateless Protocol Adapter**:
An infrastructure component that translates internal domain requests to external wire protocols (such as HTTP/2 gRPC) without querying domain database contexts directly.
_Avoid_: Smart client, database-aware adapter
