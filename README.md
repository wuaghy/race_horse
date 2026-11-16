# EquiRoute backend

Backend workspace for the EquiRoute cross-border horse transport app. The canonical, source-audited documentation is in [../../docs/README.md](../../docs/README.md); this file only covers backend startup and directory orientation.

## Stack

- .NET 10 ASP.NET Core services behind a YARP gateway.
- SQL Server 2022 with eight service-owned logical databases; RabbitMQ for asynchronous events.
- Identity and Booking use SQL client stores. Compliance, Planning, Tracking, Incident, Handover and Notification use EF Core.
- Compose also starts MinIO and initializes buckets, but current document/attachment/signature adapters use local filesystem volumes.

## Run locally

From this directory, prepare `.env` from `.env.example`, replace development secrets, then start the Compose stack:

~~~powershell
if (-not (Test-Path .env)) { Copy-Item .env.example .env }
# Edit .env before starting the services.
docker compose up -d
~~~

Gateway: `http://localhost:8080`. The React client lives in `../../FE/horse_race`; see the [frontend guide](../../docs/architecture/frontend.md). Swagger UI and generated service OpenAPI documents are enabled only in Development; the Gateway UI is at `/swagger` when that environment is active.

Compose applies the T-SQL bootstrap and additive scripts in `scripts/init-sql/`. Demo seed data is opt-in with `SEED_DEMO_DATA=true`. Detailed topology, service ownership, contracts and known discrepancies are in the [documentation index](../../docs/README.md).
