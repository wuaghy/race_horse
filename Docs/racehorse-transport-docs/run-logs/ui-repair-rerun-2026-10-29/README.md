# UI repair rerun — all six scopes

- Run ID: `UI-FLOW-3-5-6-REPAIR-20261029-01`
- Date: 2026-10-29 (local demo environment)
- Interface: EquiRoute FE at `http://localhost:5173`; actions below were carried out through the FE.
- Scope: one coordinated log set for Flows 1–6. Request creation is recorded as Flow 1; readiness is rechecked for Trip 018 under Flow 2; route planning, execution, incident/route revision, and handover completion are documented under Flows 3–6.
- Main record chain: `REQ-20261029-C16B57FF` → `ORD-20261029-9320D823` (`0bec6ada-1531-4606-a636-af489f638035`) → `TRP-20261029-018` (`5d1e9a28-f405-449f-8409-35c45ad33e71`).
- Final observed FE state: Handover `HND-20261029-ec046acb` `COMPLETED`; Trip `COMPLETED`; Order `COMPLETED` after FE refresh.

All locations, schedule values, horse observations, incidents, and signature evidence in this run are synthetic demo data. No real horse shipment, inspection, GPS-device feed, disruption, or customer signature took place. Coordinates were selected manually from OSM and are not real-time GPS.

The earlier [six-scope FE run](../ui-rerun-2026-10-29/README.md) is retained as historical evidence and uses a separate record chain (`TRP-20261029-017`). It is not merged into this run. The repair chain in this folder uses Trip 018. Flow 2 here records the readiness check on Trip 018; the completed clearance case `CLR-20261029-1323` in the earlier run belongs to Trip 017 and is not represented as Trip 018's clearance.

| Flow | Latest result for this log set | Record / boundary |
|---|---|---|
| 1 — Request / Order | PASS | `REQ-20261029-C16B57FF` → `ORD-20261029-9320D823` → Trip 018 |
| 2 — Compliance / clearance | PASS for readiness recheck; no new clearance lifecycle | Trip 018 showed compliance ready/no blockers. `CLR-20261029-1323` is historical Trip 017 evidence only. |
| 3 — Route / assignment | PASS | Route plan Trip 018, version 3 active, readiness clear |
| 4 — Execution / tracking | PASS | Trip 018 manually departed/arrived at OSM-selected points |
| 5 — Incident / route change | PASS | `INC-20261029-0002` resolved; corrected route version 3 active |
| 6 — Handover / completion | PASS | Handover, Trip and Order 018 all `COMPLETED` |

## Repairs included in this rerun

1. Planning’s overlap checks now ignore inactive/archived route assignments and completed/cancelled Trips; duplicate horse assignment can update the stall on the same route leg.
2. Booking event/FE Trip creation is idempotent when both paths race for one Order.
3. FE Manager approval supplies an audit reason when the optional note is blank; incident resolution uses an inline form; OSM search no longer submits the parent execution form; Customer Handover loading no longer depends on a protected Planning Trip read.
4. FE displays Booking Order status alongside Handover. Handover completion emits `Handover.HandoverCompleted`; Planning completes the Trip and publishes `Planning.TripCompleted`; Booking consumes it and completes the Order.
5. The FE dev server had stopped during Flow 6. It was restarted; the Gateway was restarted after Handover container replacement to refresh service discovery. No database volumes were removed or reset.

## Detailed records

- [Flow 1 — Request and Order](flow-01.md)
- [Flow 2 — Compliance readiness and clearance](flow-02.md)
- [Flow 3 — Route planning and READY](flow-03.md)
- [Flow 4 — Execution and tracking](flow-04.md)
- [Flow 5 — Incident and emergency route change](flow-05.md)
- [Flow 6 — Acceptance, Handover and completion](flow-06.md)

