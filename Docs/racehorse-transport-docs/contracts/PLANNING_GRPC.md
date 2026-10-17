# Planning gRPC Contract

## Purpose

Tracking needs a small, current snapshot of a Planning trip before presenting cross-service execution information. This synchronous internal query uses gRPC rather than sharing the Planning database or coupling the public client to HTTP/2.

## Transport

- Contract: `backend/Contracts/Grpc/planning.proto`
- Server: Planning API, internal Compose port `8081`, HTTP/2
- Client: Tracking API generated `PlanningQueryClient`
- Public facade: `GET /api/v1/trips/{tripId}/planning-summary`
- The gRPC port is exposed only inside the Docker Compose network.

## Method

```protobuf
rpc GetTripSummary (GetTripSummaryRequest) returns (TripSummaryReply);
```

The response contains the trip/order identifiers, trip number, status, compliance readiness, planned timestamps, delay and optimistic-concurrency version.

## Error mapping

| Condition | gRPC status | REST facade |
|---|---|---|
| Invalid `trip_id` | `INVALID_ARGUMENT` | Not produced by the GUID-constrained facade route |
| Trip not found | `NOT_FOUND` | HTTP 404 in the standard API envelope |
| Planning unavailable | `UNAVAILABLE` | Handled by the global exception path and logged with correlation context |

## Demo verification

Call the public facade with a valid JWT and Planning Trip ID:

```http
GET /api/v1/trips/{tripId}/planning-summary
Authorization: Bearer {accessToken}
```

A successful response includes:

```json
{
  "code": 0,
  "data": {
    "tripId": "...",
    "status": "PLANNED",
    "source": "planning-grpc"
  }
}
```
