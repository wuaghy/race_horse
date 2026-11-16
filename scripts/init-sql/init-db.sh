#!/bin/bash
set -e

echo "Waiting for SQL Server to be ready..."
for i in {1..60}; do
    if /opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -Q "SELECT 1" &>/dev/null; then
        echo "SQL Server is ready."
        break
    fi
    echo "SQL Server is not ready yet... waiting (attempt $i/60)"
    sleep 2
done

echo "Creating databases..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/00_create_databases.sql

echo "Running DDL scripts for 8 microservices..."
for file in /scripts/0[1-8]_*.sql; do
    echo "Executing $file..."
    case "$file" in
        */01_identity_db.sql) database=IdentityDb; expected_tables=4; sentinel=Roles ;;
        */02_booking_db.sql) database=BookingDb; expected_tables=7; sentinel=Customers ;;
        */03_compliance_db.sql) database=ComplianceDb; expected_tables=9; sentinel=DocumentTypes ;;
        */04_planning_db.sql) database=PlanningDb; expected_tables=16; sentinel=Countries ;;
        */05_tracking_db.sql) database=TrackingDb; expected_tables=4; sentinel=TripEvents; runtime_sentinel=TripExecutions ;;
        */06_incident_db.sql) database=IncidentDb; expected_tables=4; sentinel=Incidents ;;
        */07_handover_db.sql) database=HandoverDb; expected_tables=6; sentinel=HandoverRecords ;;
        */08_notification_db.sql) database=NotificationDb; expected_tables=3; sentinel=Notifications ;;
    esac

    schema_state=$(/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -d "$database" -h -1 -W -Q "SET NOCOUNT ON; SELECT CONVERT(varchar(10), COUNT(*)) + '|' + CONVERT(varchar(1), CASE WHEN OBJECT_ID(N'dbo.$sentinel', N'U') IS NULL THEN 0 ELSE 1 END) FROM sys.tables WHERE is_ms_shipped = 0;")
    table_count=${schema_state%%|*}
    sentinel_exists=${schema_state##*|}

    if [ "$database" = "TrackingDb" ]; then
        runtime_exists=$(/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -d "$database" -h -1 -W -Q "SET NOCOUNT ON; SELECT CONVERT(varchar(1), CASE WHEN OBJECT_ID(N'dbo.$runtime_sentinel', N'U') IS NULL THEN 0 ELSE 1 END);")
        if [ "$sentinel_exists" = "1" ] && [ "$runtime_exists" = "1" ]; then
            echo "$database schema including runtime tables is already initialized; skipping."
            continue
        fi
        if [ "$table_count" -ge "$expected_tables" ] && [ "$sentinel_exists" = "1" ]; then
            echo "$database base schema is already initialized; applying additive runtime tables later."
            continue
        fi
    fi

    if [ "$table_count" -ge "$expected_tables" ] && [ "$sentinel_exists" = "1" ]; then
        echo "$database schema is already initialized; skipping."
        continue
    fi
    if [ "$table_count" != "0" ]; then
        echo "$database has a partial or unexpected schema ($table_count tables); refusing to rerun its DDL."
        exit 1
    fi

    /opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i "$file"
done

echo "Applying Booking ownership migration..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/09_booking_identity_user_link.sql

echo "Applying additive Tracking runtime tables..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/11_tracking_runtime_tables.sql

echo "Applying additive Planning incident projection..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/12_planning_incident_projection.sql

echo "Applying additive Handover incident projection..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/13_handover_incident_projection.sql

echo "Applying additive Planning route capabilities and flight confirmation..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/14_planning_route_capabilities_flight_confirmation.sql

echo "Applying additive Handover customer ownership projection..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/15_handover_customer_ownership_projection.sql

echo "Applying additive Handover recipient contact projection..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/16_handover_recipient_contact.sql

echo "Applying Handover trip arrival gate..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/23_handover_trip_arrival.sql

echo "Applying additive Planning route duration estimates..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/17_planning_route_duration_estimates.sql

echo "Applying Booking draft idempotency migration..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/18_booking_request_idempotency.sql

echo "Applying Planning cancellation gate migration..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/19_planning_cancellation_gate.sql

echo "Applying Planning express delivery estimate migration..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/20_planning_express_duration_estimates.sql

echo "Applying Booking delivery priority migration..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/21_booking_delivery_priority.sql

echo "Applying Planning request route projection and default route migration..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/22_planning_request_route_projection.sql

echo "Applying Booking request recipient migration..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/24_booking_request_recipient.sql

echo "Applying Planning staff country authorization migration..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/33_planning_staff_route_country_authorizations.sql

echo "Applying Planning staff operating areas migration..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/34_planning_staff_operating_areas.sql

echo "Applying Booking and Planning custom pickup migration..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/25_customer_custom_pickup.sql

echo "Applying Booking horse country name migration..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/40_booking_horse_country_name.sql

echo "Applying Booking pricing and payment migration..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/41_booking_pricing_payments.sql

echo "Applying Booking distance pricing migration..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/42_booking_distance_pricing.sql

echo "Applying Booking PayOS checkout link persistence migration..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/43_booking_payos_checkout_links.sql

echo "Applying Booking manual settlement and audit migration..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/44_booking_manual_settlements.sql

echo "Applying Compliance clearance type migration..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/46_compliance_clearance_type.sql

echo "Applying Planning multi-order trip consolidation migration..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/27_planning_multi_order_trips.sql

echo "Applying Planning order display-label migration..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/32_planning_order_display_labels.sql

echo "Applying Planning customer ownership projection migration..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/45_planning_customer_user_projection.sql

echo "Applying Planning daily dispatch calendar migration..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/28_planning_daily_dispatch_calendar.sql

echo "Applying Notification SMTP email outbox migration..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/31_notification_email_outbox.sql

echo "Ensuring escort role exists..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/35_identity_escort_role.sql

echo "Applying Planning route shape migration..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/36_planning_route_shape.sql

echo "Applying Planning route templates migration..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/37_planning_route_templates.sql

echo "Applying Incident medical resolution migration..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/39_incident_medical_resolution.sql

echo "Applying Planning order payment gate migration..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/42_planning_order_payment_gate.sql

if [ "${SEED_DEMO_DATA:-false}" = "true" ]; then
    echo "Seeding opt-in development test data..."
    /opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/10_demo_data.sql
    echo "Seeding demo domestic compliance rules..."
    /opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/30_demo_compliance_rules.sql
    echo "Seeding demo route travel-time estimates..."
    /opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/26_planning_demo_route_duration_seed.sql
    echo "Seeding demo horse compartments..."
    /opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/29_planning_demo_compartments.sql
    echo "Seeding demo flights for November and December 2026..."
    /opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/38_demo_flights_nov_dec_2026.sql
fi

echo "Database initialization completed successfully!"
