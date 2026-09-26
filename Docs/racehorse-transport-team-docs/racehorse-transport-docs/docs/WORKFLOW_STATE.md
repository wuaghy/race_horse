# Workflow & State Machines

## 1. Transport Request

```text
DRAFT
  │ submit
  ▼
SUBMITTED
  │ review
  ▼
UNDER_REVIEW
  ├───────────────┐
  │               │
  ▼               ▼
NEED_INFORMATION  APPROVED
  │                  │
  │ resubmit          │ create order
  └──────→ SUBMITTED  ▼
                   Order CREATED

UNDER_REVIEW → REJECTED
DRAFT/SUBMITTED → CANCELLED according to rules
```

## 2. Order

```text
CREATED
  ↓
PLANNING
  ↓
READY_FOR_EXECUTION
  ↓
IN_PROGRESS
  ↓
COMPLETED
```

Cancellation is allowed only according to business rules and unresolved operational commitments.

## 3. Trip

```text
PLANNED
  ↓
READY
  ↓
DEPARTED
  ↓
IN_TRANSIT
  ├── DELAYED ──→ IN_TRANSIT
  └── ARRIVED
          ↓
     HANDED_OVER
          ↓
       COMPLETED
```

Cancellation may occur before the point where execution makes cancellation operationally unsafe or invalid.

## 4. Route Plan Version

```text
DRAFT
  ↓
PENDING_APPROVAL
  ├── REJECTED
  └── APPROVED
          ↓
        ACTIVE
          ↓
       ARCHIVED
```

An active route is never edited in-place for emergency changes. Create a new version.

## 5. Document

```text
UPLOADED
  ↓
UNDER_REVIEW
  ├── APPROVED
  ├── REJECTED
  └── NEED_ADDITIONAL_INFO
             ↓
          UPLOADED
```

Expiration is a derived operational state and may be surfaced as `EXPIRED`.

## 6. Clearance

```text
DRAFT
 ↓
SUBMITTED
 ↓
UNDER_REVIEW
 ├── NEED_ADDITIONAL_INFO → SUBMITTED
 ├── APPROVED
 └── REJECTED
       
APPROVED → COMPLETED
```

## 7. Incident

```text
OPEN
 ↓
INVESTIGATING
 ├── RESOLVED
 └── CANCELLED
       ↓
     CLOSED
```

## 8. Handover

```text
DRAFT
 ↓
PENDING_ACCEPTANCE
 ├── ACCEPTED
 └── DISPUTED
        ↓ resolution
      ACCEPTED / COMPLETED
```

## 9. State-transition implementation

State transition belongs to the owning service domain/application layer.

Controller should call a command/service such as:

```csharp
await requestService.ApproveAsync(requestId, actorId, ct);
```

Do not accept arbitrary status values from a public DTO.

## 10. Audit

Every important transition logs:

```text
actor
entity
action
old state
new state
reason
timestamp
correlation id
```
