# Flow 4 — Execution, tracking and horse condition

- Run: `UI-FLOW-3-5-6-REPAIR-20261029-01`
- Trip: `TRP-20261029-018` / `5d1e9a28-f405-449f-8409-35c45ad33e71`
- Actor: assigned Driver demo account.
- Movement is synthetic UI activity. OSM points were manually selected; there was no GPS device feed or real shipment.

## Step-by-step FE record

| # | Role / FE screen | Action and data | Observed result |
|---|---|---|---|
| 1 | Driver → **Execution** | Selected assigned Trip `TRP-20261029-018` under My assigned trip and loaded the timeline. Started execution from that selected record; did not type a second Trip number. | FE initialized execution for the selected Trip. |
| 2 | Driver → departure OSM picker | Searched the map and manually selected `10.7749592, 106.7164077`; clicked **Record departure**. | FE showed `DEPARTED` and added the Departure movement event. |
| 3 | Driver → destination OSM picker | Manually selected `10.7749171, 106.7161931`; clicked **Record arrival**. | FE showed `ARRIVED`; timeline displayed both Departure and Destination. |
| 4 | Manager → **Handover** timeline | Loaded Trip 018 after arrival. | Timeline included both movement events; the same steps are referenced by Flow 6 before customer acceptance. |

## Result and limits

**PASS.** Execution started from the assigned Trip and recorded departure/arrival through OSM selections. These map points are manual inputs, not live GPS. Full Handover timeline and completion evidence follows in [Flow 6](flow-06.md).

