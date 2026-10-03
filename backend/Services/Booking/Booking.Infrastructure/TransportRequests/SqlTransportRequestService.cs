using System.Data;
using System.Text.Json;
using Booking.Application.Customers;
using Booking.Application.TransportRequests;
using BuildingBlocks.Exceptions;
using Contracts.IntegrationEvents;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Booking.Infrastructure.TransportRequests;

public sealed class SqlTransportRequestService : ITransportRequestService
{
    private readonly string _connectionString;

    public SqlTransportRequestService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("BookingDb")
            ?? throw new InvalidOperationException("ConnectionStrings:BookingDb must be configured.");
    }

    public async Task<PageResult<TransportRequestListItem>> GetCustomerRequestsAsync(Guid identityUserId, int page, int pageSize, CancellationToken cancellationToken)
    {
        ValidatePage(page, pageSize);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        const string ownerFilter = "FROM dbo.TransportRequests r INNER JOIN dbo.Customers c ON c.Id = r.CustomerId WHERE c.IdentityUserId = @IdentityUserId";
        await using var countCommand = new SqlCommand("SELECT COUNT_BIG(*) " + ownerFilter + ";", connection);
        countCommand.Parameters.Add("@IdentityUserId", SqlDbType.UniqueIdentifier).Value = identityUserId;
        var total = (long)(await countCommand.ExecuteScalarAsync(cancellationToken) ?? 0L);

        await using var command = new SqlCommand(
            "SELECT r.Id, r.RequestNo, r.Status, r.RequestedDepartureAt, r.CreatedAt, " +
            "(SELECT COUNT(*) FROM dbo.TransportRequestHorses rh WHERE rh.RequestId = r.Id) AS HorseCount, r.VersionNo " +
            ownerFilter + " ORDER BY r.CreatedAt DESC, r.Id OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;",
            connection);
        command.Parameters.Add("@IdentityUserId", SqlDbType.UniqueIdentifier).Value = identityUserId;
        AddPaging(command, page, pageSize);
        return new PageResult<TransportRequestListItem>(await ReadListAsync(command, cancellationToken), page, pageSize, total);
    }

    public async Task<TransportRequestRecord?> GetCustomerRequestAsync(Guid identityUserId, Guid requestId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        return await ReadRequestAsync(connection, null, requestId, identityUserId, cancellationToken);
    }

    public async Task<TransportRequestRecord?> GetManagerRequestAsync(Guid requestId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        return await ReadRequestAsync(connection, null, requestId, null, cancellationToken);
    }

    public async Task<TransportRequestRecord> CreateDraftAsync(Guid identityUserId, TransportRequestInput input, CancellationToken cancellationToken)
    {
        ValidateInput(input, requireHorses: false);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var customerId = await RequireCustomerIdAsync(connection, transaction, identityUserId, cancellationToken);
            var requestId = Guid.NewGuid();
            var requestNo = "REQ-" + DateTime.UtcNow.ToString("yyyyMMdd") + "-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            await using var command = new SqlCommand(
                "INSERT INTO dbo.TransportRequests (Id, RequestNo, CustomerId, CreatedByUserId, OriginLocationId, DestinationLocationId, " +
                "RequestedDepartureAt, RequestedArrivalAt, PreferredTransportMode, SpecialRequirements, Notes, Status) " +
                "VALUES (@Id, @RequestNo, @CustomerId, @CreatedByUserId, @OriginLocationId, @DestinationLocationId, " +
                "@DepartureAt, @ArrivalAt, @Mode, @Requirements, @Notes, 'DRAFT');",
                connection,
                transaction);
            AddRequestParameters(command, input);
            command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = requestId;
            command.Parameters.Add("@RequestNo", SqlDbType.VarChar, 50).Value = requestNo;
            command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = customerId;
            command.Parameters.Add("@CreatedByUserId", SqlDbType.UniqueIdentifier).Value = identityUserId;
            await command.ExecuteNonQueryAsync(cancellationToken);
            await InsertRequestHorsesAsync(connection, transaction, requestId, customerId, input.HorseIds, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return (await GetCustomerRequestAsync(identityUserId, requestId, cancellationToken))!;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<TransportRequestRecord> UpdateDraftAsync(Guid identityUserId, Guid requestId, TransportRequestInput input, CancellationToken cancellationToken)
    {
        ValidateInput(input, requireHorses: false);
        if (input.VersionNo is null)
        {
            throw new BusinessRuleException("VersionNo is required when updating a request.", "VERSION_REQUIRED", "versionNo");
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var current = await ReadRequestAsync(connection, transaction, requestId, identityUserId, cancellationToken)
                ?? throw new NotFoundException("Transport request", requestId);
            if (current.Status is not ("DRAFT" or "NEED_INFORMATION"))
            {
                throw new BusinessRuleException("Only draft or returned requests can be edited.", "REQUEST_STATE_INVALID");
            }

            if (current.VersionNo != input.VersionNo)
            {
                throw new ConcurrencyException("Request changed. Reload it and retry.");
            }

            await using var update = new SqlCommand(
                "UPDATE dbo.TransportRequests SET OriginLocationId = @OriginLocationId, DestinationLocationId = @DestinationLocationId, " +
                "RequestedDepartureAt = @DepartureAt, RequestedArrivalAt = @ArrivalAt, PreferredTransportMode = @Mode, " +
                "SpecialRequirements = @Requirements, Notes = @Notes, UpdatedAt = SYSUTCDATETIME(), VersionNo = VersionNo + 1 " +
                "WHERE Id = @Id AND VersionNo = @VersionNo;",
                connection,
                transaction);
            AddRequestParameters(update, input);
            update.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = requestId;
            update.Parameters.Add("@VersionNo", SqlDbType.Int).Value = input.VersionNo.Value;
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw new ConcurrencyException("Request changed. Reload it and retry.");
            }

            await using (var deleteHorses = new SqlCommand("DELETE FROM dbo.TransportRequestHorses WHERE RequestId = @RequestId;", connection, transaction))
            {
                deleteHorses.Parameters.Add("@RequestId", SqlDbType.UniqueIdentifier).Value = requestId;
                await deleteHorses.ExecuteNonQueryAsync(cancellationToken);
            }

            await InsertRequestHorsesAsync(connection, transaction, requestId, current.CustomerId, input.HorseIds, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return (await GetCustomerRequestAsync(identityUserId, requestId, cancellationToken))!;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<TransportRequestRecord> SubmitAsync(Guid identityUserId, Guid requestId, Guid correlationId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var current = await ReadRequestAsync(connection, transaction, requestId, identityUserId, cancellationToken, lockForUpdate: true)
                ?? throw new NotFoundException("Transport request", requestId);
            if (current.Status is not ("DRAFT" or "NEED_INFORMATION"))
            {
                throw new BusinessRuleException("Only a draft or returned request can be submitted.", "REQUEST_STATE_INVALID");
            }

            if (current.OriginLocationId is null || current.DestinationLocationId is null || current.HorseIds.Count == 0)
            {
                throw new BusinessRuleException("Origin, destination, and at least one horse are required before submission.", "REQUEST_INCOMPLETE");
            }

            await UpdateStateAsync(connection, transaction, requestId, current.VersionNo, "SUBMITTED", identityUserId, null, correlationId, true, cancellationToken);
            await WriteAuditAsync(connection, transaction, "TransportRequest", requestId, "SUBMITTED", current.Status, "SUBMITTED", identityUserId, null, correlationId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return (await GetCustomerRequestAsync(identityUserId, requestId, cancellationToken))!;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<PageResult<TransportRequestListItem>> GetManagerQueueAsync(Guid managerUserId, int page, int pageSize, CancellationToken cancellationToken)
    {
        ValidatePage(page, pageSize);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        const string filter = "FROM dbo.TransportRequests r WHERE r.Status IN ('SUBMITTED','UNDER_REVIEW') " +
            "AND (r.AssignedManagerUserId IS NULL OR r.AssignedManagerUserId = @ManagerId)";
        await using var countCommand = new SqlCommand("SELECT COUNT_BIG(*) " + filter + ";", connection);
        countCommand.Parameters.Add("@ManagerId", SqlDbType.UniqueIdentifier).Value = managerUserId;
        var total = (long)(await countCommand.ExecuteScalarAsync(cancellationToken) ?? 0L);
        await using var command = new SqlCommand(
            "SELECT r.Id, r.RequestNo, r.Status, r.RequestedDepartureAt, r.CreatedAt, " +
            "(SELECT COUNT(*) FROM dbo.TransportRequestHorses rh WHERE rh.RequestId = r.Id) AS HorseCount, r.VersionNo " +
            filter + " ORDER BY r.SubmittedAt, r.CreatedAt OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;",
            connection);
        command.Parameters.Add("@ManagerId", SqlDbType.UniqueIdentifier).Value = managerUserId;
        AddPaging(command, page, pageSize);
        return new PageResult<TransportRequestListItem>(await ReadListAsync(command, cancellationToken), page, pageSize, total);
    }

    public async Task<TransportRequestRecord> StartReviewAsync(Guid managerUserId, Guid requestId, Guid correlationId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var current = await ReadRequestAsync(connection, transaction, requestId, null, cancellationToken, lockForUpdate: true)
                ?? throw new NotFoundException("Transport request", requestId);
            if (current.Status == "UNDER_REVIEW" && current.AssignedManagerUserId == managerUserId)
            {
                await transaction.CommitAsync(cancellationToken);
                return current;
            }

            EnsureAssignedManager(current, managerUserId);

            if (current.Status != "SUBMITTED")
            {
                throw new BusinessRuleException("Only submitted requests can enter review.", "REQUEST_STATE_INVALID");
            }

            await using var update = new SqlCommand(
                "UPDATE dbo.TransportRequests SET Status = 'UNDER_REVIEW', AssignedManagerUserId = @ManagerId, " +
                "ReviewedAt = SYSUTCDATETIME(), UpdatedAt = SYSUTCDATETIME(), VersionNo = VersionNo + 1 WHERE Id = @Id AND VersionNo = @VersionNo;",
                connection,
                transaction);
            update.Parameters.Add("@ManagerId", SqlDbType.UniqueIdentifier).Value = managerUserId;
            update.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = requestId;
            update.Parameters.Add("@VersionNo", SqlDbType.Int).Value = current.VersionNo;
            await EnsureOneRowAsync(update, cancellationToken);
            await WriteAuditAsync(connection, transaction, "TransportRequest", requestId, "UNDER_REVIEW", current.Status, "UNDER_REVIEW", managerUserId, null, correlationId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return (await GetManagerRequestAsync(requestId, cancellationToken))!;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public Task<TransportRequestRecord> RequestInformationAsync(Guid managerUserId, Guid requestId, string reason, Guid correlationId, CancellationToken cancellationToken) =>
        ReviewTransitionAsync(managerUserId, requestId, "NEED_INFORMATION", reason, correlationId, cancellationToken);

    public Task<TransportRequestRecord> RejectAsync(Guid managerUserId, Guid requestId, string reason, Guid correlationId, CancellationToken cancellationToken) =>
        ReviewTransitionAsync(managerUserId, requestId, "REJECTED", reason, correlationId, cancellationToken);

    public async Task<TransportRequestRecord> ApproveAsync(Guid managerUserId, Guid requestId, string? note, Guid correlationId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var current = await ReadRequestAsync(connection, transaction, requestId, null, cancellationToken, lockForUpdate: true)
                ?? throw new NotFoundException("Transport request", requestId);
            EnsureAssignedManager(current, managerUserId);
            if (current.Status == "APPROVED" && current.OrderId is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return current;
            }

            if (current.Status != "UNDER_REVIEW")
            {
                throw new BusinessRuleException("Only a request under review can be approved.", "REQUEST_STATE_INVALID");
            }

            var orderId = Guid.NewGuid();
            var orderNo = "ORD-" + DateTime.UtcNow.ToString("yyyyMMdd") + "-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            var now = DateTimeOffset.UtcNow;
            await using (var order = new SqlCommand(
                "INSERT INTO dbo.TransportOrders (Id, OrderNo, RequestId, ApprovedByUserId, ApprovedAt, Status) " +
                "VALUES (@Id, @OrderNo, @RequestId, @ManagerId, @ApprovedAt, 'CREATED');",
                connection,
                transaction))
            {
                order.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = orderId;
                order.Parameters.Add("@OrderNo", SqlDbType.VarChar, 50).Value = orderNo;
                order.Parameters.Add("@RequestId", SqlDbType.UniqueIdentifier).Value = requestId;
                order.Parameters.Add("@ManagerId", SqlDbType.UniqueIdentifier).Value = managerUserId;
                order.Parameters.Add("@ApprovedAt", SqlDbType.DateTime2).Value = now.UtcDateTime;
                await order.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var update = new SqlCommand(
                "UPDATE dbo.TransportRequests SET Status = 'APPROVED', ApprovedAt = @ApprovedAt, ReviewedAt = @ApprovedAt, " +
                "RejectionReason = NULL, UpdatedAt = @ApprovedAt, VersionNo = VersionNo + 1 WHERE Id = @Id AND VersionNo = @VersionNo;",
                connection,
                transaction))
            {
                update.Parameters.Add("@ApprovedAt", SqlDbType.DateTime2).Value = now.UtcDateTime;
                update.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = requestId;
                update.Parameters.Add("@VersionNo", SqlDbType.Int).Value = current.VersionNo;
                await EnsureOneRowAsync(update, cancellationToken);
            }

            await WriteAuditAsync(connection, transaction, "TransportRequest", requestId, "APPROVED", current.Status, "APPROVED", managerUserId, note, correlationId, cancellationToken);
            await WriteAuditAsync(connection, transaction, "TransportOrder", orderId, "CREATED", null, "CREATED", managerUserId, note, correlationId, cancellationToken);
            await WriteOutboxAsync(connection, transaction, new BookingRequestApprovedEvent
            {
                CorrelationId = correlationId,
                OccurredAt = now.UtcDateTime,
                Data = new BookingRequestApprovedData(requestId, current.CustomerId, current.RequestNo, orderId)
            }, "TransportRequest", requestId, correlationId, now, cancellationToken);
            await WriteOutboxAsync(connection, transaction, new BookingOrderCreatedEvent
            {
                CorrelationId = correlationId,
                OccurredAt = now.UtcDateTime,
                Data = new BookingOrderCreatedData(orderId, orderNo, requestId, current.CustomerId)
            }, "TransportOrder", orderId, correlationId, now, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return (await GetManagerRequestAsync(requestId, cancellationToken))!;
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (await GetManagerRequestAsync(requestId, cancellationToken))!;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<TransportRequestRecord> ReviewTransitionAsync(
        Guid managerUserId,
        Guid requestId,
        string newStatus,
        string reason,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new BusinessRuleException("A reason is required.", "REVIEW_REASON_REQUIRED", "reason");
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var current = await ReadRequestAsync(connection, transaction, requestId, null, cancellationToken, lockForUpdate: true)
                ?? throw new NotFoundException("Transport request", requestId);
            EnsureAssignedManager(current, managerUserId);
            if (current.Status != "UNDER_REVIEW")
            {
                throw new BusinessRuleException("Only a request under review can be returned or rejected.", "REQUEST_STATE_INVALID");
            }

            await using var update = new SqlCommand(
                "UPDATE dbo.TransportRequests SET Status = @Status, RejectionReason = @Reason, ReviewedAt = SYSUTCDATETIME(), " +
                "UpdatedAt = SYSUTCDATETIME(), VersionNo = VersionNo + 1 WHERE Id = @Id AND VersionNo = @VersionNo;",
                connection,
                transaction);
            update.Parameters.Add("@Status", SqlDbType.VarChar, 40).Value = newStatus;
            update.Parameters.Add("@Reason", SqlDbType.NVarChar, 1000).Value = reason.Trim();
            update.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = requestId;
            update.Parameters.Add("@VersionNo", SqlDbType.Int).Value = current.VersionNo;
            await EnsureOneRowAsync(update, cancellationToken);
            await WriteAuditAsync(connection, transaction, "TransportRequest", requestId, newStatus, current.Status, newStatus, managerUserId, reason.Trim(), correlationId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return (await GetManagerRequestAsync(requestId, cancellationToken))!;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<TransportRequestRecord?> ReadRequestAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        Guid requestId,
        Guid? identityUserId,
        CancellationToken cancellationToken,
        bool lockForUpdate = false)
    {
        var lockHint = lockForUpdate ? " WITH (UPDLOCK, HOLDLOCK)" : string.Empty;
        var ownerJoin = identityUserId is null ? string.Empty : " INNER JOIN dbo.Customers c ON c.Id = r.CustomerId";
        var ownerFilter = identityUserId is null ? string.Empty : " AND c.IdentityUserId = @IdentityUserId";
        await using var command = new SqlCommand(
            "SELECT r.Id, r.RequestNo, r.CustomerId, r.CreatedByUserId, r.AssignedManagerUserId, r.OriginLocationId, r.DestinationLocationId, " +
            "r.RequestedDepartureAt, r.RequestedArrivalAt, r.PreferredTransportMode, r.SpecialRequirements, r.Notes, r.Status, " +
            "r.RejectionReason, r.VersionNo, o.Id, o.OrderNo " +
            "FROM dbo.TransportRequests r" + lockHint + ownerJoin + " LEFT JOIN dbo.TransportOrders o ON o.RequestId = r.Id " +
            "WHERE r.Id = @RequestId" + ownerFilter + ";",
            connection,
            transaction);
        command.Parameters.Add("@RequestId", SqlDbType.UniqueIdentifier).Value = requestId;
        if (identityUserId is not null)
        {
            command.Parameters.Add("@IdentityUserId", SqlDbType.UniqueIdentifier).Value = identityUserId.Value;
        }

        TransportRequestRecord request;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            request = new TransportRequestRecord(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetGuid(2),
                reader.GetGuid(3),
                reader.IsDBNull(4) ? null : reader.GetGuid(4),
                reader.IsDBNull(5) ? null : reader.GetGuid(5),
                reader.IsDBNull(6) ? null : reader.GetGuid(6),
                ToUtcOffset(reader.GetDateTime(7)),
                reader.IsDBNull(8) ? null : ToUtcOffset(reader.GetDateTime(8)),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetString(11),
                reader.GetString(12),
                reader.IsDBNull(13) ? null : reader.GetString(13),
                reader.GetInt32(14),
                [],
                reader.IsDBNull(15) ? null : reader.GetGuid(15),
                reader.IsDBNull(16) ? null : reader.GetString(16));
        }

        return request with { HorseIds = await ReadHorseIdsAsync(connection, transaction, requestId, cancellationToken) };
    }

    private static async Task<IReadOnlyList<Guid>> ReadHorseIdsAsync(SqlConnection connection, SqlTransaction? transaction, Guid requestId, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            "SELECT HorseId FROM dbo.TransportRequestHorses WHERE RequestId = @RequestId ORDER BY CreatedAt, Id;",
            connection,
            transaction);
        command.Parameters.Add("@RequestId", SqlDbType.UniqueIdentifier).Value = requestId;
        var horseIds = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            horseIds.Add(reader.GetGuid(0));
        }

        return horseIds;
    }

    private static async Task<IReadOnlyList<TransportRequestListItem>> ReadListAsync(SqlCommand command, CancellationToken cancellationToken)
    {
        var items = new List<TransportRequestListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new TransportRequestListItem(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                ToUtcOffset(reader.GetDateTime(3)),
                ToUtcOffset(reader.GetDateTime(4)),
                reader.GetInt32(5),
                reader.GetInt32(6)));
        }

        return items;
    }

    private static async Task InsertRequestHorsesAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid requestId,
        Guid customerId,
        IReadOnlyList<Guid> horseIds,
        CancellationToken cancellationToken)
    {
        if (horseIds.Distinct().Count() != horseIds.Count)
        {
            throw new BusinessRuleException("A horse can only be added once to a request.", "REQUEST_HORSE_DUPLICATE", "horseIds");
        }

        foreach (var horseId in horseIds)
        {
            await using var command = new SqlCommand(
                "INSERT INTO dbo.TransportRequestHorses (RequestId, HorseId) " +
                "SELECT @RequestId, Id FROM dbo.Horses WHERE Id = @HorseId AND OwnerCustomerId = @CustomerId;",
                connection,
                transaction);
            command.Parameters.Add("@RequestId", SqlDbType.UniqueIdentifier).Value = requestId;
            command.Parameters.Add("@HorseId", SqlDbType.UniqueIdentifier).Value = horseId;
            command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = customerId;
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw new BusinessRuleException("Every selected horse must belong to the customer's profile.", "HORSE_OWNERSHIP_REQUIRED", "horseIds");
            }
        }
    }

    private static async Task UpdateStateAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid requestId,
        int versionNo,
        string status,
        Guid actorId,
        string? reason,
        Guid correlationId,
        bool clearReviewReason,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            "UPDATE dbo.TransportRequests SET Status = @Status, SubmittedAt = SYSUTCDATETIME(), " +
            "RejectionReason = CASE WHEN @ClearReviewReason = 1 THEN NULL ELSE @Reason END, " +
            "UpdatedAt = SYSUTCDATETIME(), VersionNo = VersionNo + 1 WHERE Id = @Id AND VersionNo = @VersionNo;",
            connection,
            transaction);
        command.Parameters.Add("@Status", SqlDbType.VarChar, 40).Value = status;
        command.Parameters.Add("@ClearReviewReason", SqlDbType.Bit).Value = clearReviewReason;
        command.Parameters.Add("@Reason", SqlDbType.NVarChar, 1000).Value = (object?)reason ?? DBNull.Value;
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = requestId;
        command.Parameters.Add("@VersionNo", SqlDbType.Int).Value = versionNo;
        await EnsureOneRowAsync(command, cancellationToken);
    }

    private static async Task WriteAuditAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string entityType,
        Guid entityId,
        string action,
        string? oldState,
        string? newState,
        Guid actorId,
        string? reason,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            "INSERT INTO dbo.AuditLogs (EntityType, EntityId, Action, OldState, NewState, ActorUserId, Reason, CorrelationId, OccurredAt) " +
            "VALUES (@EntityType, @EntityId, @Action, @OldState, @NewState, @ActorId, @Reason, @CorrelationId, SYSUTCDATETIME());",
            connection,
            transaction);
        command.Parameters.Add("@EntityType", SqlDbType.VarChar, 100).Value = entityType;
        command.Parameters.Add("@EntityId", SqlDbType.UniqueIdentifier).Value = entityId;
        command.Parameters.Add("@Action", SqlDbType.VarChar, 50).Value = action;
        command.Parameters.Add("@OldState", SqlDbType.VarChar, 50).Value = (object?)oldState ?? DBNull.Value;
        command.Parameters.Add("@NewState", SqlDbType.VarChar, 50).Value = (object?)newState ?? DBNull.Value;
        command.Parameters.Add("@ActorId", SqlDbType.UniqueIdentifier).Value = actorId;
        command.Parameters.Add("@Reason", SqlDbType.NVarChar, 2000).Value = (object?)reason ?? DBNull.Value;
        command.Parameters.Add("@CorrelationId", SqlDbType.UniqueIdentifier).Value = correlationId;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task WriteOutboxAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        IIntegrationEvent integrationEvent,
        string aggregateType,
        Guid aggregateId,
        Guid correlationId,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await using var command = new SqlCommand(
            "INSERT INTO dbo.OutboxMessages (EventType, AggregateType, AggregateId, Payload, CorrelationId, OccurredAt, Status) " +
            "VALUES (@EventType, @AggregateType, @AggregateId, @Payload, @CorrelationId, @OccurredAt, 'PENDING');",
            connection,
            transaction);
        command.Parameters.Add("@EventType", SqlDbType.VarChar, 150).Value = integrationEvent.EventType;
        command.Parameters.Add("@AggregateType", SqlDbType.VarChar, 100).Value = aggregateType;
        command.Parameters.Add("@AggregateId", SqlDbType.UniqueIdentifier).Value = aggregateId;
        command.Parameters.Add("@Payload", SqlDbType.NVarChar, -1).Value = payload;
        command.Parameters.Add("@CorrelationId", SqlDbType.UniqueIdentifier).Value = correlationId;
        command.Parameters.Add("@OccurredAt", SqlDbType.DateTime2).Value = occurredAt.UtcDateTime;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<Guid> RequireCustomerIdAsync(SqlConnection connection, SqlTransaction transaction, Guid identityUserId, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("SELECT Id FROM dbo.Customers WHERE IdentityUserId = @IdentityUserId;", connection, transaction);
        command.Parameters.Add("@IdentityUserId", SqlDbType.UniqueIdentifier).Value = identityUserId;
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is Guid customerId ? customerId : throw new BusinessRuleException("Create a customer profile before creating a request.", "CUSTOMER_PROFILE_REQUIRED");
    }

    private async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task EnsureOneRowAsync(SqlCommand command, CancellationToken cancellationToken)
    {
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new ConcurrencyException();
        }
    }

    private static void AddRequestParameters(SqlCommand command, TransportRequestInput input)
    {
        command.Parameters.Add("@OriginLocationId", SqlDbType.UniqueIdentifier).Value = input.OriginLocationId;
        command.Parameters.Add("@DestinationLocationId", SqlDbType.UniqueIdentifier).Value = input.DestinationLocationId;
        command.Parameters.Add("@DepartureAt", SqlDbType.DateTime2).Value = input.RequestedDepartureAt.UtcDateTime;
        command.Parameters.Add("@ArrivalAt", SqlDbType.DateTime2).Value = (object?)input.RequestedArrivalAt?.UtcDateTime ?? DBNull.Value;
        command.Parameters.Add("@Mode", SqlDbType.VarChar, 30).Value = (object?)input.PreferredTransportMode ?? DBNull.Value;
        command.Parameters.Add("@Requirements", SqlDbType.NVarChar, 2000).Value = (object?)input.SpecialRequirements ?? DBNull.Value;
        command.Parameters.Add("@Notes", SqlDbType.NVarChar, 2000).Value = (object?)input.Notes ?? DBNull.Value;
    }

    private static void AddPaging(SqlCommand command, int page, int pageSize)
    {
        command.Parameters.Add("@Offset", SqlDbType.Int).Value = page * pageSize;
        command.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;
    }

    private static DateTimeOffset ToUtcOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static void EnsureAssignedManager(TransportRequestRecord request, Guid managerUserId)
    {
        if (request.AssignedManagerUserId is not null && request.AssignedManagerUserId != managerUserId)
        {
            throw new ForbiddenException("This request is assigned to another logistics manager.");
        }
    }

    private static void ValidateInput(TransportRequestInput input, bool requireHorses)
    {
        if (input.OriginLocationId == Guid.Empty || input.DestinationLocationId == Guid.Empty || input.OriginLocationId == input.DestinationLocationId)
        {
            throw new BusinessRuleException("Choose different origin and destination locations.", "REQUEST_ROUTE_INVALID");
        }

        if (input.RequestedDepartureAt <= DateTimeOffset.UtcNow ||
            input.RequestedArrivalAt is not null && input.RequestedArrivalAt <= input.RequestedDepartureAt)
        {
            throw new BusinessRuleException("The arrival time must be later than a future departure time.", "REQUEST_SCHEDULE_INVALID");
        }

        if (input.PreferredTransportMode is not null && input.PreferredTransportMode is not ("ROAD" or "AIR" or "MIXED"))
        {
            throw new BusinessRuleException("Preferred transport mode is not supported.", "TRANSPORT_MODE_INVALID", "preferredTransportMode");
        }

        if (requireHorses && input.HorseIds.Count == 0)
        {
            throw new BusinessRuleException("Select at least one horse.", "REQUEST_HORSE_REQUIRED", "horseIds");
        }
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 0 || pageSize is < 1 or > 100)
        {
            throw new BusinessRuleException("Page must be non-negative and pageSize must be between 1 and 100.", "PAGINATION_INVALID");
        }
    }
}