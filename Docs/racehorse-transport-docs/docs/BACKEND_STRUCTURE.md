# Backend Source Architecture

## 1. Repository layout

```text
backend/
├── Gateway/
│   └── Gateway.Api/
│
├── Services/
│   ├── Identity/
│   │   ├── Identity.Api/
│   │   ├── Identity.Application/
│   │   ├── Identity.Domain/
│   │   └── Identity.Infrastructure/
│   │
│   ├── Booking/
│   ├── Compliance/
│   ├── Planning/
│   ├── Tracking/
│   ├── Incident/
│   ├── Handover/
│   └── Notification/
│
├── BuildingBlocks/
│   └── BuildingBlocks/
│       ├── Api/
│       ├── Authentication/
│       ├── Exceptions/
│       ├── Logging/
│       ├── Messaging/
│       └── Persistence/
│
├── Contracts/
│   └── IntegrationEvents/
│
└── Tests/
```

## 2. Service layers

### Api

Contains:

- Controllers
- API models if strictly transport-specific
- HTTP configuration
- Authentication/authorization wiring

Must not contain business rules.

### Application

Contains:

- Commands
- Queries
- DTOs
- Validators
- Application services
- Event handlers
- authorization checks that depend on use-case context

### Domain

Contains:

- Entities
- Value Objects
- Enums
- Domain rules
- Domain events/interfaces

Must not depend on EF Core, HTTP or RabbitMQ implementation.

### Infrastructure

Contains:

- EF Core DbContext
- Entity configurations
- Repositories
- migrations
- messaging implementation
- object storage implementation
- external service adapters

## 3. Dependency direction

```text
Api
 ↓
Application
 ↓
Domain

Infrastructure → Application/Domain
```

Domain must not depend on Infrastructure.

## 4. DTO rule

Do not expose EF entities directly from API.

Use:

```text
CreateXRequest
UpdateXRequest
XResponse
XListItemResponse
```

## 5. Repository rule

Do not create a generic repository just because it looks reusable.

Prefer domain/use-case focused abstractions when a repository abstraction is needed.

## 6. Entity rule

One service's entity is private to that service.

Other services store external IDs, not copied writable ownership.

## 7. Query optimization

Use projection:

```csharp
.Select(x => new XListItemResponse { ... })
```

rather than loading a large graph and mapping after the database query when the use case does not require it.

## 8. Transactions

A use case that changes several tables in the same service may use one local DB transaction.

Cross-service consistency uses events/outbox, not distributed transactions.

## 9. Logging

Use structured logs:

```text
RequestId
CorrelationId
UserId
Service
Endpoint
EntityId
Action
DurationMs
```

Do not log passwords, access tokens, refresh tokens, document secrets, or unnecessary personal data.

## 10. Dockerfile rule

Use multi-stage builds:

```text
SDK image → restore/build/publish
       ↓
runtime ASP.NET image → production
```

Keep images small and do not ship the full SDK as the runtime image.

## 11. Gateway source

Gateway should contain routing and edge concerns only:

```text
Routing
Authentication at edge
Correlation ID
Rate limiting if required
CORS if applicable
Request size limits
```

Do not put business logic in Gateway.
