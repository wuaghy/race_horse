# Database Files

`racehorse_microservices.dbml` is a logical DBML diagram for dbdiagram.io.

Important:

- Tables are prefixed by service/database name for visualization.
- Cross-service UUIDs intentionally do not have foreign keys.
- Inside a service, normal relational FKs are used.
- Physical deployment may place all logical databases in one SQL Server instance during development.
- Production may use managed or separated database infrastructure without changing service ownership.
