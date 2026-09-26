# Testing Strategy

## 1. Test pyramid

```text
           E2E
          /   \
      Integration
       /        \
     Unit       Contract
```

## 2. Backend unit tests

Focus on:

- state transitions
- validation
- permission rules
- capacity checks
- scheduling conflicts
- readiness calculation
- on-time calculation
- route change impact rules

## 3. Integration tests

Per service:

- API + real test database
- EF Core mappings
- command behavior
- authorization

For messaging:

- producer event shape
- consumer idempotency
- retry behavior

## 4. Contract tests

Verify that API responses match OpenAPI/schema expectations and event envelopes remain compatible.

## 5. Frontend tests

Focus on:

- form validation
- query/mutation behavior
- auth refresh behavior
- screen states
- navigation guards
- permission-based rendering

## 6. End-to-end critical paths

### E2E-01 Request to approval

```text
Login Customer
→ create horse
→ create request
→ submit
→ Login Manager
→ approve
→ verify Order
```

### E2E-02 Compliance

```text
Upload document
→ review
→ approve
→ clearance
→ readiness
```

### E2E-03 Planning

```text
Order
→ Trip
→ route
→ resources
→ READY
```

### E2E-04 Execution

```text
READY
→ depart
→ checkpoint
→ GPS
→ health log
→ arrive
```

### E2E-05 Incident

```text
Incident
→ notification
→ route change
→ approval
→ active new route
```

### E2E-06 Handover

```text
Arrived
→ handover
→ horse acceptance
→ complete
```

## 7. Test data

Use deterministic test fixtures:

```text
Customer A
Horse A/B
Vietnam → Japan regulation
Vehicle A
Trip A
Route Version 1
```

Do not use uncontrolled production-like personal data.
