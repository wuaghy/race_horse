# Flow 6 — Execution, Customer acceptance and completion

- Run: `UI-FLOW-3-5-6-REPAIR-20261029-01`
- Trip: `TRP-20261029-018` / `5d1e9a28-f405-449f-8409-35c45ad33e71`
- Order: `ORD-20261029-9320D823` / `0bec6ada-1531-4606-a636-af489f638035`
- Handover: `HND-20261029-ec046acb` / `a3eee41c-f706-4f36-976c-1ad264a6c785`
- Horse: Silver Comet `44444444-4444-4444-8444-444444444441`
- All movements, health observations, customer acceptance and signature evidence are synthetic UI data. OSM points were manually selected; no GPS device or real shipment was involved. The uploaded placeholder explicitly says it is not a real signature.

## Step-by-step FE record

| # | Role / FE screen | Action and data | Observed result |
|---|---|---|---|
| 1 | Driver demo → **Execution** | Selected assigned Trip 018 and loaded timeline. Started execution from the selected trip; did not re-enter a different trip number. | Execution record was opened for the correct Trip. |
| 2 | Driver → departure point picker | Searched OSM and selected a point manually at `10.7749592, 106.7164077`; recorded departure. | FE showed Trip `DEPARTED` and timeline contained the departure event. This is a manually selected map point, not live GPS. |
| 3 | Driver → arrival point picker | Selected a destination point manually at `10.7749171, 106.7161931`; recorded arrival. | FE showed `ARRIVED`; timeline displayed both departure and destination events. |
| 4 | Manager → **Handover** → Trip 018 → **Load trip timeline** | Continued the existing Handover `HND-20261029-ec046acb`; did not create another draft. Existing journey timeline included Flow 5 incident and both movement events. Receiver: `Synthetic Demo Receiver`; Order `ORD-20261029-9320D823`. |
| 5 | Manager → horse and condition | Silver Comet added/retained on Handover. Recorded synthetic healthy condition and note: `Synthetic UI scenario only; no physical inspection was performed.` Submitted for Customer acceptance. | FE timeline showed `HORSE_INSPECTED: HEALTHY`, then `SUBMITTED_FOR_ACCEPTANCE`; Handover entered `PENDING_ACCEPTANCE`. |
| 6 | Customer demo → **Handover** | Loaded Trip 018 and clicked **Accept** for Silver Comet; entered no real-world data. A FE bug that coupled Customer Handover read with protected Planning Trip read was fixed; Customer now loads the Handover without that unrelated request. | FE toast `Horse acceptance recorded.`; horse status `ACCEPTED`, Handover status `ACCEPTED`. |
| 7 | Manager demo → signature field | Uploaded `/tmp/scope-6-signature-flow6-retry.png`, a synthetic placeholder image labeled not a signature. The first attempt happened while FE dev server was stopped and failed before persistence. Restarted Vite and refreshed the page; retry was sent through the FE and Handover API returned HTTP 201. | FE toast `Signature uploaded.`; timeline showed `SIGNATURE_UPLOADED`; completion blocker cleared and FE showed `Ready to complete`. |
| 8 | Manager → **Complete handover** | Clicked **Complete handover** after customer acceptance and placeholder upload. | FE toast `Handover completed.`; HND `ACCEPTED → COMPLETED`; timeline showed `COMPLETED`. |
| 9 | Event consumer chain | Handover published `Handover.HandoverCompleted`. Planning log recorded receipt and `Marked Trip … completed`, then emitted `Planning.TripCompleted`; Booking consumed the event. | FE initially showed Order syncing. Clicked **Refresh Order status**; the Handover panel then displayed `Order ORD-20261029-9320D823 · COMPLETED`. |
| 10 | Manager → **Trips, routes & assignments** | Refreshed trips and selected `TRP-20261029-018`. | FE displayed `TRP-20261029-018 · COMPLETED`; detail showed `COMPLETED · Order 0bec6ada-1531-4606-a636-af489f638035 · Compliance ready`. Verified again after clean Handover API redeploy. |

## Repairs and transient failures observed

- Handover upload attempt coincided with an unavailable FE dev server / stale browser tab and Gateway service-discovery failure after container replacement. This produced `Failed to fetch`; it did not persist the first upload. Restarting Vite, reloading FE, and retrying from the file input succeeded.
- Handover Customer access now accepts the JWT email claim variants and FE does not call the protected Planning Trip endpoint for Customer Handover reads.
- Handover is completed, and Order/Trip are independently confirmed `COMPLETED`; completion was not inferred from Handover alone.
- Handover API was rebuilt from the current source after the successful run to remove temporary diagnostic logging. Its first verification page inherited a stale FE tab while Vite was stopped; Vite was started again, FE reloaded, and the clean API then returned the completed Handover/Order and completed Trip list. Docker volumes were not deleted or reset.

## Result

**PASS.** Flow 6 completed on FE through `DEPARTED → ARRIVED → Customer ACCEPTED → Handover COMPLETED → Trip COMPLETED → Order COMPLETED`. Placeholder evidence is synthetic and does not represent a legal signature or real acceptance.

