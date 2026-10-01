# Planning API contract — BE3 phase 1

Base path is `/api/v1`. Every successful response uses `ApiResponse<T>` with `code`, `message`, `data`, `errors`, and `meta`.

## Fleet and location master data

CRUD resources are `countries`, `locations`, `vehicles`, `stalls`, `airlines`, and `flight-bookings`. `PUT` updates editable master-data fields only. Availability is read through:

- `GET /availability/vehicles?vehicleId={uuid}&from={utc}&to={utc}`
- `GET /availability/stalls?stallId={uuid}&from={utc}&to={utc}`
- `GET /availability/flights?flightBookingId={uuid}&from={utc}&to={utc}`

## Trip and route commands

`POST /trips` accepts `{ "orderId": "uuid" }` and is idempotent per Order ID; it represents the `Booking.OrderCreated` consumer. Create a route with `POST /trips/{id}/route-plans`, edit a DRAFT version using its legs/checkpoints endpoints, and command its state via `/submit`, `/approve`, and `/activate`. After activation, `POST /route-plans/{id}/versions` is required before any further edits.

Resource, staff, and horse assignment use command endpoints only. Example resource body:

```json
{ "vehicleId": "uuid", "flightBookingId": null }
```

## Readiness

`POST /trips/{id}/compliance-ready` is the idempotent input for `Compliance.ComplianceReady`. `GET /trips/{id}/readiness` returns a `ready` flag and Vietnamese blocker messages. `POST /trips/{id}/confirm-ready` returns HTTP 422 while any blocker remains; on success it changes only through the command and emits `Planning.TripReady` to the outbox.
