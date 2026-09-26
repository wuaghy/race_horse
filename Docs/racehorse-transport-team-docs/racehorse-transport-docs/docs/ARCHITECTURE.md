# System Architecture

## 1. Architecture overview

```text
                    React Native Mobile
                           │
                     HTTPS + JWT
                           │
                           ▼
                    ┌─────────────┐
                    │ API Gateway │
                    │    YARP     │
                    └──────┬──────┘
                           │
        ┌──────────────────┼──────────────────┐
        │                  │                  │
        ▼                  ▼                  ▼
    Identity            Booking          Compliance
        │                  │                  │
   IdentityDb         BookingDb        ComplianceDb
        │                  │                  │
        └──────────────┬───┴──────────────────┘
                       │
                  RabbitMQ Events
                       │
        ┌──────────────┼────────────────────┐
        ▼              ▼                    ▼
     Planning       Tracking             Incident
     PlanningDb     TrackingDb           IncidentDb
        │              │                    │
        └──────────────┼────────────────────┘
                       ▼
                    Handover
                   HandoverDb
                       │
                       ▼
                  Notification
                 NotificationDb
```

## 2. Microservice boundaries

| Service | Owns | Does not own |
|---|---|---|
| Identity | User, Role, Refresh Token | Customer domain, Horse |
| Booking | Customer, Horse, Request, Order | Route, GPS, Document review |
| Compliance | Regulation, Documents, Clearance | Horse master data |
| Planning | Trip, Route, Vehicle, Stall, Flight, Assignment | Horse master data |
| Tracking | Trip Events, GPS, Health Logs | Route definition |
| Incident | Incidents, Attachments | Route version data |
| Handover | Handover, Cost, Revenue, Completion | Trip master data |
| Notification | Notification, Device Token | Business domain state |
| Gateway | Routing, cross-cutting edge concerns | Business data |

## 3. Database per service

Each service owns its schema/database. A development machine may run all logical databases inside one SQL Server container, but service-level ownership remains separate.

Microsoft's .NET guidance describes database-per-microservice/data sovereignty as a core microservice principle: each service owns its data and other services should access it through APIs or messaging rather than direct database access.

References:

- https://learn.microsoft.com/en-us/dotnet/architecture/microservices/architect-microservice-container-applications/data-sovereignty-per-microservice
- https://learn.microsoft.com/en-us/dotnet/architecture/cloud-native/distributed-data

## 4. Synchronous vs asynchronous communication

Use synchronous HTTP through Gateway or internal service API when the caller needs an immediate answer.

Use RabbitMQ event when:

- another service must react independently;
- a state change should notify multiple subscribers;
- strong synchronous coupling is unnecessary;
- eventual consistency is acceptable.

Do not create a chain such as:

```text
Gateway → Booking → Compliance → Planning → Tracking
```

for a single HTTP request unless the immediate result truly requires it.

Prefer:

```text
Booking
  → RequestApproved event
      → Compliance
      → Planning
```

## 5. Transaction boundary

A database transaction is allowed only inside one service/database.

Do not try to implement distributed SQL transaction across services.

For cross-service workflows, use events and idempotent consumers.

## 6. JWT architecture

```text
React Native
   ↓ login
Identity
   ↓
Access Token + Refresh Token
   ↓
Gateway
   ↓
Service
```

Claims should include at least:

```text
sub
role
iss
aud
iat
exp
jti
```

Each service validates the bearer token needed for its own endpoints. Gateway validation is not the only authorization layer.

## 7. API Gateway

The mobile client uses one public base URL:

```text
https://api.example.com
```

Gateway routes to services.

Example:

```text
/api/v1/auth/*          → Identity
/api/v1/horses/*        → Booking
/api/v1/transport/*    → Booking
/api/v1/documents/*     → Compliance
/api/v1/clearance/*    → Compliance
/api/v1/trips/*         → Planning / Tracking according to route
/api/v1/incidents/*    → Incident
/api/v1/handovers/*    → Handover
/api/v1/notifications/* → Notification
```

## 8. Resilience requirements

Minimum:

- HTTP timeout
- retry only for safe/transient operations
- RabbitMQ consumer retry
- dead-letter queue
- idempotent event handlers
- correlation ID
- structured logging

Do not blindly retry a command that may already have changed state.

## 9. Docker deployment rule

Development:

```text
Docker Compose
├── gateway
├── identity
├── booking
├── compliance
├── planning
├── tracking
├── incident
├── handover
├── notification
├── sqlserver
├── rabbitmq
└── object-storage
```

Production should use managed database/message/object storage where practical. Application containers should be stateless.

## 10. Current stack baseline

At project creation time this documentation targets .NET 10 LTS. Microsoft lists .NET 10 as supported until November 2028.

Reference:

- https://learn.microsoft.com/dotnet/core/releases-and-support

## 11. Architecture references

- Microsoft .NET Microservices Architecture: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/
- Data sovereignty per microservice: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/architect-microservice-container-applications/data-sovereignty-per-microservice
- Multi-container microservice design: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/multi-container-microservice-net-applications/microservice-application-design
- JWT bearer authentication: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication
