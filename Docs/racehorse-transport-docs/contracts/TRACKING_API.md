# Tracking API contract (T12–T14)

All routes require an authenticated bearer token through the gateway. Read routes allow `CUSTOMER`, `DRIVER_ESCORT`, `FLEET_ROUTE_COORDINATOR`, `LOGISTICS_MANAGER`, and `ADMIN`; update routes allow `DRIVER_ESCORT`, `FLEET_ROUTE_COORDINATOR`, `LOGISTICS_MANAGER`, and `ADMIN`. Planning owns `GET /api/v1/trips/{tripId}` and `POST /api/v1/trips`; Tracking retains the nested trip action routes below. The gateway sends the more-specific Tracking routes to Tracking (route order 50) before the Planning trips catch-all (order 100).

Tracking currently authorizes by role but does not yet verify trip/horse assignment against an identity-owned assignment source. `DRIVER_ESCORT` mutations therefore require further resource-level assignment validation before production use.

## Trip execution

The service-native routes are `POST /api/v1/trips`, `GET /api/v1/trips/{tripId}`, `POST /api/v1/trips/{tripId}/departure`, `POST /api/v1/trips/{tripId}/arrival`, `PUT /api/v1/trips/{tripId}/location`, and `POST|GET /api/v1/trips/{tripId}/checkpoints` (plus `GET /api/v1/trips/{tripId}/checkpoints/{checkpointId}`). The gateway exposes root/detail aliases `POST|GET /api/v1/trip-executions[/{tripId}]`; these are rewritten to Tracking's `/api/v1/trips[/{tripId}]`.

## GPS and ETA

Both the service-native paths and gateway aliases are supported:

| Operation | Service-native route | Gateway alias |
| --- | --- | --- |
| Ingest/list GPS points | `POST|GET /api/v1/trips/{tripId}/gps/points` | `POST|GET /api/v1/tracking/trips/{tripId}/gps/points` |
| Latest GPS point | `GET /api/v1/trips/{tripId}/gps/points/latest` | `GET /api/v1/tracking/trips/{tripId}/gps/points/latest` |
| Record/list GPS locations | `POST|GET /api/v1/trips/{tripId}/gps/locations` | `POST|GET /api/v1/tracking/trips/{tripId}/gps/locations` |
| Calculate/current ETA | `POST /api/v1/trips/{tripId}/eta/calculate`; `GET /api/v1/trips/{tripId}/eta/current` | `POST /api/v1/tracking/trips/{tripId}/eta/calculate`; `GET /api/v1/tracking/trips/{tripId}/eta/current` |

## Horse health

The service-native and gateway-alias paths are:

| Operation | Service-native route | Gateway alias |
| --- | --- | --- |
| Record/list health logs | `POST|GET /api/v1/trips/{tripId}/horses/{horseId}/health/logs` | `POST|GET /api/v1/tracking/trips/{tripId}/horses/{horseId}/health/logs` |
| Get health log | `GET /api/v1/trips/{tripId}/horses/{horseId}/health/logs/{logId}` | `GET /api/v1/tracking/trips/{tripId}/horses/{horseId}/health/logs/{logId}` |
| Create/list alerts | `POST|GET /api/v1/trips/{tripId}/horses/{horseId}/health/alerts` | `POST|GET /api/v1/tracking/trips/{tripId}/horses/{horseId}/health/alerts` |
| Acknowledge alert | `POST /api/v1/trips/{tripId}/horses/{horseId}/health/alerts/{alertId}/acknowledge` | `POST /api/v1/tracking/trips/{tripId}/horses/{horseId}/health/alerts/{alertId}/acknowledge` |
| Health status | `GET /api/v1/trips/{tripId}/horses/{horseId}/health/status` | `GET /api/v1/tracking/trips/{tripId}/horses/{horseId}/health/status` |

Gateway aliases strip `/api/v1/tracking` and preserve the remaining route under `/api/v1`, forwarding to Tracking. `tripId`, `horseId`, and resource IDs are GUIDs. Request and response payloads are defined by the corresponding Tracking endpoint DTOs.
