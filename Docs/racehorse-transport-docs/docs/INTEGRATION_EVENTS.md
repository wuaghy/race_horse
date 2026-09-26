# Integration Event Standard

## 1. Event envelope

```json
{
  "eventId": "uuid",
  "eventType": "Booking.RequestApproved",
  "version": 1,
  "occurredAt": "2026-09-21T09:00:00Z",
  "correlationId": "uuid",
  "source": "booking-service",
  "data": {}
}
```

## 2. Naming

Format:

```text
<BoundedContext>.<PastTenseEvent>
```

Examples:

```text
Booking.RequestApproved
Booking.OrderCreated
Compliance.DocumentApproved
Compliance.ComplianceReady
Planning.TripCreated
Planning.TripReady
Planning.RouteActivated
Tracking.TripDeparted
Tracking.CheckpointArrived
Tracking.HealthAlertRaised
Incident.IncidentCreated
Planning.RouteChangeApplied
Handover.HandoverCompleted
```

## 3. Event rules

- Events describe facts that already happened.
- Do not name an event `ApproveRequest`; use `RequestApproved`.
- Consumer must be idempotent.
- Event schema is versioned.
- Do not put secrets in event payload.
- Do not put huge file contents in event payload.
- Use IDs and URLs/references where appropriate.

## 4. Important events

### Booking.RequestApproved

Producer: Booking

Consumers:

- Compliance
- Planning
- Notification

### Booking.OrderCreated

Producer: Booking

Consumers:

- Planning
- Notification

### Compliance.ComplianceReady

Producer: Compliance

Consumers:

- Planning
- Notification

### Planning.TripReady

Producer: Planning

Consumers:

- Tracking
- Notification

### Tracking.TripDeparted

Producer: Tracking

Consumers:

- Notification
- Handover
- Reporting later

### Tracking.HealthAlertRaised

Producer: Tracking

Consumers:

- Notification
- Incident

### Incident.IncidentCreated

Producer: Incident

Consumers:

- Notification
- Planning when route change may be needed

### Planning.RouteChangeApplied

Producer: Planning

Consumers:

- Tracking
- Notification

### Handover.HandoverCompleted

Producer: Handover

Consumers:

- Notification
- Reporting

## 5. Reliability

Use an outbox pattern when reliable publishing becomes necessary:

```text
Service DB transaction
   ├── business state update
   └── outbox event row
          ↓
       publisher
          ↓
       RabbitMQ
```

Do not claim an event was published simply because a database save succeeded.

## 6. Eventual consistency expectation

After a command, another service may update later.

Example:

```text
Booking approves request
        ↓
Request API immediately returns APPROVED
        ↓
RabbitMQ
        ↓
Planning receives OrderCreated
        ↓
Planning creates Trip
```

UI should show meaningful pending/synchronizing states instead of assuming every dependent service is instantly updated.
