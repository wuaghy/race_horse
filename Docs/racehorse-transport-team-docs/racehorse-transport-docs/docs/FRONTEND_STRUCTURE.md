# React Native Source Architecture

## 1. Folder structure

```text
mobile/
└── src/
    ├── app/
    │   ├── navigation/
    │   ├── providers/
    │   └── App.tsx
    │
    ├── core/
    │   ├── api/
    │   ├── auth/
    │   ├── storage/
    │   ├── errors/
    │   ├── permissions/
    │   ├── notifications/
    │   └── config/
    │
    ├── components/
    │   ├── Button/
    │   ├── Input/
    │   ├── StatusBadge/
    │   ├── EmptyState/
    │   ├── Loading/
    │   └── ErrorView/
    │
    ├── features/
    │   ├── auth/
    │   ├── horses/
    │   ├── transportRequests/
    │   ├── compliance/
    │   ├── planning/
    │   ├── tracking/
    │   ├── incidents/
    │   ├── handover/
    │   ├── notifications/
    │   └── profile/
    │
    ├── hooks/
    ├── utils/
    ├── types/
    └── constants/
```

## 2. Feature structure

Example:

```text
features/tracking/
├── api/
│   └── trackingApi.ts
├── hooks/
│   ├── useTrip.ts
│   ├── useTripTimeline.ts
│   └── useHorseHealth.ts
├── screens/
│   ├── TripListScreen.tsx
│   ├── TripDetailScreen.tsx
│   └── TripTrackingScreen.tsx
├── components/
│   ├── TripStatusCard.tsx
│   ├── TripTimeline.tsx
│   └── HorseHealthCard.tsx
├── types/
└── index.ts
```

## 3. Server state

Use TanStack Query for data obtained from backend:

- requests
- horses
- documents
- trips
- timeline
- incidents
- handovers

TanStack Query supports React Native and provides cache/refetch patterns appropriate for server state.

Reference:

- https://tanstack.com/query/latest/docs/framework/react/react-native

## 4. Local state

Use local component state or a lightweight client-state store for:

- modal open/close
- temporary form state
- navigation/UI state
- non-server preferences

Do not duplicate the entire backend cache into a global store.

## 5. API client

All HTTP must pass through:

```text
core/api/apiClient.ts
```

Responsibilities:

- base URL
- JSON serialization
- auth header
- request ID
- error normalization
- refresh token flow
- timeout

Screens must not call `fetch()`/Axios directly.

## 6. Query/mutation pattern

```text
Screen
 ↓
hook
 ↓
feature api
 ↓
core api client
 ↓
Gateway
```

Example:

```text
useTrip(tripId)
useSubmitRequest()
useApproveRequest()
```

## 7. Navigation

Navigation belongs to `app/navigation`.

Feature screens are registered there; features must not create independent top-level NavigationContainers.

## 8. Authentication flow

```text
LoginScreen
 ↓
login mutation
 ↓
Identity
 ↓
accessToken + refreshToken
 ↓
secure storage
 ↓
authenticated navigator
```

On 401:

```text
refresh
 ↓
retry once
 ↓
if failed → clear auth → LoginScreen
```

## 9. Screen states

Every API-driven screen must define:

```text
Loading
Success
Empty
Error
Refreshing
Submitting
Unauthorized/Forbidden
```

## 10. Role-based navigation

Navigation may hide inaccessible screens for UX, but backend remains the authority.

Example:

```text
CUSTOMER
 ├── Home
 ├── Horses
 ├── Requests
 ├── Trips
 └── Notifications

DRIVER
 ├── Today
 ├── Active Trip
 ├── Checkpoints
 ├── Horse Health
 └── Incidents
```

## 11. React Native references

- Networking: https://reactnative.dev/docs/network
- TanStack Query React Native: https://tanstack.com/query/latest/docs/framework/react/react-native
