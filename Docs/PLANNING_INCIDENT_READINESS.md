# Incident readiness projection

Incident remains the source of truth. Planning and Handover consume its `Incident.IncidentCreated` and `Incident.IncidentResolved` outbox events into local `TripIncidentFlags` / `HandoverIncidentFlags` read models. Unresolved `HIGH` and `CRITICAL` incidents block Planning trip readiness and Handover completion; resolving the incident clears the blocker after the resolution event is applied.

The projection is eventually consistent with RabbitMQ. The readiness and handover completion checks use the latest delivered event state. Deployments must run the additive SQL scripts `12_planning_incident_projection.sql` and `13_handover_incident_projection.sql`; the repository init script applies both automatically.
