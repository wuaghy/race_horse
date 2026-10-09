# Handover API Contract — BE3 Phase 2 (T17)

Base path: `/api/v1/handovers`. All JSON responses use the standard `ApiResponse<T>` envelope. IDs are UUIDs and API datetimes are UTC ISO 8601.

## Roles

- `CUSTOMER`, `DRIVER_ESCORT`, `FLEET_ROUTE_COORDINATOR`, `LOGISTICS_MANAGER`, and `ADMIN` can read handover records, check completion blockers, and download signatures or horse evidence images.
- `DRIVER_ESCORT`, `FLEET_ROUTE_COORDINATOR`, `LOGISTICS_MANAGER`, and `ADMIN` can prepare a handover draft (`POST /handovers`), update draft metadata (`PATCH /handovers/{id}`), add/update horses (`POST /handovers/{id}/horses`), record per-horse inspection (`POST /handovers/{id}/horses/{horseId}/inspect`), and submit the handover for acceptance (`POST /handovers/{id}/submit`).
- `CUSTOMER`, `LOGISTICS_MANAGER`, and `ADMIN` can accept or dispute individual horses (`POST /handovers/{id}/horses/{horseId}/accept`, `POST /handovers/{id}/horses/{horseId}/dispute`).
- `FLEET_ROUTE_COORDINATOR`, `LOGISTICS_MANAGER`, and `ADMIN` can formally resolve a disputed horse (`POST /handovers/{id}/horses/{horseId}/resolve-dispute`).
- `CUSTOMER`, `DRIVER_ESCORT`, `FLEET_ROUTE_COORDINATOR`, `LOGISTICS_MANAGER`, and `ADMIN` can upload signature or horse evidence files and complete the handover once all completion prerequisites pass (`POST /handovers/{id}/complete`).

## Endpoints

| Method | Path | Purpose |
|---|---|---|
| GET | `/handovers?page=0&pageSize=20&tripId={uuid}&status=PENDING_ACCEPTANCE` | Filtered, paginated list |
| GET | `/handovers/{id}` | Handover detail, horse inspection/acceptance list, and completion blockers |
| GET | `/handovers/by-trip/{tripId}` | Handover detail by Trip ID (`1:1` per trip) |
| GET | `/handovers/{id}/completion-check` | Evaluate whether handover can be completed and list blockers |
| POST | `/handovers` | Create handover record in `DRAFT` (`1:1` per `tripId`) |
| PATCH | `/handovers/{id}` | Partial update of editable metadata (`receiverName`, `receiverPhone`, `receiverEmail`, `handoverLocationId`, `scheduledAt`, `notes`) |
| POST | `/handovers/{id}/horses` | Add or update a horse on the handover record |
| POST | `/handovers/{id}/horses/{horseId}/inspect` | Record per-horse inspection (`condition`, `issueNote`) |
| POST | `/handovers/{id}/horses/{horseId}/evidence` | Multipart evidence upload (JPG, PNG, WebP, PDF; max 10 MB) |
| GET | `/handovers/{id}/horses/{horseId}/evidence` | Authorized horse evidence download |
| POST | `/handovers/{id}/submit` | Command: `DRAFT` → `PENDING_ACCEPTANCE` (requires receiverName and ≥ 1 horse) |
| POST | `/handovers/{id}/horses/{horseId}/accept` | Command: Accept horse (`PENDING`/`DISPUTED` → `ACCEPTED`) |
| POST | `/handovers/{id}/horses/{horseId}/dispute` | Command: Dispute horse (`PENDING`/`ACCEPTED` → `DISPUTED`; requires `issueNote`) |
| POST | `/handovers/{id}/horses/{horseId}/resolve-dispute` | Command: Formally resolve disputed horse (`DISPUTED` → `ACCEPTED`; requires `resolutionNote`) |
| POST | `/handovers/{id}/signature` | Multipart receiver signature upload (JPG, PNG, WebP, PDF; max 10 MB) |
| GET | `/handovers/{id}/signature` | Authorized signature download |
| POST | `/handovers/{id}/complete` | Command: Validate completion rules and transition to `COMPLETED` |

## Completion Rules & Concurrency

- Handover cannot transition to `COMPLETED` unless:
  1. It has been submitted (`Status != DRAFT`).
  2. It has at least one horse.
  3. Every horse in `HandoverHorses` is `ACCEPTED` (no `PENDING` or unresolved `DISPUTED` horses remain).
  4. Receiver signature (`SignatureUrl`) has been uploaded.
- Illegal state transitions or unmet completion prerequisites return HTTP `422`.
- Commands support `expectedVersionNo`; stale version numbers return HTTP `409` (`COMMON_CONCURRENCY_CONFLICT`).

## Events

- Completing a handover writes `Handover.HandoverCompleted` to `HandoverDb.OutboxMessages` in the same database transaction as the `HandoverRecord` state transition and `AuditLogs` entry.
- The service also consumes `Tracking.TripDeparted` idempotently (keyed by unique `TripId`) to pre-create a `DRAFT` handover record when a trip departs.
