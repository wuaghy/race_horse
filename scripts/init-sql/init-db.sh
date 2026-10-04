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
        */05_tracking_db.sql) database=TrackingDb; expected_tables=4; sentinel=TripEvents ;;
        */06_incident_db.sql) database=IncidentDb; expected_tables=4; sentinel=Incidents ;;
        */07_handover_db.sql) database=HandoverDb; expected_tables=6; sentinel=HandoverRecords ;;
        */08_notification_db.sql) database=NotificationDb; expected_tables=3; sentinel=Notifications ;;
    esac

    schema_state=$(/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -d "$database" -h -1 -W -Q "SET NOCOUNT ON; SELECT CONVERT(varchar(10), COUNT(*)) + '|' + CONVERT(varchar(1), CASE WHEN OBJECT_ID(N'dbo.$sentinel', N'U') IS NULL THEN 0 ELSE 1 END) FROM sys.tables WHERE is_ms_shipped = 0;")
    table_count=${schema_state%%|*}
    sentinel_exists=${schema_state##*|}

    if [ "$table_count" = "$expected_tables" ] && [ "$sentinel_exists" = "1" ]; then
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

if [ "${SEED_DEMO_DATA:-false}" = "true" ]; then
    echo "Seeding opt-in development test data..."
    /opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -I -b -i /scripts/10_demo_data.sql
fi

echo "Database initialization completed successfully!"
