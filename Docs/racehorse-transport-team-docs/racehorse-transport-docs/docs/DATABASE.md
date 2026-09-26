# Database Design

## 1. Database ownership

```text
IdentityDb
BookingDb
ComplianceDb
PlanningDb
TrackingDb
IncidentDb
HandoverDb
NotificationDb
```

Every database belongs to exactly one service.

## 2. Ownership map

### IdentityDb

```text
Users
Roles
RefreshTokens
```

### BookingDb

```text
Customers
Horses
TransportRequests
TransportRequestHorses
TransportOrders
```

### ComplianceDb

```text
DocumentTypes
Documents
DocumentReviews
RegulationRules
RegulationDocumentRequirements
ClearanceCases
ClearanceCaseDocuments
```

### PlanningDb

```text
Countries
Locations
TransportTrips
Vehicles
Stalls
Airlines
FlightBookings
RoutePlans
RoutePlanVersions
RouteLegs
Checkpoints
TripLegAssignments
StaffAssignments
HorseLegAssignments
```

### TrackingDb

```text
TripEvents
TripLocations
HorseHealthLogs
```

### IncidentDb

```text
Incidents
IncidentAttachments
```

### HandoverDb

```text
HandoverRecords
HandoverHorses
TripCostItems
TripRevenueItems
```

### NotificationDb

```text
Notifications
DeviceTokens
```

## 3. Cross-service IDs

Example:

```text
BookingDb.Horses.Id = H123
PlanningDb.HorseLegAssignments.HorseId = H123
TrackingDb.HorseHealthLogs.HorseId = H123
```

`H123` is a logical reference only. There is no cross-database foreign key.

## 4. Core relationships

### Booking

```text
Customer 1 ─── N Horse
Customer 1 ─── N TransportRequest
TransportRequest N ─── N Horse
TransportRequest 1 ─── 1 TransportOrder
TransportOrder 1 ─── 1 TransportTrip
```

### Planning

```text
Trip 1 ─── 1 RoutePlan
RoutePlan 1 ─── N RoutePlanVersion
RoutePlanVersion 1 ─── N RouteLeg
RouteLeg 1 ─── N Checkpoint
Trip N ─── N Vehicle assignment by RouteLeg
Trip N ─── N Staff assignment
RouteLeg N ─── N Horse assignment by TripHorse/Stall
```

### Compliance

Documents may contain logical references to:

- HorseId
- RequestId
- TripId

but no cross-service foreign key.

## 5. Important constraints

### Request

- A Request must have at least one Horse before submission.
- Origin and destination are required.
- Requested departure must be valid.
- Only valid states may transition.

### Trip

- Trip belongs to one Order in this version of the system.
- Trip cannot become READY unless required operational prerequisites are satisfied.

### Route

- Route version is immutable after activation.
- Emergency change creates a new version.
- Only one RoutePlanVersion can be ACTIVE logically at a time.

### Vehicle

- Vehicle capacity must not be exceeded.
- Vehicle cannot be double-booked for overlapping operational intervals.

### Stall

- Stall capacity must not be exceeded.
- Stall cannot be double-booked for overlapping intervals.

### Staff

- Driver/Escort cannot be assigned to conflicting route legs.

### Document

- Expiry must be checked where regulation requires it.
- Review history must be preserved.

### Handover

Trip completion requires all required horses to be accepted or a formally resolved dispute according to business rules.

## 6. Naming conventions

SQL tables: PascalCase or configured EF naming strategy; choose one and keep it consistent. This project uses **PascalCase table names matching entity names** in the database design.

Columns:

```text
Id
CreatedAt
UpdatedAt
VersionNo
```

Primary keys use UUID/Guid.

## 7. Concurrency

Entities frequently updated by multiple actors should have:

```text
VersionNo
```

or EF Core row-version equivalent according to the selected SQL Server implementation.

On conflict return HTTP 409 with a business error code such as:

```text
COMMON_CONCURRENCY_CONFLICT
```

## 8. Migration rules

- Migration belongs to owning service.
- Never modify another service's migration.
- Never manually change production schema without migration review.
- Seed only stable reference/master data.

## 9. Database diagram

See `database/racehorse_microservices.dbml` for the full dbdiagram.io schema.
