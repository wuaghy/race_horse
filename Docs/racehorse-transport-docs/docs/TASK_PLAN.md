# Task Plan — Vertical Slice Delivery

## 1. Task rule

A normal feature task is **not complete** when the API works.

It is complete only when:

```text
BE
 +
DB migration
 +
API contract
 +
FE API integration
 +
Query/mutation state
 +
Navigation
 +
Screen UI
 +
Validation
 +
Loading/error/empty states
 +
End-to-end manual verification
```

The member owning a feature task must hand over a runnable screen.

Foundation tasks are the only exception because they create shared infrastructure rather than a business screen.

## 2. Priority labels

- P0: mandatory prerequisite
- P1: core business path
- P2: important supporting feature
- P3: optional/advanced
- P4: hardening/release

## 3. Dependency notation

`Depends on: Txx` means the task cannot be integrated until the dependency contract is available.

Tasks marked "parallel" may be implemented concurrently using mocked dependencies.

---

## T00 — Architecture Freeze

**Priority:** P0

**Depends on:** none

**Scope:** documentation / contract only

### Deliverables

- service boundaries
- database ownership
- state machines
- API response contract
- event envelope
- naming conventions
- screen/feature ownership map

### Acceptance

All future tasks can reference the frozen documents without inventing their own conventions.

---

## T01 — Repository / Docker / Shared Backend Foundation

**Priority:** P0

**Depends on:** T00

**Can parallel with:** T02

### Backend

- solution structure
- common response wrapper
- exception middleware
- correlation ID
- logging
- validation base
- Dockerfiles
- Compose
- SQL Server
- RabbitMQ
- object storage

### Frontend

- React Native project
- TypeScript configuration
- navigation shell
- theme/design tokens
- core API client skeleton
- TanStack Query provider
- common loading/error/empty components

### Screen output

`System Shell / Health Screen`:

- calls Gateway health endpoint
- shows API connectivity
- shows environment

### Acceptance

A clean clone can run the mobile shell and the entire backend stack via Docker.

---

## T02 — Identity, JWT & Role-aware Shell

**Priority:** P0

**Depends on:** T01

### Backend

Identity service:

- User
- Role
- login
- refresh
- logout
- me
- password hashing
- JWT
- authorization policies

Gateway:

- JWT validation
- routing

### Frontend

- login screen
- session restoration
- secure token storage
- refresh flow
- logout
- authenticated/unauthenticated navigation
- role-based navigator shell

### Screens

- Login
- Splash/Session Restore
- Unauthorized/Forbidden

### Acceptance

A user can log in, reopen app, refresh token, navigate by role, and log out.

---

## T03 — Customer Profile & Horse Management

**Priority:** P1

**Depends on:** T02

### Service

Booking.

### Backend

- Customer profile
- Horse CRUD
- ownership validation
- horse status
- passport/registration fields

### Frontend

- Profile screen
- Horse list
- Horse detail
- Add horse
- Edit horse
- validation

### Acceptance

Customer can create and edit a horse and see only horses they are allowed to access.

---

## T04 — Create Transport Request

**Priority:** P1

**Depends on:** T03

### Backend

- request draft
- request detail
- add/remove horses
- origin/destination
- schedule
- requirements
- validation
- save draft

### Frontend

- Request list
- Create Request wizard/form
- select horses
- route input
- schedule input
- special requirements
- save draft

### Screens

- Request List
- Create Request
- Request Detail

### Acceptance

Customer can save a valid draft request and reopen it with the same data.

---

## T05 — Submit Request & Manager Approval

**Priority:** P1

**Depends on:** T04, T02

**Parallel:** Compliance master-data work may start, but integration waits for this task.

### Backend

- submit
- manager queue
- request information
- approve
- reject
- reason
- audit
- create TransportOrder transactionally
- publish `Booking.RequestApproved` and `Booking.OrderCreated`

### Frontend

Customer:

- submit confirmation
- status UI
- returned-to-customer information state

Manager:

- pending request list
- request detail
- approve dialog
- reject dialog
- request-information dialog

### Screens

- Customer Request Status
- Manager Request Queue
- Manager Request Detail

### Acceptance

Approval creates exactly one Order and cannot be duplicated by repeated requests.

---

## T06 — Compliance Master Data & Regulation

**Priority:** P1

**Depends on:** T02

**Can parallel with:** T03–T05

### Backend

- countries/reference
- document types
- regulation rules
- required document mapping
- quarantine/customs/inspection requirement

### Frontend

Admin/Transport Specialist:

- Document Type list/detail
- Regulation list
- Regulation editor
- required document editor

### Acceptance

Specialist can define a rule such as:

```text
Vietnam → Japan
requires Passport
requires Health Certificate
requires Vaccination Certificate
requires Customs
requires Quarantine
```

---

## T07 — Horse / Trip Documents

**Priority:** P1

**Depends on:** T05, T06

### Backend

Compliance:

- upload document
- file storage
- document metadata
- list by horse/request/trip
- review history
- approve/reject/request-information
- expiry validation

### Frontend

Customer:

- Horse documents
- Upload document
- document status

Specialist:

- review queue
- document detail
- approve/reject/request info

### Screens

- Horse Documents
- Document Upload
- Document Review

### Acceptance

A Customer can upload a required document and a Specialist can review it while preserving review history.

---

## T08 — Clearance & Compliance Readiness

**Priority:** P1

**Depends on:** T07, T06, T05

### Backend

- clearance case
- case documents
- submission/review states
- required-document calculation
- readiness result
- `Compliance.ClearanceApproved`
- `Compliance.ComplianceReady`

### Frontend

- Clearance list
- Clearance detail
- readiness dashboard
- missing/expired document display

### Screens

- Compliance Dashboard
- Clearance Detail

### Acceptance

The system clearly explains why a Trip is not yet compliance-ready.

---

## T09 — Fleet & Location Master Data

**Priority:** P1

**Depends on:** T02

**Can parallel with:** T06–T08

### Backend

Planning:

- Country
- Location
- Vehicle
- Stall
- Airline
- Flight Booking
- availability checks

### Frontend

Coordinator:

- vehicle list/detail
- stall list/detail
- location list/detail
- flight booking list/detail

### Acceptance

Coordinator can maintain resources used by a Route Plan.

---

## T10 — Trip & Route Planning

**Priority:** P1

**Depends on:** T05, T09

**Integration depends on:** T08 if compliance-ready gating is enforced.

### Backend

- create Trip from Order
- RoutePlan
- RoutePlanVersion
- RouteLeg
- Checkpoint
- submit/approve/activate route
- resource assignment

### Frontend

- Trip list
- Trip detail
- Route editor
- checkpoint editor
- assignment screens

### Screens

- Trip List
- Create/Edit Route
- Trip Planning Detail

### Acceptance

Manager/Coordinator can turn an approved Order into a planned Trip with an active route and assigned resources.

---

## T11 — Trip Readiness & Pre-departure

**Priority:** P1

**Depends on:** T08, T10

### Backend

Readiness rule:

```text
active route
+ required resource assignment
+ staff assignment
+ horse/stall assignment
+ compliance ready
= READY
```

Publish `Planning.TripReady`.

### Frontend

- Trip readiness checklist
- blockers
- ready/not-ready status
- coordinator/manager confirmation

### Screen

- Trip Readiness Detail

### Acceptance

A Trip cannot depart if mandatory readiness conditions are not met.

---

## T12 — Trip Execution & Checkpoint Timeline

**Priority:** P1

**Depends on:** T11

### Backend

Tracking:

- trip events
- departure
- checkpoint arrival/departure
- customs cleared
- quarantine milestones
- destination arrival
- timeline

### Frontend

Driver/Escort:

- active trip
- next checkpoint
- update milestone
- timeline

Customer:

- trip timeline

### Screens

- Driver Active Trip
- Checkpoint Update
- Customer Trip Timeline

### Acceptance

Driver can update a checkpoint and Customer can see the resulting timeline.

---

## T13 — GPS Tracking & ETA

**Priority:** P2

**Depends on:** T12

### Backend

- GPS point ingestion
- current location
- history
- ETA calculation/service

### Frontend

- map/tracking screen
- current position
- route context
- ETA
- stale/offline indicator

### Acceptance

Customer can see latest known position and ETA. If GPS is unavailable, UI states when the last update occurred.

---

## T14 — Horse Health Monitoring

**Priority:** P1

**Depends on:** T12

### Backend

- health log
- condition
- stress level
- observation
- image
- health alert
- `Tracking.HealthAlertRaised`

### Frontend

Driver/Escort:

- horse list in active trip
- health form
- image upload
- health history

Customer/Manager:

- health status display

### Acceptance

A Driver/Escort can record a health observation for the correct horse and authorized viewers can see the history.

---

## T15 — Incident Management

**Priority:** P2

**Depends on:** T12, T14

### Backend

- incident creation
- severity
- attachments
- status
- resolution
- notification event

### Frontend

Driver/Escort:

- create incident
- attach image
- active incidents

Coordinator/Manager:

- incident queue
- detail
- resolve/close

### Acceptance

Critical incidents generate the expected notification and remain traceable to Trip/Leg/Horse when applicable.

---

## T16 — Emergency Route Change

**Priority:** P3

**Depends on:** T10, T15

### Backend

- proposed RoutePlanVersion
- RouteChangeRequest
- delay estimate
- additional cost
- approve/reject/apply
- archive old version
- publish `Planning.RouteChangeApplied`

### Frontend

Coordinator:

- propose route change
- compare old/new route

Manager:

- approve/reject

Driver:

- view new active route

Customer:

- see updated ETA/status

### Acceptance

Applying an emergency route creates a new active route version without deleting the old route history.

---

## T17 — Handover & Acceptance

**Priority:** P2

**Depends on:** T12

### Backend

- handover
- per-horse inspection
- accept/dispute
- signature
- attachments
- completion validation
- `Handover.HandoverCompleted`

### Frontend

Driver/Escort:

- handover preparation

Customer:

- horse acceptance
- inspection review
- signature/accept/dispute

### Screens

- Handover Detail
- Horse Acceptance
- Completion

### Acceptance

Trip cannot be completed before handover rules pass.

---

## T18 — Cost / Revenue / KPI Dashboard

**Priority:** P2

**Depends on:** T10, T12, T17

### Backend

- cost items
- revenue items
- on-time delivery
- average delay
- trip duration
- vehicle utilization
- incident rate
- compliance delay

### Frontend

Manager dashboard:

- revenue
- cost
- completed trips
- delays
- on-time rate
- active trips
- incidents

### Acceptance

Dashboard values can be traced back to source records and define all KPI formulas.

---

## T19 — Notification Center & Push

**Priority:** P2

**Depends on:** T05, T08, T12, T15, T16, T17

### Backend

- Notification service
- device tokens
- unread/read
- event consumers

### Frontend

- notification center
- read/unread
- push deep links

### Acceptance

A domain event results in the correct notification audience and the notification links back to the correct feature screen.

---

## T20 — Audit & Operational History

**Priority:** P2

**Depends on:** T05 onward

### Backend

Audit important actions:

- approval/rejection
- document review
- route activation
- route change
- incident resolution
- handover completion

### Frontend

Manager/Admin audit viewer.

### Acceptance

A business-sensitive state change can be traced to actor, time, reason and old/new state.

---

## T21 — Integration & E2E Hardening

**Priority:** P4

**Depends on:** T01–T20 as applicable

### Backend

- integration tests
- consumer idempotency
- retry/DLQ
- concurrency handling
- API contract checks

### Frontend

- offline/error handling
- stale state handling
- navigation edge cases
- token expiration edge case

### Acceptance

Critical business flows can run from Customer request to completed Trip in a clean environment.

---

## T22 — Production Docker & Deployment

**Priority:** P4

**Depends on:** T21

### Deliverables

- production Dockerfiles
- environment variables/secrets
- reverse proxy/ingress config
- health checks
- database migration process
- RabbitMQ configuration
- object storage configuration
- logging/monitoring

### Acceptance

A fresh environment can deploy the stack using documented commands and verify health of all services.
