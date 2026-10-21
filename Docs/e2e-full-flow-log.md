# EquiRoute full workflow run log

## Run summary

- **Run date:** 2026-10-21 (Asia/Ho_Chi_Minh)
- **Environment:** local Docker Compose backend + Vite frontend (`http://localhost:5173`)
- **Result:** completed from customer transport request through logistics planning, document compliance, driver execution, customer horse acceptance, and final handover completion.
- **Test data only:** all locations, addresses, documents, rule text, notes, coordinates, inspection, acceptance, and signature evidence below are synthetic. No real horse was transported, inspected, handed over, or signed for. Do not treat the compliance rule or document as legal guidance or official evidence.
- **Accounts:** demo customer, logistics manager, driver/escort, and admin roles were used. The demo password is intentionally not recorded here.

## Identifiers created or used

| Entity | Value |
| --- | --- |
| Customer horse | Silver Comet · `DEMO-HORSE-001` |
| Horse ID | `44444444-4444-4444-8444-444444444441` |
| Request number | `REQ-20261021-627EB361` |
| Booking order number | `ORD-20261021-D7CFE524` |
| Booking order ID | `0e664477-a6ff-40b7-88b2-cb618ea77935` |
| Trip number | `TRP-20261021-001` |
| Trip ID | `29377806-03be-479f-9097-b5e84e2ac91f` |
| Handover number | `HND-20261021-9d64c36a` |
| Handover ID | `1f19ca9d-46ba-49f7-9425-3e24e18c963a` |
| Country | Vietnam · `VN` · `d0acfc32-85c6-4ba0-b99f-ab030b75dbb8` |
| Vehicle | code `VEH-327D86C863564E8AB6D29609C120A20A` · plate `TEST-LOCAL-001` |
| Horse compartment | `STALL-10846030D68F42318EAAC5CFD4B20F4E` |
| Compliance rule | `LOCAL-E2E-VN-VN-PASSPORT-2026` · `f373fbe5-67a8-470b-8260-60138e7910fa` |
| Compliance document | `DOC-20261021-3238` · `9e939907-2fa0-4b53-8f7c-a03b5ae32b10` |

## Step-by-step record

### 0. Start local services

- Confirmed the Docker services were stopped, then started the Docker Compose stack and the frontend dev server.
- Frontend was available at `http://localhost:5173`; the APIs and database were reachable through the local gateway at `http://localhost:8080`.
- Docker's configured `desktop` credential helper was unavailable on this machine. Used an isolated Docker client config under `/tmp/codex-docker-config` to start/build the local services.

### 1. Logistics manager — add local test reference data

The customer request form had no selectable locations, so the manager first added reference data.

1. **Country**
   - Name: `Vietnam`
   - ISO code: `VN`
   - State: active
2. **Pickup location**
   - Code/name: `LOCAL-DEMO Pickup`
   - Location type: `CUSTOMER_SITE`
   - Address: `Test location only — not a real pickup address`
   - City: `Ho Chi Minh City`
   - Country: `Vietnam`
   - Latitude/longitude: blank
3. **Delivery location**
   - Code/name: `LOCAL-DEMO Delivery`
   - Address: `Test location only — not a real delivery address`
   - City: `Ho Chi Minh City`
   - Country: `Vietnam`
   - Latitude/longitude: blank
4. **Vehicle**
   - Internal vehicle code: `VEH-327D86C863564E8AB6D29609C120A20A`
   - Plate number: `TEST-LOCAL-001`
   - Vehicle type: `TRUCK`
   - Horse capacity: `2`
   - Stall capacity: `2`
   - Temperature controlled: `true`
   - Status: `AVAILABLE`
5. **Horse compartment**
   - Code: `STALL-10846030D68F42318EAAC5CFD4B20F4E`
   - Name: `Local demo single-horse compartment`
   - Capacity: `1`
   - Notes: `Test record for E2E workflow`

These addresses and resources are placeholders created solely to exercise the local workflow.

### 2. Customer — submit transport request

Opened **Transport requests → New request** and entered:

| Form field | Entered value |
| --- | --- |
| Origin location | `LOCAL-DEMO Pickup` |
| Destination location | `LOCAL-DEMO Delivery` |
| Departure | `2026-11-01 09:00` local time |
| Expected arrival | `2026-11-01 13:00` local time |
| Mode | `Road` |
| Horse | `Silver Comet · DEMO-HORSE-001` (selected from the customer's horse list) |
| Notes | `LOCAL E2E TEST — no real transport` |

Submitted successfully as `REQ-20261021-627EB361`, status `SUBMITTED`.

### 3. Logistics manager — review and approve request

Opened **Request review**, selected `REQ-20261021-627EB361`, and took the request for review (`UNDER_REVIEW`).

- Decision: approve.
- Decision reason entered: `Approved for local end-to-end test.`
- Result: approved; order `ORD-20261021-D7CFE524` created (`0e664477-a6ff-40b7-88b2-cb618ea77935`).

The UI initially allowed the approve action without a reason but the API rejected the empty reason with HTTP 400. Entering the reason above let the workflow continue.

### 4. Logistics manager — create trip and plan route

Created a trip from the approved order:

- Trip: `TRP-20261021-001` (`29377806-03be-479f-9097-b5e84e2ac91f`)
- Initial trip status: `PLANNED`

**Route plan version 1**

- Added one road leg: `LOCAL-DEMO Pickup` → `LOCAL-DEMO Delivery`.
- Planned departure: `2026-11-01 09:00` local time.
- Planned arrival: `2026-11-01 13:00` local time.
- Distance: `15 km`.
- Submitted, approved, and activated version 1.

The active version could not be edited, so created **version 2** with reason `Local E2E assignment and execution`. Recreated the same route leg on version 2, then assigned:

- Vehicle: `VEH-327D86C863564E8AB6D29609C120A20A` · `TEST-LOCAL-001`.
- Staff: `Demo Driver & Escort` (`driver.demo@racehorse.local`) as `DRIVER` and `ESCORT`.
- Horse: `Silver Comet` (`44444444-4444-4444-8444-444444444441`).
- Compartment: `STALL-10846030D68F42318EAAC5CFD4B20F4E`.

Submitted version 2, approved it, and activated it. Version 2 is the active route; version 1 is archived.

**Issue encountered:** creating the first route plan failed with a SQL foreign-key error because the parent route plan and its first version were inserted in one save operation in the wrong order. Added a parent save before adding its version in `BE/race_horse/backend/Services/Planning/Planning.Application/Services/TripPlanningService.cs`, rebuilt the local Planning API, and route-plan creation then succeeded.

### 5. Admin — create local test compliance rule and approve evidence

The local seed had no Vietnam-to-Vietnam regulation rule, so readiness stayed blocked even with an approved horse document. Added one explicitly local-only rule through the admin role:

- Rule code: `LOCAL-E2E-VN-VN-PASSPORT-2026`.
- Origin/destination: Vietnam → Vietnam.
- Required document: horse passport (`HORSE_PASSPORT`, type ID `11111111-1111-4111-8111-111111111102`).
- Required count: `1`; minimum validity days: `0`.
- Rule description/notes identify this as synthetic local test configuration, not legal guidance.

**Customer → Documents** uploaded one synthetic passport placeholder for Silver Comet:

| Document field | Entered value |
| --- | --- |
| Document type | `FEI Horse Passport & Registration` / `HORSE_PASSPORT` |
| Horse | `Silver Comet · DEMO-HORSE-001` |
| Issue date | `2026-10-21` |
| Expiry date | `2027-10-21` |
| Issuing authority | `LOCAL E2E TEST — synthetic evidence` |
| File | `/tmp/local-e2e-not-official.pdf` — contains `LOCAL E2E TEST ONLY`, `Synthetic test evidence`, and `NOT an official passport` |

Request ID and Trip ID were left empty on upload because the document belongs to the horse and the readiness evaluation links it to the trip. The uploaded document was `DOC-20261021-3238`, status `UPLOADED`.

Admin opened **Compliance Documents → Approve**. The document status became `APPROVED`.

**Readiness evaluation fields:**

| Field | Value |
| --- | --- |
| Trip ID | `29377806-03be-479f-9097-b5e84e2ac91f` |
| Origin country ID | `d0acfc32-85c6-4ba0-b99f-ab030b75dbb8` |
| Destination country ID | `d0acfc32-85c6-4ba0-b99f-ab030b75dbb8` |
| Horse IDs | `44444444-4444-4444-8444-444444444441` |
| Departure date | `2026-11-01` |

Result shown by Compliance: `Ready for departure`; `1/1` requirements met, `0` missing, `0` expired, `0` in review.

**Issue encountered:** Compliance published `Compliance.ComplianceReady`, but Planning ignored its payload because the event was wrapped in a nested `data` object with PascalCase field names. Updated `RabbitMqEventConsumerService.cs` to unwrap the nested payload and read property names case-insensitively. Rebuilt Planning and replayed the already-earned local event. Planning then set `ComplianceReady=true`; manager confirmed the trip `READY`.

### 6. Driver/escort — start and execute trip

Signed in as `driver.demo@racehorse.local`; **Assigned trips** showed `TRP-20261021-001 · READY`.

- Trip number entered in **Start execution**: `TRP-20261021-001`.
- Result: execution initialized; initial execution state `PLANNED`.
- Recorded departure: execution status `DEPARTED`; `2026-10-21T00:37:27.5501721Z`.
- Recorded arrival: execution status `ARRIVED`; `2026-10-21T00:37:27.5631939Z`.
- Location coordinates: `0, 0` for both events. These are synthetic placeholder coordinates because neither test location had coordinates; they do not represent the route.

The UI's departure/arrival buttons open blocking latitude/longitude prompts which could not be completed through the in-app browser control. Used the local Tracking API with the driver demo role to record the two test events. No checkpoint milestone or GPS trail was entered.

### 7. Logistics manager — prepare handover

Loaded the trip in **Handover**. A draft already existed:

- Handover: `HND-20261021-9d64c36a` (`1f19ca9d-46ba-49f7-9425-3e24e18c963a`).
- Initial status: `DRAFT`.

The draft UI did not expose receiver edit fields after loading an existing draft, so updated those fields using the local Handover API:

- Receiver name: `Demo Customer (LOCAL TEST)`.
- Receiver email: `customer.demo@racehorse.local`.
- Scheduled at: `2026-11-01T13:00:00Z`.
- Notes: `LOCAL E2E TEST ONLY — synthetic handover, not a real transfer.`

Added the assigned horse:

- Horse: `44444444-4444-4444-8444-444444444441`.
- Initial condition: `HEALTHY`.
- Notes: `LOCAL E2E TEST ONLY — no real inspection.`
- Inspection: `HEALTHY`, with the same local-test note.

Submitted the handover for customer acceptance; status became `PENDING_ACCEPTANCE`.

### 8. Customer — accept horse on handover

Signed in as `customer.demo@racehorse.local`, entered Trip ID `29377806-03be-479f-9097-b5e84e2ac91f` in **Handover**, and loaded `HND-20261021-9d64c36a`.

- Horse shown: Silver Comet (`44444444-4444-4444-8444-444444444441`).
- Clicked **Accept**.
- Result: horse status `ACCEPTED`; handover status `ACCEPTED`.
- Acceptance note recorded by the UI: `Accepted on delivery.`

This is a local demo acceptance only, not customer consent for an actual transfer.

### 9. Logistics manager — complete handover

Completion was blocked until a receiver signature file existed. To finish the synthetic workflow, uploaded `/tmp/local-e2e-signature-placeholder.png`, which is visibly marked `LOCAL E2E TEST ONLY`, `NOT a real signature`, and `No real horse transfer or customer consent`.

- Completion timestamp supplied: `2026-10-21T00:40:00Z`.
- Completion note: `LOCAL E2E TEST ONLY — no real horse transfer.`
- Result: handover status `COMPLETED`.
- `Handover.HandoverCompleted` was consumed by Planning, which changed trip status to `COMPLETED`.

## Final verified state

- Transport request: approved.
- Trip: `TRP-20261021-001` — `COMPLETED`.
- Active route version: v2; vehicle, staff, horse, and compartment assigned.
- Compliance: ready; passport document approved; 1/1 test requirement met.
- Execution: departure recorded, then arrival recorded.
- Customer acceptance: horse marked `ACCEPTED`.
- Handover: `HND-20261021-9d64c36a` — `COMPLETED`.

## Workarounds and limitations to address in the app

1. Location setup needs real address search/coordinates; test locations here have placeholder addresses and no real coordinates.
2. Approving a request required a reason although the UI did not indicate that requirement.
3. Planning needed an explicit parent-route-plan save before inserting a version; code fix applied in this run.
4. Planning's compliance event consumer did not handle the nested, PascalCase payload; code fix applied in this run.
5. The local seed had no VN→VN compliance rules. A synthetic rule was added; do not use it as a production regulation.
6. The browser file chooser did not attach the synthetic passport file, so the same local Customer upload endpoint was used. The UI then showed the uploaded record.
7. Driver departure/arrival prompts were not operable in the in-app browser, so the local Tracking API was used with `0,0` placeholder coordinates.
8. Existing handover drafts expose no receiver edit form; the local Handover API was used to add receiver name/email and schedule.
9. Handover completion requires a signature file. Only the clearly labelled synthetic placeholder was uploaded; no real signature was created.
10. This was an integration run of the local demo environment. It did not validate real addresses, regulations, GPS, official equine documents, or an actual handover.
11. After the API verified the handover as `COMPLETED`, the customer page heading showed `COMPLETED` but still displayed `Awaiting acceptance`; Accept/Dispute controls also remained visible. The journey panel showed no movement or handover events even though departure, arrival, and acceptance had been recorded. The completion status is correct in the service, but these UI timeline/state displays need follow-up.
