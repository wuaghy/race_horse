# Flow 5 — Incident and emergency route change

- Run: `UI-FLOW-3-5-6-REPAIR-20261029-01`
- Trip: `TRP-20261029-018` / `5d1e9a28-f405-449f-8409-35c45ad33e71`
- Incident: `INC-20261029-0002`
- Route plan: `a35b75be-6c87-4e20-b05c-974dd2d55664`
- Synthetic exercise only: no actual traffic disruption, driver report, or horse incident occurred.

## Step-by-step FE record

| # | FE screen/action | Input / observed result |
|---|---|---|
| 1 | Manager → **Incidents** → choose Trip 018 and Silver Comet | Created a `TRAFFIC_DELAY`, `MEDIUM` incident titled/described as `UI-FLOW-3-5-6-REPAIR synthetic traffic-delay exercise`; recorded on 2026-10-29 and explicitly stated there was no real disruption. No real evidence file was attached. |
| 2 | **Submit incident report** | FE created `INC-20261029-0002`, status `OPEN`, linked to Trip 018. |
| 3 | **Resolve** on incident | The old UI used `window.prompt`, which did not expose a usable resolution field in this browser. Replaced it with an inline resolution form. Entered a synthetic close-out note and submitted from FE; incident changed to `RESOLVED`, which is also shown on the Handover journey timeline. |
| 4 | Manager → **Route changes** → select Trip 018 | FE loaded route plan `a35b75be-6c87-4e20-b05c-974dd2d55664`. |
| 5 | **Propose route version** | Reason: synthetic traffic delay exercise; estimated duration +40 minutes; additional cost USD 0. FE created version 2 `3c326cbf-7540-4353-a67f-8d478c99e893` (`DRAFT`). |
| 6 | Add leg to version 2 | FE would not submit an empty route version (`Cannot submit a route plan version with no route legs`). Added synthetic road leg, sequence 1, saved pickup → saved delivery, 15 km, planned 2026-11-05 09:00–12:40. |
| 7 | Submit → approve → activate version 2 | FE confirmed each transition. Version 2 became `ACTIVE`; readiness then surfaced that changing the active version did not carry over the previous vehicle, staff, horse and stall assignments. This blocker was not reported as pass. |
| 8 | Create correction version 3 and add the active route leg | Created version `6a7128ad-d9dc-4b95-8d4e-d66c8634c37e` with a synthetic pickup-to-delivery road leg. |
| 9 | Assign resources on version 3 | Assigned `TEST-LOCAL-001`, Driver and Escort demo account, and Silver Comet. Initial horse assignment had no stall, so readiness continued to report `horse has no stall`. BE assignment was repaired to upsert the stall on the existing horse+leg assignment; then selected synthetic stall `88ffb644-624a-47ff-9c7c-9650cfeb1e00` in FE and saved. |
| 10 | Submit → approve → activate version 3; reload readiness | FE showed version 3 `ACTIVE`, route history retained v1/v2, and readiness displayed `No blockers reported.` Incident remained `RESOLVED`. |

## Result

**PASS.** Incident was recorded and resolved in the FE; emergency route v2 was proposed/approved/activated, and the assignment loss it exposed was corrected by fully assigning route v3. Version 3 is the final active route for Flow 6. All incident and delay details are synthetic.

