# Route transport mode configuration

Planning owns the route capability catalog. Configure `RouteTransportModes` in `Planning.Api` configuration with direction-specific location ID pairs:

```json
{
  "RouteTransportModes": {
    "<origin-location-guid>><destination-location-guid>": ["ROAD", "AIR"]
  }
}
```

Only explicitly configured pairs offer modes. Reverse-direction routes need their own entry. Supported values are `ROAD`, `AIR`, and `MIXED`; an unconfigured pair returns `configured: false` and an empty `allowedModes` list. Values are owned by Planning and can be supplied through environment-specific configuration or a secret/configuration provider. No mode is inferred from location type or airline records.

Customers and planning staff can query `GET /api/v1/route-capabilities?originLocationId={guid}&destinationLocationId={guid}`. The response payload contains `originLocationId`, `destinationLocationId`, `configured`, and `allowedModes`.

This is an offer-time lookup. The current system has no persistent route-capability administration screen; production routes must be configured before customers can submit them.

Booking submission now checks the Planning-owned capability over internal gRPC and rejects an unconfigured route or mode with `TRANSPORT_MODE_UNAVAILABLE_FOR_ROUTE`. Opt-in `SEED_DEMO_DATA=true` creates four demo locations (two stables and two airports) and route capabilities for all 12 directed pickup/delivery pairs, so every pair in the request dropdown can continue. Seeded pairs offer `ROAD`, `AIR`, and `MIXED`. For other routes, add the locations through Planning, then configure the directed pair in `Planning.Api` settings or deployment environment using the JSON shape above. Reverse travel needs a separate entry.

Customer cancellation of an approved order queries Planning by order ID over internal gRPC. Booking permits cancellation only when Planning reports no trip or `PLANNED`, then emits `Booking.OrderCancelled`; Planning changes a still-PLANNED trip to `CANCELLED`. The Planning read and Booking write are separate services, so a trip could advance to READY between the read and Booking transaction. The cancellation event will not cancel a trip that has already advanced, and Booking reports the order as cancelled. Eliminating this narrow race requires a shared command/saga or a cross-service transactional policy.
