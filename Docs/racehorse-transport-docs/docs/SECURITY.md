# Security Rules

## 1. Authentication

Use JWT Bearer access tokens issued by Identity.

Access token must be short-lived relative to refresh token lifetime according to the security policy chosen by the project.

Use refresh-token rotation/revocation strategy appropriate to the deployment.

## 2. Authorization

Always check:

```text
Who is the user?
What role do they have?
What resource are they accessing?
Does this state transition allow the action?
```

Example:

```text
CUSTOMER
→ GET /trips/{tripId}
→ verify trip belongs to customer's order
```

## 3. File security

For uploaded documents/images:

- validate MIME/type
- validate extension
- validate size
- generate storage object key
- never trust client filename as storage path
- prevent executable content where inappropriate
- authorize download access

## 4. Secrets

Never commit:

- JWT signing secrets
- refresh token secrets
- database passwords
- RabbitMQ credentials
- object storage keys
- push notification keys

Use environment variables or a secrets manager.

## 5. Logging

Never log:

- password
- access token
- refresh token
- authorization header
- sensitive document contents

## 6. API security

- HTTPS outside local development
- validate JWT issuer/audience/signature/expiration
- server-side validation
- request body size limits
- appropriate rate limiting for auth endpoints
- CORS only where actually needed

## 7. Mobile storage

Use platform secure storage for refresh tokens and other sensitive credentials.

## 8. Business authorization examples

### Customer

Can only manage their own horses, requests, trips, documents and handovers where allowed.

### Driver/Escort

Can update only assigned trip/leg/horse records.

### Transport Specialist

Can review compliance data but not approve a logistics request unless explicitly authorized.

### Coordinator

Can manage assigned operational resources but cannot approve business decisions reserved for Logistics Manager.

### Logistics Manager

Can approve request/route changes within defined policy.
