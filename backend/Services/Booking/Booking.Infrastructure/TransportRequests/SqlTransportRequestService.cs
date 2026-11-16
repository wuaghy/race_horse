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
    private readonly int _defaultMinimumLeadTimeDays;
    private readonly IReadOnlyDictionary<string, int> _routeMinimumLeadTimeDays;

    public SqlTransportRequestService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("BookingDb")
            ?? throw new InvalidOperationException("ConnectionStrings:BookingDb must be configured.");
        var configuredDefault = configuration["Booking:DefaultMinimumLeadTimeDays"];
        if (configuredDefault is null)
            _defaultMinimumLeadTimeDays = 5;
        else if (!int.TryParse(configuredDefault, out var defaultDays) || defaultDays < 1)
            throw new InvalidOperationException("Booking:DefaultMinimumLeadTimeDays must be a positive integer.");
        else
            _defaultMinimumLeadTimeDays = defaultDays;

        var routeOverrides = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in configuration.GetSection("Booking:RouteMinimumLeadTimeDays").GetChildren())
        {
            var routeParts = entry.Key.Split('>');
            if (routeParts.Length != 2 || !Guid.TryParse(routeParts[0], out var originId) || !Guid.TryParse(routeParts[1], out var destinationId))
                throw new InvalidOperationException($"Invalid Booking:RouteMinimumLeadTimeDays key '{entry.Key}'. Use '<origin-location-guid>><destination-location-guid>'.");
            if (!int.TryParse(entry.Value, out var days) || days < 1)
                throw new InvalidOperationException($"Booking:RouteMinimumLeadTimeDays['{entry.Key}'] must be a positive integer.");
            routeOverrides[RouteKey(originId, destinationId)] = days;
        }
        _routeMinimumLeadTimeDays = routeOverrides;
    }

    public BookingLeadTimeInfo GetBookingLeadTime(Guid originLocationId, Guid destinationLocationId)
    {
        if (originLocationId == Guid.Empty || destinationLocationId == Guid.Empty || originLocationId == destinationLocationId)
            throw new BusinessRuleException("Choose different origin and destination locations.", "REQUEST_ROUTE_INVALID");
        var days = MinimumLeadTimeDays(originLocationId, destinationLocationId);
        return new BookingLeadTimeInfo(days, DateTimeOffset.UtcNow.AddDays(days));
    }

    public async Task<IReadOnlyList<CustomerHorseAvailability>> GetCustomerHorseAvailabilityAsync(
        Guid identityUserId,
        IReadOnlyList<Guid> horseIds,
        Guid? requestId,
        CancellationToken cancellationToken)
    {
        if (horseIds.Count is < 1 or > 100 || horseIds.Any(id => id == Guid.Empty) || horseIds.Distinct().Count() != horseIds.Count)
            throw new BusinessRuleException("Choose between 1 and 100 different valid horses.", "HORSE_SELECTION_INVALID", "horseIds");

        await using var connection = await OpenConnectionAsync(cancellationToken);
        if (requestId is Guid currentRequestId)
        {
            await using var ownedDraft = new SqlCommand(
                "SELECT COUNT(*) FROM dbo.TransportRequests r INNER JOIN dbo.Customers c ON c.Id = r.CustomerId " +
                "WHERE r.Id = @RequestId AND c.IdentityUserId = @IdentityUserId AND r.Status IN ('DRAFT','NEED_INFORMATION');", connection);
            ownedDraft.Parameters.Add("@RequestId", SqlDbType.UniqueIdentifier).Value = currentRequestId;
            ownedDraft.Parameters.Add("@IdentityUserId", SqlDbType.UniqueIdentifier).Value = identityUserId;
            if (Convert.ToInt32(await ownedDraft.ExecuteScalarAsync(cancellationToken)) != 1)
                throw new NotFoundException("Editable transport request", currentRequestId);
        }

        var idParameters = string.Join(", ", horseIds.Select((_, index) => $"@HorseId{index}"));
        var sql =
            "SELECT h.Id, h.Status, CASE WHEN h.Status <> 'ACTIVE' THEN 'HORSE_NOT_ACTIVE' " +
            "WHEN EXISTS (SELECT 1 FROM dbo.TransportRequestHorses rh " +
            "INNER JOIN dbo.TransportRequests r ON r.Id = rh.RequestId " +
            "LEFT JOIN dbo.TransportOrders o ON o.RequestId = r.Id " +
            "WHERE rh.HorseId = h.Id AND (@RequestId IS NULL OR r.Id <> @RequestId) " +
            "AND r.Status IN ('DRAFT','SUBMITTED','UNDER_REVIEW','NEED_INFORMATION','APPROVED') " +
            "AND (r.Status <> 'APPROVED' OR COALESCE(o.Status, 'CREATED') NOT IN ('COMPLETED','CANCELLED'))) " +
            "THEN 'HORSE_TRANSPORT_ALREADY_OPEN' ELSE NULL END AS Blocker " +
            "FROM dbo.Horses h INNER JOIN dbo.Customers c ON c.Id = h.OwnerCustomerId " +
            $"WHERE c.IdentityUserId = @IdentityUserId AND h.Id IN ({idParameters});";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@IdentityUserId", SqlDbType.UniqueIdentifier).Value = identityUserId;
        command.Parameters.Add("@RequestId", SqlDbType.UniqueIdentifier).Value = (object?)requestId ?? DBNull.Value;
        for (var index = 0; index < horseIds.Count; index++)
            command.Parameters.Add($"@HorseId{index}", SqlDbType.UniqueIdentifier).Value = horseIds[index];

        var resultById = new Dictionary<Guid, CustomerHorseAvailability>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var blocker = reader.IsDBNull(2) ? null : reader.GetString(2);
                resultById[reader.GetGuid(0)] = new CustomerHorseAvailability(reader.GetGuid(0), blocker is null, reader.GetString(1), blocker);
            }
        }

        return horseIds.Select(id => resultById.TryGetValue(id, out var item)
            ? item
            : new CustomerHorseAvailability(id, false, "UNKNOWN", "HORSE_OWNERSHIP_REQUIRED")).ToList();
    }

    public async Task<OrderStatusSummary?> GetOrderStatusAsync(Guid orderId, Guid? customerIdentityUserId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand(
            "SELECT o.Id, o.OrderNo, o.RequestId, o.Status, o.UpdatedAt " +
            "FROM dbo.TransportOrders o " +
            "INNER JOIN dbo.TransportRequests r ON r.Id = o.RequestId " +
            "INNER JOIN dbo.Customers c ON c.Id = r.CustomerId " +
            "WHERE o.Id = @OrderId AND (@CustomerIdentityUserId IS NULL OR c.IdentityUserId = @CustomerIdentityUserId);",
            connection);
        command.Parameters.Add("@OrderId", SqlDbType.UniqueIdentifier).Value = orderId;
        command.Parameters.Add("@CustomerIdentityUserId", SqlDbType.UniqueIdentifier).Value = (object?)customerIdentityUserId ?? DBNull.Value;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new OrderStatusSummary(reader.GetGuid(0), reader.GetString(1), reader.GetGuid(2), reader.GetString(3), ToUtcOffset(reader.GetDateTime(4)));
    }

    public async Task<OrderStatusSummary?> CancelOrderAsync(Guid orderId, Guid identityUserId, string? reason, Guid correlationId, CancellationToken cancellationToken)
    {
        reason = NormalizeCancellationReason(reason);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            Guid requestId;
            string orderStatus;
            string requestStatus;
            await using (var read = new SqlCommand(
                "SELECT o.RequestId, o.Status, r.Status FROM dbo.TransportOrders o WITH (UPDLOCK, HOLDLOCK) " +
                "INNER JOIN dbo.TransportRequests r ON r.Id = o.RequestId " +
                "INNER JOIN dbo.Customers c ON c.Id = r.CustomerId " +
                "WHERE o.Id = @OrderId AND c.IdentityUserId = @IdentityUserId;", connection, transaction))
            {
                read.Parameters.Add("@OrderId", SqlDbType.UniqueIdentifier).Value = orderId;
                read.Parameters.Add("@IdentityUserId", SqlDbType.UniqueIdentifier).Value = identityUserId;
                await using var reader = await read.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return null;
                }
                requestId = reader.GetGuid(0);
                orderStatus = reader.GetString(1);
                requestStatus = reader.GetString(2);
            }

            if (orderStatus == "CANCELLED")
            {
                await transaction.CommitAsync(cancellationToken);
                return await GetOrderStatusAsync(orderId, identityUserId, cancellationToken);
            }
            if (requestStatus == "APPROVED")
                throw new BusinessRuleException("An approved transport request cannot be cancelled by the customer.", "ORDER_CANCELLATION_NOT_ALLOWED");

            if (orderStatus is not ("CREATED" or "PLANNING"))
                throw new BusinessRuleException("This order is no longer eligible for customer cancellation.", "ORDER_CANCELLATION_NOT_ALLOWED");

            var now = DateTimeOffset.UtcNow;
            await using (var updateOrder = new SqlCommand(
                "UPDATE dbo.TransportOrders SET Status = 'CANCELLED', UpdatedAt = @Now, VersionNo = VersionNo + 1 WHERE Id = @OrderId AND Status IN ('CREATED','PLANNING');", connection, transaction))
            {
                updateOrder.Parameters.Add("@Now", SqlDbType.DateTime2).Value = now.UtcDateTime;
                updateOrder.Parameters.Add("@OrderId", SqlDbType.UniqueIdentifier).Value = orderId;
                await EnsureOneRowAsync(updateOrder, cancellationToken);
            }
            await using (var updateRequest = new SqlCommand(
                "UPDATE dbo.TransportRequests SET Status = 'CANCELLED', UpdatedAt = @Now, VersionNo = VersionNo + 1 WHERE Id = @RequestId AND Status = 'APPROVED';", connection, transaction))
            {
                updateRequest.Parameters.Add("@Now", SqlDbType.DateTime2).Value = now.UtcDateTime;
                updateRequest.Parameters.Add("@RequestId", SqlDbType.UniqueIdentifier).Value = requestId;
                await EnsureOneRowAsync(updateRequest, cancellationToken);
            }

            var auditReason = reason is null ? "Cancelled by customer before trip readiness." : $"Cancelled by customer before trip readiness: {reason}";
            await WriteAuditAsync(connection, transaction, "TransportRequest", requestId, "CANCELLED", requestStatus, "CANCELLED", identityUserId, auditReason, correlationId, cancellationToken);
            await WriteAuditAsync(connection, transaction, "TransportOrder", orderId, "CANCELLED", orderStatus, "CANCELLED", identityUserId, auditReason, correlationId, cancellationToken);
            await WriteOutboxAsync(connection, transaction, new BookingOrderCancelledEvent
            {
                CorrelationId = correlationId,
                OccurredAt = now.UtcDateTime,
                Data = new BookingOrderCancelledData(orderId, requestId, identityUserId, reason)
            }, "TransportOrder", orderId, correlationId, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return await GetOrderStatusAsync(orderId, identityUserId, cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<PageResult<TransportRequestListItem>> GetCustomerRequestsAsync(Guid identityUserId, int page, int pageSize, string? search, string? status, string? sortBy, string? sortDirection, CancellationToken cancellationToken)
    {
        ValidatePage(page, pageSize);
        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        status = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToUpperInvariant();
        sortBy = string.IsNullOrWhiteSpace(sortBy) ? null : sortBy.Trim().ToLowerInvariant();
        sortDirection = string.IsNullOrWhiteSpace(sortDirection) ? (sortBy is null ? "desc" : "asc") : sortDirection.Trim().ToLowerInvariant();
        if (sortBy is not (null or "departure" or "request" or "created"))
            throw new BusinessRuleException("Request sortBy must be 'departure', 'request', or 'created'.", "SORT_FIELD_INVALID", "sortBy");
        if (sortDirection is not ("asc" or "desc"))
            throw new BusinessRuleException("Sort direction must be 'asc' or 'desc'.", "SORT_DIRECTION_INVALID", "sortDirection");
        var orderBy = sortBy switch
        {
            "departure" => $"r.RequestedDepartureAt {sortDirection.ToUpperInvariant()}, r.Id ASC",
            "request" => $"r.RequestNo {sortDirection.ToUpperInvariant()}, r.Id ASC",
            "created" => $"r.CreatedAt {sortDirection.ToUpperInvariant()}, r.Id ASC",
            _ => "r.CreatedAt DESC, r.Id DESC"
        };
        await using var connection = await OpenConnectionAsync(cancellationToken);
        const string ownerFilter = "FROM dbo.TransportRequests r INNER JOIN dbo.Customers c ON c.Id = r.CustomerId WHERE c.IdentityUserId = @IdentityUserId AND (@Search IS NULL OR r.RequestNo LIKE '%' + @Search + '%' OR EXISTS (SELECT 1 FROM dbo.TransportRequestHorses searchRh INNER JOIN dbo.Horses searchHorse ON searchHorse.Id = searchRh.HorseId WHERE searchRh.RequestId = r.Id AND searchHorse.HorseName LIKE '%' + @Search + '%')) AND (@Status IS NULL OR r.Status = @Status)";
        await using var countCommand = new SqlCommand("SELECT COUNT_BIG(*) " + ownerFilter + ";", connection);
        countCommand.Parameters.Add("@IdentityUserId", SqlDbType.UniqueIdentifier).Value = identityUserId;
        countCommand.Parameters.Add("@Search", SqlDbType.NVarChar, 200).Value = (object?)search ?? DBNull.Value;
        countCommand.Parameters.Add("@Status", SqlDbType.VarChar, 30).Value = (object?)status ?? DBNull.Value;
        var total = (long)(await countCommand.ExecuteScalarAsync(cancellationToken) ?? 0L);

        await using var command = new SqlCommand(
            "SELECT r.Id, r.RequestNo, r.Status, r.RequestedDepartureAt, r.CreatedAt, " +
            "(SELECT COUNT(*) FROM dbo.TransportRequestHorses rh WHERE rh.RequestId = r.Id) AS HorseCount, r.VersionNo, " +
            "COALESCE((SELECT STRING_AGG(h.HorseName, ', ') FROM dbo.TransportRequestHorses rh INNER JOIN dbo.Horses h ON h.Id = rh.HorseId WHERE rh.RequestId = r.Id), '') AS HorseNames " +
            ownerFilter + $" ORDER BY {orderBy} OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;",
            connection);
        command.Parameters.Add("@IdentityUserId", SqlDbType.UniqueIdentifier).Value = identityUserId;
        command.Parameters.Add("@Search", SqlDbType.NVarChar, 200).Value = (object?)search ?? DBNull.Value;
        command.Parameters.Add("@Status", SqlDbType.VarChar, 30).Value = (object?)status ?? DBNull.Value;
        AddPaging(command, page, pageSize);
        return new PageResult<TransportRequestListItem>(await ReadListAsync(command, cancellationToken), page, pageSize, total);
    }

    public async Task<TransportRequestRecord?> GetCustomerRequestAsync(Guid identityUserId, Guid requestId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        return await ReadRequestAsync(connection, null, requestId, identityUserId, cancellationToken);
    }

    public async Task<IReadOnlyList<TransportRequestHistoryEntry>> GetCustomerRequestHistoryAsync(Guid identityUserId, Guid requestId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        if (await ReadRequestAsync(connection, null, requestId, identityUserId, cancellationToken) is null)
            throw new NotFoundException("Transport request", requestId);

        await using var command = new SqlCommand(
            "SELECT Id, Action, OldState, NewState, ActorUserId, Reason, OccurredAt FROM dbo.AuditLogs " +
            "WHERE EntityType = 'TransportRequest' AND EntityId = @RequestId ORDER BY OccurredAt, Id;", connection);
        command.Parameters.Add("@RequestId", SqlDbType.UniqueIdentifier).Value = requestId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var history = new List<TransportRequestHistoryEntry>();
        while (await reader.ReadAsync(cancellationToken))
        {
            history.Add(new TransportRequestHistoryEntry(
                reader.GetGuid(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetGuid(4),
                reader.IsDBNull(5) ? null : reader.GetString(5), ToUtcOffset(reader.GetDateTime(6))));
        }
        return history;
    }

    public async Task<TransportRequestRecord?> GetManagerRequestAsync(Guid requestId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        return await ReadRequestAsync(connection, null, requestId, null, cancellationToken);
    }

    public async Task<TransportRequestRecord> CreateDraftAsync(Guid identityUserId, TransportRequestInput input, Guid? idempotencyKey, CancellationToken cancellationToken)
    {
        ValidateInput(input, requireHorses: false, enforceLeadTime: false);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var customerId = await RequireCustomerIdAsync(connection, transaction, identityUserId, cancellationToken);
            if (idempotencyKey is Guid key)
            {
                await using var lookup = new SqlCommand(
                    "SELECT CustomerUserId, RequestId FROM dbo.TransportRequestIdempotencyKeys WITH (UPDLOCK, HOLDLOCK) WHERE IdempotencyKey = @Key;",
                    connection, transaction);
                lookup.Parameters.Add("@Key", SqlDbType.UniqueIdentifier).Value = key;
                await using var reader = await lookup.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    var ownerId = reader.GetGuid(0);
                    var existingRequestId = reader.GetGuid(1);
                    await reader.DisposeAsync();
                    if (ownerId != identityUserId)
                        throw new BusinessRuleException("This request key has already been used.", "IDEMPOTENCY_KEY_REUSED");
                    await transaction.CommitAsync(cancellationToken);
                    return (await GetCustomerRequestAsync(identityUserId, existingRequestId, cancellationToken))
                        ?? throw new InvalidOperationException("The idempotent request mapping points to a missing request.");
                }
            }
            var requestId = Guid.NewGuid();
            var requestNo = "REQ-" + DateTime.UtcNow.ToString("yyyyMMdd") + "-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            await using var command = new SqlCommand(
                "INSERT INTO dbo.TransportRequests (Id, RequestNo, CustomerId, CreatedByUserId, OriginLocationId, DestinationLocationId, " +
                "RequestedDepartureAt, RequestedArrivalAt, PreferredTransportMode, SpecialRequirements, Notes, DeliveryPriority, " +
                "CustomPickupLatitude, CustomPickupLongitude, CustomPickupLabel, " +
                "RecipientIsRequester, RecipientName, RecipientPhone, RecipientEmail, Status) " +
                "VALUES (@Id, @RequestNo, @CustomerId, @CreatedByUserId, @OriginLocationId, @DestinationLocationId, " +
                "@DepartureAt, @ArrivalAt, @Mode, @Requirements, @Notes, @DeliveryPriority, " +
                "@CustomPickupLatitude, @CustomPickupLongitude, @CustomPickupLabel, " +
                "@RecipientIsRequester, @RecipientName, @RecipientPhone, @RecipientEmail, 'DRAFT');",
                connection,
                transaction);
            AddRequestParameters(command, input);
            command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = requestId;
            command.Parameters.Add("@RequestNo", SqlDbType.VarChar, 50).Value = requestNo;
            command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = customerId;
            command.Parameters.Add("@CreatedByUserId", SqlDbType.UniqueIdentifier).Value = identityUserId;
            await command.ExecuteNonQueryAsync(cancellationToken);
            await InsertRequestHorsesAsync(connection, transaction, requestId, customerId, input.HorseIds, cancellationToken);
            if (idempotencyKey is Guid createdWithKey)
            {
                await using var saveKey = new SqlCommand(
                    "INSERT INTO dbo.TransportRequestIdempotencyKeys (IdempotencyKey, CustomerUserId, RequestId, CreatedAt) VALUES (@Key, @CustomerUserId, @RequestId, SYSUTCDATETIME());",
                    connection, transaction);
                saveKey.Parameters.Add("@Key", SqlDbType.UniqueIdentifier).Value = createdWithKey;
                saveKey.Parameters.Add("@CustomerUserId", SqlDbType.UniqueIdentifier).Value = identityUserId;
                saveKey.Parameters.Add("@RequestId", SqlDbType.UniqueIdentifier).Value = requestId;
                await saveKey.ExecuteNonQueryAsync(cancellationToken);
            }
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
        ValidateInput(input, requireHorses: false, enforceLeadTime: false);
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
                "SpecialRequirements = @Requirements, Notes = @Notes, DeliveryPriority = @DeliveryPriority, " +
                "CustomPickupLatitude = @CustomPickupLatitude, CustomPickupLongitude = @CustomPickupLongitude, CustomPickupLabel = @CustomPickupLabel, " +
                "RecipientIsRequester = @RecipientIsRequester, RecipientName = @RecipientName, RecipientPhone = @RecipientPhone, RecipientEmail = @RecipientEmail, " +
                "UpdatedAt = SYSUTCDATETIME(), VersionNo = VersionNo + 1 " +
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

            ValidateInput(new TransportRequestInput(
                current.OriginLocationId ?? Guid.Empty,
                current.DestinationLocationId ?? Guid.Empty,
                current.RequestedDepartureAt,
                current.RequestedArrivalAt,
                current.PreferredTransportMode,
                current.SpecialRequirements,
                current.Notes,
                current.HorseIds,
                current.VersionNo,
                current.DeliveryPriority,
                current.RecipientIsRequester,
                current.RecipientName,
                current.RecipientPhone,
                current.RecipientEmail,
                current.CustomPickupLatitude,
                current.CustomPickupLongitude,
                current.CustomPickupLabel), requireHorses: true, enforceLeadTime: true);

            if (current.OriginLocationId is null || current.DestinationLocationId is null || current.HorseIds.Count == 0)
            {
                throw new BusinessRuleException("Origin, destination, and at least one horse are required before submission.", "REQUEST_INCOMPLETE");
            }

            await EnsureRequestHorsesActiveAsync(connection, transaction, requestId, cancellationToken);
            await EnsureNoOpenHorseTransportAsync(connection, transaction, requestId, cancellationToken);

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

    public async Task<TransportRequestRecord> CancelCustomerRequestAsync(Guid identityUserId, Guid requestId, string? reason, Guid correlationId, CancellationToken cancellationToken)
    {
        reason = NormalizeCancellationReason(reason);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var current = await ReadRequestAsync(connection, transaction, requestId, identityUserId, cancellationToken, lockForUpdate: true)
                ?? throw new NotFoundException("Transport request", requestId);
            if (current.Status == "CANCELLED")
            {
                await transaction.CommitAsync(cancellationToken);
                return current;
            }
            if (current.Status is not ("DRAFT" or "SUBMITTED" or "UNDER_REVIEW" or "NEED_INFORMATION"))
                throw new BusinessRuleException("Only draft, submitted, under-review, or returned requests can be cancelled by the customer.", "REQUEST_CANCELLATION_NOT_ALLOWED");

            await using var update = new SqlCommand(
                "UPDATE dbo.TransportRequests SET Status = 'CANCELLED', UpdatedAt = SYSUTCDATETIME(), VersionNo = VersionNo + 1 WHERE Id = @Id AND VersionNo = @VersionNo;",
                connection, transaction);
            update.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = requestId;
            update.Parameters.Add("@VersionNo", SqlDbType.Int).Value = current.VersionNo;
            await EnsureOneRowAsync(update, cancellationToken);
            var auditReason = reason is null ? "Cancelled by customer." : $"Cancelled by customer: {reason}";
            if (current.AssignedManagerUserId is Guid managerId)
                auditReason += $" Manager assignment {managerId:D} retained on request.";
            await WriteAuditAsync(connection, transaction, "TransportRequest", requestId, "CANCELLED", current.Status, "CANCELLED", identityUserId, auditReason, correlationId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return (await GetCustomerRequestAsync(identityUserId, requestId, cancellationToken))!;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static string? NormalizeCancellationReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return null;
        reason = reason.Trim();
        if (reason.Length > 1000)
            throw new BusinessRuleException("Cancellation reason must not exceed 1000 characters.", "CANCELLATION_REASON_TOO_LONG", "reason");
        return reason;
    }

    public async Task<PageResult<TransportRequestListItem>> GetManagerQueueAsync(Guid managerUserId, int page, int pageSize, string? search, string? status, string? sortBy, string? sortDirection, CancellationToken cancellationToken)
    {
        ValidatePage(page, pageSize);
        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        status = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToUpperInvariant();
        sortBy = string.IsNullOrWhiteSpace(sortBy) ? null : sortBy.Trim().ToLowerInvariant();
        sortDirection = string.IsNullOrWhiteSpace(sortDirection) ? (sortBy is null ? "asc" : "asc") : sortDirection.Trim().ToLowerInvariant();
        if (sortBy is not (null or "departure" or "request" or "submitted"))
            throw new BusinessRuleException("Manager queue sortBy must be 'departure', 'request', or 'submitted'.", "SORT_FIELD_INVALID", "sortBy");
        if (sortDirection is not ("asc" or "desc"))
            throw new BusinessRuleException("Sort direction must be 'asc' or 'desc'.", "SORT_DIRECTION_INVALID", "sortDirection");
        var orderBy = sortBy switch
        {
            "departure" => $"r.RequestedDepartureAt {sortDirection.ToUpperInvariant()}, r.Id ASC",
            "request" => $"r.RequestNo {sortDirection.ToUpperInvariant()}, r.Id ASC",
            "submitted" => $"r.SubmittedAt {sortDirection.ToUpperInvariant()}, r.CreatedAt {sortDirection.ToUpperInvariant()}, r.Id ASC",
            _ => "r.SubmittedAt, r.CreatedAt, r.Id"
        };
        if (status is not null && status is not ("SUBMITTED" or "UNDER_REVIEW" or "NEED_INFORMATION" or "APPROVED" or "REJECTED" or "CANCELLED"))
        {
            throw new BusinessRuleException("The requested manager queue status is not supported.", "REQUEST_STATUS_FILTER_INVALID", "status");
        }
        await using var connection = await OpenConnectionAsync(cancellationToken);
        const string filter = "FROM dbo.TransportRequests r WHERE ((@Status IS NOT NULL AND r.Status = @Status) OR (@Status IS NULL AND (@Search IS NOT NULL OR r.Status IN ('SUBMITTED','UNDER_REVIEW'))) ) " +
            "AND (r.AssignedManagerUserId IS NULL OR r.AssignedManagerUserId = @ManagerId) " +
            "AND (@Search IS NULL OR r.RequestNo LIKE '%' + @Search + '%')";
        await using var countCommand = new SqlCommand("SELECT COUNT_BIG(*) " + filter + ";", connection);
        countCommand.Parameters.Add("@ManagerId", SqlDbType.UniqueIdentifier).Value = managerUserId;
        countCommand.Parameters.Add("@Search", SqlDbType.NVarChar, 200).Value = (object?)search ?? DBNull.Value;
        countCommand.Parameters.Add("@Status", SqlDbType.VarChar, 30).Value = (object?)status ?? DBNull.Value;
        var total = (long)(await countCommand.ExecuteScalarAsync(cancellationToken) ?? 0L);
        await using var command = new SqlCommand(
            "SELECT r.Id, r.RequestNo, r.Status, r.RequestedDepartureAt, r.CreatedAt, " +
            "(SELECT COUNT(*) FROM dbo.TransportRequestHorses rh WHERE rh.RequestId = r.Id) AS HorseCount, r.VersionNo, " +
            "COALESCE((SELECT STRING_AGG(h.HorseName, ', ') FROM dbo.TransportRequestHorses rh INNER JOIN dbo.Horses h ON h.Id = rh.HorseId WHERE rh.RequestId = r.Id), '') AS HorseNames " +
            filter + $" ORDER BY {orderBy} OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;",
            connection);
        command.Parameters.Add("@ManagerId", SqlDbType.UniqueIdentifier).Value = managerUserId;
        command.Parameters.Add("@Search", SqlDbType.NVarChar, 200).Value = (object?)search ?? DBNull.Value;
        command.Parameters.Add("@Status", SqlDbType.VarChar, 30).Value = (object?)status ?? DBNull.Value;
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

    public async Task<TransportRequestRecord> ApproveAsync(Guid managerUserId, Guid requestId, string? note, Guid correlationId, CancellationToken cancellationToken, string? managerName = null)
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

            await EnsureRequestHorsesActiveAsync(connection, transaction, requestId, cancellationToken);
            await EnsureNoOpenHorseTransportAsync(connection, transaction, requestId, cancellationToken);

            var orderId = Guid.NewGuid();
            var orderNo = "ORD-" + DateTime.UtcNow.ToString("yyyyMMdd") + "-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            var now = DateTimeOffset.UtcNow;
            var customer = await GetCustomerHandoverContactAsync(connection, transaction, current.CustomerId, cancellationToken);
            var requestedHorses = await ReadRequestHorseNamesAsync(connection, transaction, requestId, cancellationToken);
            var quote = await ReadQuoteAsync(connection, transaction, current.OriginLocationId, current.DestinationLocationId, current.CustomPickupLatitude, current.CustomPickupLongitude, current.PreferredTransportMode, current.DeliveryPriority, requestedHorses.Count, cancellationToken);
            await using (var order = new SqlCommand(
                "INSERT INTO dbo.TransportOrders (Id, OrderNo, RequestId, ApprovedByUserId, ApprovedAt, Status, QuotedAmount, CurrencyCode) " +
                "VALUES (@Id, @OrderNo, @RequestId, @ManagerId, @ApprovedAt, 'CREATED', @QuotedAmount, @CurrencyCode);",
                connection,
                transaction))
            {
                order.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = orderId;
                order.Parameters.Add("@OrderNo", SqlDbType.VarChar, 50).Value = orderNo;
                order.Parameters.Add("@RequestId", SqlDbType.UniqueIdentifier).Value = requestId;
                order.Parameters.Add("@ManagerId", SqlDbType.UniqueIdentifier).Value = managerUserId;
                order.Parameters.Add("@ApprovedAt", SqlDbType.DateTime2).Value = now.UtcDateTime;
                var quotedAmount = order.Parameters.Add("@QuotedAmount", SqlDbType.Decimal); quotedAmount.Precision = 18; quotedAmount.Scale = 2; quotedAmount.Value = (object?)quote.Amount ?? DBNull.Value;
                order.Parameters.Add("@CurrencyCode", SqlDbType.VarChar, 10).Value = (object?)quote.CurrencyCode ?? DBNull.Value;
                await order.ExecuteNonQueryAsync(cancellationToken);
            }
            if (quote.Amount is > 0 && quote.CurrencyCode is not null)
            {
                // PayOS order codes are numeric; keep them below JavaScript's safe-integer limit.
                var payOsOrderCode = (BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 0) & long.MaxValue) % 9_000_000_000_000_000L + 1;
                await using var invoice = new SqlCommand(
                    "INSERT INTO dbo.TransportPaymentInvoices (Id, OrderId, InvoiceNo, Amount, CurrencyCode, Status, PayOsOrderCode) VALUES (@Id, @OrderId, @InvoiceNo, @Amount, @Currency, 'PENDING_PAYMENT', @PayOsOrderCode);",
                    connection,
                    transaction);
                invoice.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
                invoice.Parameters.Add("@OrderId", SqlDbType.UniqueIdentifier).Value = orderId;
                invoice.Parameters.Add("@InvoiceNo", SqlDbType.VarChar, 80).Value = "INV-" + orderNo[4..];
                var invoiceAmount = invoice.Parameters.Add("@Amount", SqlDbType.Decimal); invoiceAmount.Precision = 18; invoiceAmount.Scale = 2; invoiceAmount.Value = quote.Amount.Value;
                invoice.Parameters.Add("@Currency", SqlDbType.VarChar, 10).Value = quote.CurrencyCode;
                invoice.Parameters.Add("@PayOsOrderCode", SqlDbType.BigInt).Value = payOsOrderCode;
                await invoice.ExecuteNonQueryAsync(cancellationToken);
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
            var recipientName = current.RecipientIsRequester ? customer.ContactPerson ?? customer.Name : current.RecipientName;
            var recipientPhone = current.RecipientIsRequester ? customer.Phone : current.RecipientPhone;
            var recipientEmail = current.RecipientIsRequester ? customer.Email : current.RecipientEmail;
            await WriteOutboxAsync(connection, transaction, new BookingRequestApprovedEvent
            {
                CorrelationId = correlationId,
                OccurredAt = now.UtcDateTime,
                Data = new BookingRequestApprovedData(requestId, current.CustomerId, current.RequestNo, orderId, customer.UserId,
                    LimitContact(recipientName, 200),
                    LimitContact(recipientPhone, 50),
                    LimitContact(recipientEmail, 255),
                    LimitContact(customer.Email, 255),
                    requestedHorses.Values.ToArray(),
                    LimitContact(managerName, 200),
                    now.UtcDateTime,
                    orderNo,
                    current.RequestedDepartureAt.UtcDateTime,
                    LimitContact(note, 1000))
            }, "TransportRequest", requestId, correlationId, now, cancellationToken);
            await WriteOutboxAsync(connection, transaction, new BookingOrderCreatedEvent
            {
                CorrelationId = correlationId,
                OccurredAt = now.UtcDateTime,
                Data = new BookingOrderCreatedData(
                    orderId,
                    orderNo,
                    requestId,
                    current.CustomerId,
                    current.OriginLocationId,
                    current.DestinationLocationId,
                    current.RequestedDepartureAt.UtcDateTime,
                    current.RequestedArrivalAt?.UtcDateTime,
                    current.PreferredTransportMode,
                    current.CustomPickupLatitude,
                    current.CustomPickupLongitude,
                    current.CustomPickupLabel,
                    requestedHorses.Keys.ToArray(),
                    LimitContact(recipientName, 200),
                    LimitContact(recipientPhone, 50),
                    LimitContact(recipientEmail, 255),
                    current.RequestNo,
                    customer.Name,
                    requestedHorses,
                    quote.Amount is > 0 && quote.CurrencyCode is not null ? "PENDING_PAYMENT" : "PAID",
                    customer.UserId)
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

    private static async Task<IReadOnlyDictionary<Guid, string>> ReadRequestHorseNamesAsync(SqlConnection connection, SqlTransaction transaction, Guid requestId, CancellationToken cancellationToken)
    {
        var horses = new Dictionary<Guid, string>();
        await using var command = new SqlCommand(
            "SELECT horse.Id, horse.HorseName FROM dbo.TransportRequestHorses AS requestHorse " +
            "INNER JOIN dbo.Horses AS horse ON horse.Id = requestHorse.HorseId " +
            "WHERE requestHorse.RequestId = @RequestId ORDER BY horse.Id;", connection, transaction);
        command.Parameters.Add("@RequestId", SqlDbType.UniqueIdentifier).Value = requestId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) horses[reader.GetGuid(0)] = reader.GetString(1);
        return horses;
    }

    private static async Task<(decimal? Amount, string? CurrencyCode)> ReadQuoteAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid? originLocationId,
        Guid? destinationLocationId,
        decimal? customPickupLatitude,
        decimal? customPickupLongitude,
        string? transportMode,
        string? deliveryPriority,
        int horseCount,
        CancellationToken cancellationToken)
    {
        if (originLocationId is null || destinationLocationId is null || horseCount < 1)
            return (null, null);
        await using var command = new SqlCommand(
            "SELECT TOP (1) BaseAmount, PerHorseAmount, PerKmAmount, CurrencyCode FROM dbo.TransportPricingRules " +
            "WHERE IsActive=1 AND (TransportMode IS NULL OR TransportMode=@Mode) AND (DeliveryPriority IS NULL OR DeliveryPriority=@Priority) " +
            "AND (OriginLocationId IS NULL OR OriginLocationId=@Origin) AND (DestinationLocationId IS NULL OR DestinationLocationId=@Destination) " +
            "ORDER BY (CASE WHEN TransportMode IS NULL THEN 0 ELSE 1 END + CASE WHEN DeliveryPriority IS NULL THEN 0 ELSE 1 END + CASE WHEN OriginLocationId IS NULL THEN 0 ELSE 1 END + CASE WHEN DestinationLocationId IS NULL THEN 0 ELSE 1 END) DESC, UpdatedAt DESC;",
            connection,
            transaction);
        command.Parameters.Add("@Mode", SqlDbType.VarChar, 30).Value = (object?)transportMode ?? DBNull.Value;
        command.Parameters.Add("@Priority", SqlDbType.VarChar, 30).Value = (object?)deliveryPriority ?? DBNull.Value;
        command.Parameters.Add("@Origin", SqlDbType.UniqueIdentifier).Value = originLocationId.Value;
        command.Parameters.Add("@Destination", SqlDbType.UniqueIdentifier).Value = destinationLocationId.Value;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return (null, null);
        var baseAmount = reader.GetDecimal(0);
        var perHorseAmount = reader.GetDecimal(1);
        var perKmAmount = reader.GetDecimal(2);
        var currencyCode = reader.GetString(3);
        await reader.DisposeAsync();

        decimal distanceKm = 0;
        if (perKmAmount > 0)
        {
            var distance = await ReadDistanceKmAsync(connection, transaction, originLocationId.Value, destinationLocationId.Value, customPickupLatitude, customPickupLongitude, cancellationToken);
            if (distance is null)
                throw new BusinessRuleException("This pricing rule uses a per-kilometre charge, but the selected locations do not have map coordinates.", "PRICING_DISTANCE_UNAVAILABLE");
            distanceKm = distance.Value;
        }

        var amount = baseAmount + perHorseAmount * horseCount + perKmAmount * distanceKm;
        return (amount, currencyCode);
    }

    private static async Task<decimal?> ReadDistanceKmAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid originLocationId,
        Guid destinationLocationId,
        decimal? customPickupLatitude,
        decimal? customPickupLongitude,
        CancellationToken cancellationToken)
    {
        decimal? originLatitude = customPickupLatitude;
        decimal? originLongitude = customPickupLongitude;
        decimal? destinationLatitude = null;
        decimal? destinationLongitude = null;

        await using var command = new SqlCommand(
            "SELECT Id, Latitude, Longitude FROM PlanningDb.dbo.Locations WHERE Id IN (@Origin, @Destination);",
            connection,
            transaction);
        command.Parameters.Add("@Origin", SqlDbType.UniqueIdentifier).Value = originLocationId;
        command.Parameters.Add("@Destination", SqlDbType.UniqueIdentifier).Value = destinationLocationId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (reader.IsDBNull(1) || reader.IsDBNull(2)) continue;
            var id = reader.GetGuid(0);
            if (id == destinationLocationId)
            {
                destinationLatitude = reader.GetDecimal(1);
                destinationLongitude = reader.GetDecimal(2);
            }
            else if (!originLatitude.HasValue || !originLongitude.HasValue)
            {
                originLatitude = reader.GetDecimal(1);
                originLongitude = reader.GetDecimal(2);
            }
        }

        if (!originLatitude.HasValue || !originLongitude.HasValue || !destinationLatitude.HasValue || !destinationLongitude.HasValue)
            return null;

        static double ToRadians(decimal value) => (double)value * Math.PI / 180d;
        var latitudeA = ToRadians(originLatitude.Value);
        var latitudeB = ToRadians(destinationLatitude.Value);
        var deltaLatitude = latitudeB - latitudeA;
        var deltaLongitude = ToRadians(destinationLongitude.Value - originLongitude.Value);
        var haversine = Math.Pow(Math.Sin(deltaLatitude / 2d), 2d)
            + Math.Cos(latitudeA) * Math.Cos(latitudeB) * Math.Pow(Math.Sin(deltaLongitude / 2d), 2d);
        var distance = 6371d * 2d * Math.Asin(Math.Sqrt(Math.Clamp(haversine, 0d, 1d)));
        return Math.Round((decimal)distance, 2, MidpointRounding.AwayFromZero);
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
            var customerContact = await GetCustomerHandoverContactAsync(connection, transaction, current.CustomerId, cancellationToken);
            var now = DateTimeOffset.UtcNow;
            await WriteOutboxAsync(connection, transaction, new BookingRequestDecisionEvent
            {
                CorrelationId = correlationId,
                OccurredAt = now.UtcDateTime,
                Data = new BookingRequestDecisionData(requestId, customerContact.UserId, current.RequestNo, newStatus, reason.Trim(), LimitContact(customerContact.Email, 255))
            }, "TransportRequest", requestId, correlationId, now, cancellationToken);
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
            "r.RejectionReason, r.VersionNo, o.Id, o.OrderNo, r.DeliveryPriority, r.RecipientIsRequester, " +
            "r.RecipientName, r.RecipientPhone, r.RecipientEmail, r.CustomPickupLatitude, r.CustomPickupLongitude, r.CustomPickupLabel " +
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
                reader.IsDBNull(16) ? null : reader.GetString(16))
            {
                DeliveryPriority = reader.IsDBNull(17) ? "STANDARD" : reader.GetString(17),
                RecipientIsRequester = reader.IsDBNull(18) || reader.GetBoolean(18),
                RecipientName = reader.IsDBNull(19) ? null : reader.GetString(19),
                RecipientPhone = reader.IsDBNull(20) ? null : reader.GetString(20),
                RecipientEmail = reader.IsDBNull(21) ? null : reader.GetString(21),
                CustomPickupLatitude = reader.IsDBNull(22) ? null : reader.GetDecimal(22),
                CustomPickupLongitude = reader.IsDBNull(23) ? null : reader.GetDecimal(23),
                CustomPickupLabel = reader.IsDBNull(24) ? null : reader.GetString(24)
            };
        }

        var horses = await ReadRequestHorsesAsync(connection, transaction, requestId, cancellationToken);
        return request with { HorseIds = horses.Select(horse => horse.Id).ToList(), Horses = horses };
    }

    private static async Task<IReadOnlyList<TransportRequestHorse>> ReadRequestHorsesAsync(SqlConnection connection, SqlTransaction? transaction, Guid requestId, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            "SELECT h.Id, h.HorseCode, h.HorseName, h.Breed, h.Sex, h.DateOfBirth, h.Color, h.CountryOfOriginId, h.SpecialRequirements, h.Status FROM dbo.TransportRequestHorses AS rh " +
            "INNER JOIN dbo.Horses AS h ON h.Id = rh.HorseId " +
            "WHERE rh.RequestId = @RequestId ORDER BY rh.CreatedAt, rh.Id;",
            connection,
            transaction);
        command.Parameters.Add("@RequestId", SqlDbType.UniqueIdentifier).Value = requestId;
        var horses = new List<TransportRequestHorse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            horses.Add(new TransportRequestHorse(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : DateOnly.FromDateTime(reader.GetDateTime(5)),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetGuid(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.GetString(9)));
        }

        return horses;
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
                reader.GetInt32(6),
                reader.GetString(7)));
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

    private static async Task EnsureRequestHorsesActiveAsync(SqlConnection connection, SqlTransaction transaction, Guid requestId, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            "SELECT COUNT(*) FROM dbo.TransportRequestHorses rh WITH (HOLDLOCK) " +
            "INNER JOIN dbo.Horses h WITH (UPDLOCK, HOLDLOCK) ON h.Id = rh.HorseId " +
            "WHERE rh.RequestId = @RequestId AND h.Status <> 'ACTIVE';", connection, transaction);
        command.Parameters.Add("@RequestId", SqlDbType.UniqueIdentifier).Value = requestId;
        if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0)
            throw new BusinessRuleException("Every horse on the request must be active before submission or approval.", "HORSE_NOT_ACTIVE", "horseIds");
    }

    private static async Task EnsureNoOpenHorseTransportAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            "SELECT TOP (1) other.Id FROM dbo.TransportRequestHorses currentHorse WITH (UPDLOCK, HOLDLOCK) " +
            "INNER JOIN dbo.TransportRequestHorses otherHorse WITH (UPDLOCK, HOLDLOCK) ON otherHorse.HorseId = currentHorse.HorseId AND otherHorse.RequestId <> currentHorse.RequestId " +
            "INNER JOIN dbo.TransportRequests other WITH (UPDLOCK, HOLDLOCK) ON other.Id = otherHorse.RequestId " +
            "LEFT JOIN dbo.TransportOrders o ON o.RequestId = other.Id " +
            "WHERE currentHorse.RequestId = @RequestId " +
            "AND other.Status IN ('DRAFT','SUBMITTED','UNDER_REVIEW','NEED_INFORMATION','APPROVED') " +
            "AND (other.Status <> 'APPROVED' OR COALESCE(o.Status, 'CREATED') NOT IN ('COMPLETED','CANCELLED'));", connection, transaction);
        command.Parameters.Add("@RequestId", SqlDbType.UniqueIdentifier).Value = requestId;
        if (await command.ExecuteScalarAsync(cancellationToken) is not null)
            throw new BusinessRuleException("Each horse can only be included in one nonterminal transport request or order at a time. Cancel or complete the existing transport first.", "HORSE_TRANSPORT_ALREADY_OPEN", "horseIds");
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

    private static async Task<Guid> GetCustomerIdentityUserIdAsync(SqlConnection connection, SqlTransaction transaction, Guid customerId, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("SELECT IdentityUserId FROM dbo.Customers WHERE Id = @CustomerId;", connection, transaction);
        command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = customerId;
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is Guid userId ? userId : throw new BusinessRuleException("The customer account link is missing.", "CUSTOMER_ACCOUNT_NOT_LINKED");
    }

    private static async Task<(Guid UserId, string Name, string? ContactPerson, string? Phone, string? Email)> GetCustomerHandoverContactAsync(
        SqlConnection connection, SqlTransaction transaction, Guid customerId, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            "SELECT IdentityUserId, Name, ContactPerson, Phone, Email FROM dbo.Customers WHERE Id = @CustomerId;",
            connection, transaction);
        command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = customerId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || reader.IsDBNull(0))
            throw new BusinessRuleException("The customer account link is missing.", "CUSTOMER_ACCOUNT_NOT_LINKED");
        return (
            reader.GetGuid(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4));
    }

    private static string? LimitContact(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized)) return null;
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
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
        var pickupLatitude = command.Parameters.Add("@CustomPickupLatitude", SqlDbType.Decimal);
        pickupLatitude.Precision = 10; pickupLatitude.Scale = 7; pickupLatitude.Value = (object?)input.CustomPickupLatitude ?? DBNull.Value;
        var pickupLongitude = command.Parameters.Add("@CustomPickupLongitude", SqlDbType.Decimal);
        pickupLongitude.Precision = 10; pickupLongitude.Scale = 7; pickupLongitude.Value = (object?)input.CustomPickupLongitude ?? DBNull.Value;
        command.Parameters.Add("@CustomPickupLabel", SqlDbType.NVarChar, 300).Value = (object?)input.CustomPickupLabel ?? DBNull.Value;
        command.Parameters.Add("@DeliveryPriority", SqlDbType.VarChar, 20).Value = NormalizeDeliveryPriority(input.DeliveryPriority);
        command.Parameters.Add("@RecipientIsRequester", SqlDbType.Bit).Value = input.RecipientIsRequester;
        command.Parameters.Add("@RecipientName", SqlDbType.NVarChar, 200).Value = input.RecipientIsRequester ? DBNull.Value : (object?)LimitContact(input.RecipientName, 200) ?? DBNull.Value;
        command.Parameters.Add("@RecipientPhone", SqlDbType.VarChar, 50).Value = input.RecipientIsRequester ? DBNull.Value : (object?)LimitContact(input.RecipientPhone, 50) ?? DBNull.Value;
        command.Parameters.Add("@RecipientEmail", SqlDbType.VarChar, 255).Value = input.RecipientIsRequester ? DBNull.Value : (object?)LimitContact(input.RecipientEmail, 255) ?? DBNull.Value;
    }

    private static void AddPaging(SqlCommand command, int page, int pageSize)
    {
        command.Parameters.Add("@Offset", SqlDbType.Int).Value = page * pageSize;
        command.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;
    }

    private static DateTimeOffset ToUtcOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static string NormalizeDeliveryPriority(string? value) =>
        string.Equals(value?.Trim(), "EXPRESS", StringComparison.OrdinalIgnoreCase) ? "EXPRESS" : "STANDARD";

    private int MinimumLeadTimeDays(Guid originLocationId, Guid destinationLocationId) =>
        _routeMinimumLeadTimeDays.TryGetValue(RouteKey(originLocationId, destinationLocationId), out var days)
            ? days
            : _defaultMinimumLeadTimeDays;

    private static string RouteKey(Guid originLocationId, Guid destinationLocationId) =>
        $"{originLocationId:D}>{destinationLocationId:D}";

    private static void EnsureAssignedManager(TransportRequestRecord request, Guid managerUserId)
    {
        if (request.AssignedManagerUserId is not null && request.AssignedManagerUserId != managerUserId)
        {
            throw new ForbiddenException("This request is assigned to another logistics manager.");
        }
    }

    private void ValidateInput(TransportRequestInput input, bool requireHorses, bool enforceLeadTime)
    {
        if (input.OriginLocationId == Guid.Empty || input.DestinationLocationId == Guid.Empty || input.OriginLocationId == input.DestinationLocationId)
        {
            throw new BusinessRuleException("Choose different origin and destination locations.", "REQUEST_ROUTE_INVALID");
        }

        if (input.CustomPickupLatitude.HasValue != input.CustomPickupLongitude.HasValue ||
            input.CustomPickupLatitude is < -90m or > 90m || input.CustomPickupLongitude is < -180m or > 180m)
            throw new BusinessRuleException("Custom pickup coordinates are incomplete or invalid.", "CUSTOM_PICKUP_COORDINATES_INVALID", "customPickupLatitude");
        if (input.CustomPickupLabel?.Length > 300)
            throw new BusinessRuleException("Custom pickup label must not exceed 300 characters.", "CUSTOM_PICKUP_LABEL_TOO_LONG", "customPickupLabel");

        if (input.RequestedDepartureAt <= DateTimeOffset.UtcNow)
        {
            throw new BusinessRuleException("Departure must be in the future.", "DEPARTURE_MUST_BE_FUTURE", "requestedDepartureAt");
        }

        if (enforceLeadTime)
        {
            var leadTime = GetBookingLeadTime(input.OriginLocationId, input.DestinationLocationId);
            if (input.RequestedDepartureAt < leadTime.EarliestDepartureAt)
            {
                throw new BusinessRuleException(
                    $"Departure must be at least {leadTime.MinimumLeadTimeDays} days after submission. Earliest allowed departure is {leadTime.EarliestDepartureAt:O}.",
                    "REQUEST_LEAD_TIME_NOT_MET", "requestedDepartureAt");
            }
        }

        if (input.RequestedArrivalAt is not null && input.RequestedArrivalAt <= input.RequestedDepartureAt)
        {
            throw new BusinessRuleException("Expected arrival must be later than departure.", "ARRIVAL_AFTER_DEPARTURE_REQUIRED", "requestedArrivalAt");
        }

        if (input.PreferredTransportMode is not null && input.PreferredTransportMode is not ("ROAD" or "AIR" or "MIXED"))
        {
            throw new BusinessRuleException("Preferred transport mode is not supported.", "TRANSPORT_MODE_INVALID", "preferredTransportMode");
        }

        if (input.DeliveryPriority is not null && input.DeliveryPriority.Trim().ToUpperInvariant() is not ("STANDARD" or "EXPRESS"))
            throw new BusinessRuleException("Choose Standard or Express delivery priority.", "DELIVERY_PRIORITY_INVALID", "deliveryPriority");

        if (requireHorses && !input.RecipientIsRequester)
        {
            if (string.IsNullOrWhiteSpace(input.RecipientName))
                throw new BusinessRuleException("Enter the recipient's name or choose that the requester will receive the horses.", "RECIPIENT_NAME_REQUIRED", "recipientName");
            if (string.IsNullOrWhiteSpace(input.RecipientPhone))
                throw new BusinessRuleException("Enter the recipient's phone number.", "RECIPIENT_PHONE_REQUIRED", "recipientPhone");
            if (string.IsNullOrWhiteSpace(input.RecipientEmail) || !System.Net.Mail.MailAddress.TryCreate(input.RecipientEmail, out _))
                throw new BusinessRuleException("Enter a valid recipient email address.", "RECIPIENT_EMAIL_REQUIRED", "recipientEmail");
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
