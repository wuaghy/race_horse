# Planning API contract — BE3 phase 1

Base path is `/api/v1`. Every successful response uses `ApiResponse<T>` with `code`, `message`, `data`, `errors`, and `meta`.

Planning read/command access is granted to `TRANSPORT_SPECIALIST`, `FLEET_ROUTE_COORDINATOR`, `LOGISTICS_MANAGER`, and `ADMIN`; approval actions remain limited to `LOGISTICS_MANAGER` and `ADMIN`.

## Fleet and location master data

CRUD resources are `countries`, `locations`, `vehicles`, `stalls`, `airlines`, and `flight-bookings`. `PUT` updates editable master-data fields only. Availability is read through:

- `GET /availability/vehicles?vehicleId={uuid}&from={utc}&to={utc}`
- `GET /availability/stalls?stallId={uuid}&from={utc}&to={utc}`
- `GET /availability/flights?flightBookingId={uuid}&from={utc}&to={utc}`

## Trip and route commands

`POST /trips` accepts `{ "orderId": "uuid" }` and is idempotent per Order ID; it represents the `Booking.OrderCreated` consumer. Create a route with `POST /trips/{id}/route-plans`, edit a DRAFT version using its legs/checkpoints endpoints, and command its state via `/submit`, `/approve`, and `/activate`. After activation, `POST /route-plans/{id}/versions` is required before any further edits.

Planning owns `GET /trips/{id}` and `POST /trips`. Tracking execution commands retain the existing nested paths (`/trips/{id}/departure`, `/arrival`, `/location`, `/checkpoints`). The gateway has a `/trip-executions/{**path}` alias for Tracking's root/detail paths where those would otherwise overlap Planning.

Resource, staff, and horse assignment use command endpoints only. Example resource body:

```json
{ "vehicleId": "uuid", "flightBookingId": null }
```

## Emergency route changes

`POST /trips/{id}/route-change-requests` accepts `{ "reason", "delayEstimateMinutes", "additionalCost", "currencyCode" }` and creates a new DRAFT `RoutePlanVersion` linked to the trip's existing route plan. It requires an active version. Add the proposed route legs and checkpoints through the normal version endpoints, then submit for approval. A `LOGISTICS_MANAGER` or `ADMIN` may approve with `/route-plan-versions/{id}/approve` or reject with `/route-plan-versions/{id}/reject` and a required `{ "reason" }`. Applying an approved version via `/activate` archives the previous active version and emits `Planning.RouteChangeApplied` with the new version and impact estimates. The old route version remains queryable.

## Readiness

`POST /trips/{id}/compliance-ready` is the idempotent input for `Compliance.ComplianceReady`. `GET /trips/{id}/readiness` returns a `ready` flag and Vietnamese blocker messages. `POST /trips/{id}/confirm-ready` returns HTTP 422 while any blocker remains; on success it changes only through the command and emits `Planning.TripReady` to the outbox.
