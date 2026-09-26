# API Examples

## Login

### Request

```http
POST /api/v1/auth/login
Content-Type: application/json
```

```json
{
  "email": "customer@example.com",
  "password": "********"
}
```

### Response

```json
{
  "code": 0,
  "message": "Login successful.",
  "data": {
    "accessToken": "jwt",
    "refreshToken": "opaque-refresh-token",
    "expiresAt": "2026-09-21T10:00:00Z",
    "user": {
      "id": "uuid",
      "fullName": "Customer",
      "role": "CUSTOMER"
    }
  },
  "errors": null,
  "meta": {
    "requestId": "uuid",
    "timestamp": "2026-09-21T09:00:00Z"
  }
}
```

## Create request

```http
POST /api/v1/transport-requests
Authorization: Bearer <access-token>
Content-Type: application/json
```

```json
{
  "originLocationId": "uuid",
  "destinationLocationId": "uuid",
  "requestedDepartureAt": "2026-10-20T02:00:00Z",
  "requestedArrivalAt": "2026-10-22T10:00:00Z",
  "preferredTransportMode": "AIR",
  "specialRequirements": "Temperature-controlled handling"
}
```

## Submit request

```http
POST /api/v1/transport-requests/{id}/submit
Authorization: Bearer <access-token>
```

Body can be empty.

## Approve request

```http
POST /api/v1/transport-requests/{id}/approve
Authorization: Bearer <access-token>
Content-Type: application/json
```

```json
{
  "note": "Approved for operational planning"
}
```

## Record checkpoint event

```http
POST /api/v1/trips/{tripId}/events
Authorization: Bearer <access-token>
Content-Type: application/json
```

```json
{
  "eventType": "ARRIVED_CHECKPOINT",
  "checkpointId": "uuid",
  "eventTime": "2026-10-20T06:30:00Z",
  "latitude": 10.7765,
  "longitude": 106.7009,
  "note": "Arrived safely"
}
```

## Record horse health

```http
POST /api/v1/trip-horses/{horseId}/health-logs
Authorization: Bearer <access-token>
Content-Type: application/json
```

```json
{
  "condition": "STABLE",
  "stressLevel": "LOW",
  "temperature": 37.8,
  "appetite": "Normal",
  "observation": "Stable during checkpoint"
}
```
