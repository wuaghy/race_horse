# Local Development

## 1. Prerequisites

- Git
- Docker Desktop / Docker Engine
- .NET 10 SDK for local non-container development
- Node.js + package manager used by the team
- React Native environment according to the mobile target platform
- Android Studio for Android development when Android is targeted

## 2. Start infrastructure

Set a local JWT signing key in the environment before starting the application containers. Do not commit the key:

PowerShell:

```powershell
$env:JWT_SIGNING_KEY = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
```

Bash:

```bash
export JWT_SIGNING_KEY="$(openssl rand -base64 48)"
```

```bash
docker compose up -d
```

Expected infrastructure:

```text
SQL Server
RabbitMQ
Object Storage
Gateway
Identity
Booking
```

## 3. Start backend

Preferred development mode:

```bash
docker compose up -d
```

For debugging a single service locally:

```bash
dotnet run --project Services/Booking/Booking.Api
```

Ensure its dependent infrastructure is running.

## 4. Run migrations

Each service owns its migrations.

Example:

```bash
dotnet ef database update \
  --project Services/Booking/Booking.Infrastructure \
  --startup-project Services/Booking/Booking.Api
```

Use the equivalent command for each service.

## 5. Mobile environment

Example:

```text
API_BASE_URL=http://10.0.2.2:8080
```

The exact local host varies by emulator/device. Never hardcode production URLs in feature code.

## 6. Local account seeding

Seed only development accounts and explicitly document credentials in a local-only file that is ignored by Git.

Do not commit real passwords or tokens.

## 7. Health checks

Every service should expose a health endpoint in development.

Gateway should provide an aggregate or diagnostic endpoint for local testing.

The Gateway is published at `http://localhost:8080`. Identity and Booking are internal Compose services; mobile clients should call only the Gateway.

## 8. Reset environment

When schema/contracts change significantly:

```bash
docker compose down -v

docker compose up -d
```

This deletes local volumes. Never run `down -v` against production.

## 9. RabbitMQ development

When testing events manually, inspect:

- exchange
- routing key
- queue
- consumer state
- retry count
- dead-letter queue

## 10. Object storage

Do not commit uploaded files to the repository.

Use local object storage bucket for development and production object storage for deployment.
