#!/bin/bash
set -e

echo "Waiting for SQL Server to be ready..."
for i in {1..60}; do
    if /opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -Q "SELECT 1" &>/dev/null; then
        echo "SQL Server is ready."
        break
    fi
    echo "SQL Server is not ready yet... waiting (attempt $i/60)"
    sleep 2
done

echo "Creating databases..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -i /scripts/00_create_databases.sql

echo "Running DDL scripts for 8 microservices..."
for file in /scripts/0[1-8]_*.sql; do
    echo "Executing $file..."
    /opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "${MSSQL_SA_PASSWORD}" -C -i "$file"
done

echo "Database initialization completed successfully!"
