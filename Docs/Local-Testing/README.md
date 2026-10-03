# Local API Testing

This guide covers the local Identity and Booking APIs delivered for tasks T01-T05. The React Native client is not required for API testing.

## Start the Stack

Start Docker Desktop first. In PowerShell, open the repository root and run:

```powershell
if (-not (Test-Path .env)) { Copy-Item .env.example .env }
$bytes = New-Object byte[] 48
[Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
$env:JWT_SIGNING_KEY = [Convert]::ToBase64String($bytes)
$env:SEED_DEMO_DATA = 'true'
$env:DEMO_DATA_PASSWORD = 'HorseDev2026!'
docker compose up -d --build
docker compose ps
```

The process-level `JWT_SIGNING_KEY` overrides the placeholder in `.env`; the same key is passed to Identity, Booking, and Gateway. Keep this PowerShell session open when issuing further Compose commands so Compose receives the same key. The generated key is ephemeral; create a new one when starting a fresh local stack/session. Never commit `.env` or use these demo values outside a disposable development environment.

`SEED_DEMO_DATA` defaults to `false`. Set it to `true` before the first startup to create the sample Booking records and hashed Identity demo accounts. Seeding is idempotent and does not reset existing records. `DEMO_DATA_PASSWORD` can be changed; it must contain at least 12 characters. Do not enable demo seeding in a shared or production environment.

To start without the sample data, set `$env:SEED_DEMO_DATA = 'false'` and still provide a JWT key before `docker compose up -d --build`.

Check readiness and startup failures with:

```powershell
docker compose ps
docker compose logs --tail 50 db-init identity-api booking-api gateway-api
Invoke-RestMethod http://localhost:8080/health
```

Swagger UI is available at:

| API | URL | Use |
|---|---|---|
| Gateway | `http://localhost:8080/health` | Public entrypoint health check |
| Identity | `http://localhost:8081/swagger` | Register/login/refresh/logout/me |
| Booking | `http://localhost:8082/swagger` | Customer, horse, request, and manager operations |

Swagger UI is enabled only in the services' Development environment. The Gateway remains the application-facing API at port 8080; direct Identity and Booking ports are for local Swagger testing.

## Demo Accounts

When demo seeding is enabled:

| Role | Email | Password |
|---|---|---|
| Customer | `customer.demo@racehorse.local` | Value of `DEMO_DATA_PASSWORD` |
| Logistics manager | `manager.demo@racehorse.local` | Value of `DEMO_DATA_PASSWORD` |

The local fallback password is `HorseDev2026!`. Override it before Compose startup if desired. Demo accounts are reset to that password when the Identity API starts with demo seeding enabled.

## Sample Booking Data

The Booking seed creates a profile linked to the demo customer plus:

| Record | Stable identifier | Initial state |
|---|---|---|
| Horse | `DEMO-HORSE-001` (`Silver Comet`) | `ACTIVE` |
| Horse | `DEMO-HORSE-002` (`Morning Star`) | `ACTIVE` |
| Horse | `DEMO-HORSE-003` (`Riverstone`) | `ACTIVE` |
| Draft request | `DEMO-REQ-DRAFT-001` | `DRAFT`, one horse |
| Manager queue request | `DEMO-REQ-QUEUE-001` | `SUBMITTED`, two horses |
| Manager queue request | `DEMO-REQ-QUEUE-002` | `SUBMITTED`, one horse |
| Manager decision request | `DEMO-REQ-REVIEW-001` | `UNDER_REVIEW`, assigned to the demo manager |
| Customer follow-up request | `DEMO-REQ-INFO-001` | `NEED_INFORMATION`, with a return reason |

Origin and destination are logical Planning IDs. This local Booking slice does not verify those IDs against PlanningDb.

## Swagger Test Order

1. Open Identity Swagger at `http://localhost:8081/swagger` and execute `POST /api/v1/auth/login` with the customer demo email and password. Copy `data.accessToken` from the response.
2. Open Booking Swagger at `http://localhost:8082/swagger`, select **Authorize**, and paste the access token without adding the `Bearer ` prefix; Swagger UI supplies the scheme.
3. Call `GET /api/v1/customers/me` and `GET /api/v1/horses/` to inspect the seeded profile and horses.
4. Call `GET /api/v1/transport-requests/` to inspect all five seeded requests across draft, submitted, under-review, and needs-information states.
5. To test creating another draft, use `POST /api/v1/transport-requests/` with two distinct UUIDs for `originLocationId` and `destinationLocationId`, a future ISO 8601 `requestedDepartureAt`, and at least one owned horse ID.
6. To test the manager workflow, open Identity Swagger and log in as the logistics manager. In Booking Swagger, select **Authorize** again and replace the customer token with the manager's access token. Use `GET /api/v1/manager/transport-requests/`, then test `start-review`, `approve`, `reject`, or `request-information` with an appropriate request state.

Login request example:

```json
{
  "email": "customer.demo@racehorse.local",
  "password": "HorseDev2026!",
  "deviceInfo": "swagger-local"
}
```

New request example:

```json
{
  "originLocationId": "66666666-6666-4666-8666-666666666661",
  "destinationLocationId": "66666666-6666-4666-8666-666666666662",
  "requestedDepartureAt": "2026-11-15T08:00:00Z",
  "requestedArrivalAt": "2026-11-17T08:00:00Z",
  "preferredTransportMode": "ROAD",
  "specialRequirements": "Temperature-controlled handling",
  "notes": "Created through Swagger",
  "horseIds": ["44444444-4444-4444-8444-444444444441"]
}
```

Request submission requires origin, destination, and at least one horse. Approval creates the order and its two outbox events in the same Booking database transaction. The background publisher marks the events `PUBLISHED` only after RabbitMQ confirms them.

## Data and Reset Notes

- The seed uses fixed demo identities and stable Booking codes so it can be rerun without duplicating sample rows.
- Existing request/horse data is not reset by reseeding.
- `docker compose down -v` deletes all local databases and object-storage/message-broker data. Use it only when you explicitly want a full local reset.
- Inspect service startup and seed errors with `docker compose logs db-init identity-api booking-api`.
- The Swagger and API configuration changes live in the Identity and Booking API projects; SQL demo fixtures live in `scripts/init-sql/10_demo_data.sql` and run only when enabled.
- Stop containers without deleting local databases using `docker compose down`. Avoid `docker compose down -v` unless you intentionally want to erase the SQL, RabbitMQ, and MinIO volumes.