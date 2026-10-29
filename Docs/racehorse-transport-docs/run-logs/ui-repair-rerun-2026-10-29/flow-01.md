# Flow 1 — Request, Manager review and Order creation

- Run: `UI-FLOW-3-5-6-REPAIR-20261029-01`
- Request: `REQ-20261029-C16B57FF`
- Order: `ORD-20261029-9320D823` / `0bec6ada-1531-4606-a636-af489f638035`
- Trip created from the Order: `TRP-20261029-018` / `5d1e9a28-f405-449f-8409-35c45ad33e71`
- Data is synthetic local demo only.

## Step-by-step FE record

| # | Role / FE screen | Action and data | Observed result |
|---|---|---|---|
| 1 | Customer → **Transport requests** | Opened the new request form and selected Silver Comet `44444444-4444-4444-8444-444444444441`, plus saved synthetic Ho Chi Minh pickup and delivery locations. Entered the synthetic transport schedule and explanatory note. | FE loaded the saved locations and horse; no free-form OSM address was substituted for the saved booking locations. |
| 2 | Customer → **Create & submit request** | Submitted request `REQ-20261029-C16B57FF`. | FE confirmed submission; request entered Manager review. |
| 3 | Manager → **Transport requests** | Opened the submitted request and took it into review. | Request status became `UNDER_REVIEW`. |
| 4 | Manager → **Approve & create transport order** | First approval with an empty optional message returned HTTP 400 because the API requires `reason`, although the FE described the note as optional for approval. FE was fixed to send an audit reason when the Manager leaves the note blank; retried the same request. | FE confirmed approval and created Order `ORD-20261029-9320D823`. |
| 5 | Manager → **Trips, routes & assignments** | Continued from the approved Order. Booking event consumer and FE could race to create a Trip for the same Order. | Planning was repaired to return the Trip that won the unique-key race rather than returning 500. FE loaded `TRP-20261029-018`. |

## Result

**PASS.** Request → Manager review → Order → Trip completed on the FE. The approval retry and concurrent Trip creation fixes are recorded rather than hidden. These are the same records used by Flows 2–6 in this repair log set.

