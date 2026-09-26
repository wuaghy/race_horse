# API Standard

## 1. Base URL

Mobile talks only to Gateway:

```text
/api/v1
```

## 2. JSON conventions

- camelCase properties
- UTF-8
- Enum as string
- UUID for IDs
- ISO 8601 for datetime
- Backend stores UTC
- Frontend converts UTC to display timezone

Example:

```json
{
  "requestedDepartureAt": "2026-10-20T02:00:00Z",
  "horseIds": ["uuid-1", "uuid-2"]
}
```

## 3. HTTP methods

| Method | Use |
|---|---|
| GET | Read |
| POST | Create / command |
| PUT | Full replacement when truly needed |
| PATCH | Partial update of editable fields |
| DELETE | Delete/archive when domain permits |

## 4. Commands

Business state transitions use command endpoints:

```http
POST /transport-requests/{id}/submit
POST /transport-requests/{id}/approve
POST /transport-requests/{id}/reject
POST /trips/{id}/depart
POST /trips/{id}/complete
POST /route-change-requests/{id}/apply
```

Do not do:

```http
PATCH /trips/{id}
{
  "status": "COMPLETED"
}
```

## 5. Success response

```json
{
  "code": 0,
  "message": "Success",
  "data": {},
  "errors": null,
  "meta": {
    "requestId": "uuid",
    "timestamp": "2026-09-21T09:00:00Z"
  }
}
```

## 6. List response

```json
{
  "code": 0,
  "message": "Success",
  "data": [],
  "errors": null,
  "meta": {
    "requestId": "uuid",
    "timestamp": "2026-09-21T09:00:00Z",
    "pagination": {
      "page": 0,
      "pageSize": 20,
      "totalItems": 87,
      "totalPages": 5
    }
  }
}
```

Pagination is zero-based.

Defaults:

```text
page = 0
pageSize = 20
maxPageSize = 100
```

## 7. Error response

```json
{
  "code": 14002,
  "message": "Vehicle is already assigned.",
  "data": null,
  "errors": [
    {
      "field": "vehicleId",
      "reason": "VEHICLE_DOUBLE_BOOKED",
      "message": "Vehicle is already assigned for this period."
    }
  ],
  "meta": {
    "requestId": "uuid",
    "timestamp": "2026-09-21T09:00:00Z"
  }
}
```

## 8. HTTP status codes

| Code | Meaning |
|---:|---|
| 200 | Success |
| 201 | Created |
| 204 | Success with no response body when appropriate |
| 400 | Malformed/invalid request |
| 401 | Missing/invalid authentication |
| 403 | Authenticated but not authorized |
| 404 | Resource not found |
| 409 | Resource/state/concurrency conflict |
| 422 | Business validation failure |
| 500 | Unexpected server error |

## 9. Query conventions

```http
GET /transport-requests?page=0&pageSize=20&status=SUBMITTED&search=REQ-2026
```

Sorting:

```text
sort=createdAt
sort=-createdAt
```

Filtering names must use API DTO property names, not database column names.

## 10. Validation

Backend validates every request even if FE already validates it.

FE validation is for UX.
BE validation is authority.

## 11. Date/time

All API datetime values are UTC ISO 8601:

```text
2026-09-21T09:30:00Z
```

Do not send display strings like:

```text
21/09/2026 16:30
```

for machine-readable fields.

## 12. Authorization

Backend authorization must consider:

1. role
2. ownership/resource scope
3. state transition

Example: Customer may have `CUSTOMER` role but only access Trips belonging to that Customer.

## 13. File upload

Use multipart/form-data.

Metadata response remains inside the standard envelope.

File binary is stored in object storage, not SQL Server.

## 14. Idempotency

Commands that may be retried by the client should support an idempotency key where needed:

```http
Idempotency-Key: <uuid>
```

Especially for:

- submission
- payment/financial posting if added later
- handover completion
- route-change apply

## 15. Request ID

Gateway generates/propagates:

```text
X-Request-ID
X-Correlation-ID
```

The same correlation ID is attached to integration events.

## 16. API versioning

Version in URL:

```text
/api/v1/...
```

Breaking changes require a new version.

## 17. OpenAPI

Every service exposes OpenAPI in development. Gateway has the public contract intended for React Native.
