# Flow 3 — Lập tuyến, phân công và READY

- Run: `UI-FLOW-3-5-6-REPAIR-20261029-01`
- Request: `REQ-20261029-C16B57FF`
- Order: `ORD-20261029-9320D823` / `0bec6ada-1531-4606-a636-af489f638035`
- Trip: `TRP-20261029-018` / `5d1e9a28-f405-449f-8409-35c45ad33e71`
- Route plan: `a35b75be-6c87-4e20-b05c-974dd2d55664`
- Actor: Customer demo submitted the request; Logistics Manager performed review and route setup in the FE.

## Step-by-step FE record

| # | FE screen/action | Input / observed result |
|---|---|---|
| 1 | Customer → **Transport requests** → create and submit | Created synthetic request `REQ-20261029-C16B57FF` for Silver Comet, using saved synthetic Ho Chi Minh pickup and delivery locations; submitted for Manager review. No real shipment was created. |
| 2 | Manager → **Transport requests** → open submitted request → approve | First approval attempt exposed a contract mismatch: API requires `reason`, while FE treated it as optional for approval; API returned 400. FE was fixed to submit an audit reason when the Manager leaves the optional note blank. Retried the same request; approval succeeded and Order `ORD-20261029-9320D823` was created. |
| 3 | Manager → **Trips, routes & assignments** → select Order/Trip | Booking event and FE could request Trip creation concurrently, which previously caused a duplicate `OrderId` 500. Planning now catches the unique-key race and returns the Trip already created for that Order. FE loaded `TRP-20261029-018`. |
| 4 | **Create route plan**; add route leg and checkpoints | Created route plan `a35b75be-6c87-4e20-b05c-974dd2d55664`. Added synthetic road leg from saved pickup `LOCAL-6S-20261025-191344 Pickup` to `LOCAL-6S-20261025-191344 Delivery`, 15 km, planned 2026-11-05 09:00–12:00; added departure/destination checkpoints. FE confirmed create/add operations. |
| 5 | Assign vehicle | Initial synthetic vehicle candidate was rejected for schedule conflict. Selected `TEST-LOCAL-001` / `09513506-4589-47d5-84b5-4b6be79e6743`; FE confirmed resource assignment. |
| 6 | Assign Driver and Escort | Selected demo account `Demo Driver & Escort` (`33333333-3333-4333-8333-333333333333`) for Driver and Escort. Historical assignment conflicts from archived routes had previously blocked this; overlap validation was corrected to consider only active route assignments and Trips that can still conflict. Both roles are present on the final active route. |
| 7 | Assign horse and compartment | Assigned Silver Comet `44444444-4444-4444-8444-444444444441` with synthetic stall `88ffb644-624a-47ff-9c7c-9650cfeb1e00`. |
| 8 | Submit, approve and activate route | Version 1 progressed `DRAFT → PENDING_APPROVAL → APPROVED → ACTIVE` in the FE. Flow 5 later introduced an emergency revision; its missing carried assignments were repaired on version 3, recorded in [Flow 5](flow-05.md). |
| 9 | **Trip readiness** / readiness panel | After route v3 had the vehicle, Driver, Escort, horse and stall assignments, FE displayed `No blockers reported.` Trip showed compliance ready. Confirmed `READY` before execution; READY represented the pre-departure gate only. |

## Result

**PASS.** The FE completed request approval, Trip/route setup, assignments and the READY gate. The route history retains older versions; version 3 is the active, fully assigned route after Flow 5's route change. No real transport was started.

