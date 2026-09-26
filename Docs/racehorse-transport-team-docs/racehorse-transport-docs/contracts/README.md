# Contracts

This folder contains examples and is intended to become the single source of truth for machine-readable contracts.

Recommended additions once implementation begins:

```text
openapi/
  gateway.yaml
  identity.yaml
  booking.yaml
  compliance.yaml
  planning.yaml
  tracking.yaml
  incident.yaml
  handover.yaml

events/
  Booking.RequestApproved.v1.json
  Booking.OrderCreated.v1.json
  Compliance.ComplianceReady.v1.json
  Planning.TripReady.v1.json
  Tracking.TripDeparted.v1.json
  Tracking.HealthAlertRaised.v1.json
  Incident.IncidentCreated.v1.json
  Planning.RouteChangeApplied.v1.json
  Handover.HandoverCompleted.v1.json
```
