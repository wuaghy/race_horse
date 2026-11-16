using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Booking.Application.Customers;
using Booking.Application.TransportRequests;
using BuildingBlocks.Api;
using BuildingBlocks.Exceptions;
using Contracts.Grpc.Planning;
using Contracts.Grpc.Compliance;
using Grpc.Core;

namespace Booking.Api.TransportRequests;

public static class TransportRequestEndpoints
{
    public static void MapTransportRequestEndpoints(this WebApplication app)
    {
        var orders = app.MapGroup("/api/v1/orders").RequireAuthorization();
        orders.MapGet("/{orderId:guid}", async (Guid orderId, ClaimsPrincipal principal, ITransportRequestService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var role = principal.FindFirstValue("role");
            if (role is not ("CUSTOMER" or "LOGISTICS_MANAGER" or "ADMIN")) return Results.Forbid();
            var customerIdentityUserId = role == "CUSTOMER" ? GetUserId(principal) : (Guid?)null;
            var order = await service.GetOrderStatusAsync(orderId, customerIdentityUserId, cancellationToken);
            return order is null
                ? Results.NotFound(ApiResponse<object>.Failure(40400, "Transport order was not found.", requestId: GetRequestId(context)))
                : Results.Ok(ApiResponse<OrderStatusSummary>.Success(order, requestId: GetRequestId(context)));
        }).WithName("GetTransportOrderStatus");

        orders.MapPost("/{orderId:guid}/cancel", async (
            Guid orderId,
            CustomerCancellationRequest request,
            ClaimsPrincipal principal,
            ITransportRequestService service,
            PlanningQuery.PlanningQueryClient planning,
            IConfiguration configuration,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            if (principal.FindFirstValue("role") != "CUSTOMER") return Results.Forbid();
            var userId = GetUserId(principal);
            var order = await service.GetOrderStatusAsync(orderId, userId, cancellationToken);
            if (order is null) return Results.NotFound(ApiResponse<object>.Failure(40400, "Transport order was not found.", requestId: GetRequestId(context)));

            if (string.Equals(order.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase))
                return Results.Ok(ApiResponse<OrderStatusSummary>.Success(order, "Order was already cancelled.", GetRequestId(context)));

            SetOrderCancellationPendingReply gate;
            try
            {
                gate = await planning.SetOrderCancellationPendingAsync(new SetOrderCancellationPendingRequest { OrderId = orderId.ToString(), Pending = true }, headers: InternalServiceHeaders(configuration), cancellationToken: cancellationToken);
            }
            catch (RpcException)
            {
                return Results.Problem("Trip readiness could not be checked. Try again shortly.", statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            if (!gate.CanCancel)
                return Results.Conflict(ApiResponse<object>.Failure(40900, "The trip is already ready or has started; this order cannot be cancelled by the customer.", requestId: GetRequestId(context)));

            try
            {
                var cancelled = await service.CancelOrderAsync(orderId, userId, request.Reason, GetCorrelationId(context), cancellationToken);
                if (cancelled is not null)
                    return Results.Ok(ApiResponse<OrderStatusSummary>.Success(cancelled, "Order cancelled.", GetRequestId(context)));
                await planning.SetOrderCancellationPendingAsync(new SetOrderCancellationPendingRequest { OrderId = orderId.ToString(), Pending = false }, headers: InternalServiceHeaders(configuration), cancellationToken: cancellationToken);
                return Results.NotFound(ApiResponse<object>.Failure(40400, "Transport order was not found.", requestId: GetRequestId(context)));
            }
            catch
            {
                try
                {
                    await planning.SetOrderCancellationPendingAsync(new SetOrderCancellationPendingRequest { OrderId = orderId.ToString(), Pending = false }, headers: InternalServiceHeaders(configuration), cancellationToken: cancellationToken);
                }
                catch (RpcException) { }
                throw;
            }
        }).WithName("CancelCustomerTransportOrder");

        var customer = app.MapGroup("/api/v1/transport-requests")
            .RequireAuthorization("Customer");
        customer.MapGet("/", async (int? page, int? pageSize, string? search, string? status, string? sortBy, string? sortDirection, ClaimsPrincipal principal, ITransportRequestService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var result = await service.GetCustomerRequestsAsync(GetUserId(principal), page ?? 0, pageSize ?? 20, search, status, sortBy, sortDirection, cancellationToken);
            return Results.Ok(ApiResponse<IReadOnlyList<TransportRequestListItem>>.Success(result.Items, ToPagination(result), requestId: GetRequestId(context)));
        }).WithName("ListCustomerTransportRequests");
        customer.MapPost("/", async (SaveTransportRequestRequest request, ClaimsPrincipal principal, ITransportRequestService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            Guid? idempotencyKey = null;
            if (context.Request.Headers.TryGetValue("Idempotency-Key", out var rawKey) && !string.IsNullOrWhiteSpace(rawKey))
            {
                if (!Guid.TryParse(rawKey, out var parsedKey) || parsedKey == Guid.Empty)
                {
                    var error = new ApiError { Field = "Idempotency-Key", Reason = "INVALID_IDEMPOTENCY_KEY", Message = "Idempotency-Key must be a non-empty UUID." };
                    return Results.BadRequest(ApiResponse<object>.Failure(40000, error.Message, new List<ApiError> { error }, GetRequestId(context)));
                }
                idempotencyKey = parsedKey;
            }
            var created = await service.CreateDraftAsync(GetUserId(principal), ToInput(request), idempotencyKey, cancellationToken);
            return Results.Created($"/api/v1/transport-requests/{created.Id}", ApiResponse<TransportRequestRecord>.Success(created, "Draft request created.", GetRequestId(context)));
        }).WithName("CreateTransportRequestDraft");
        customer.MapGet("/lead-time", (Guid originLocationId, Guid destinationLocationId, ITransportRequestService service, HttpContext context) =>
            Results.Ok(ApiResponse<BookingLeadTimeInfo>.Success(service.GetBookingLeadTime(originLocationId, destinationLocationId), requestId: GetRequestId(context))))
            .WithName("GetBookingRequestLeadTime");
        customer.MapGet("/horse-availability", async (
            [FromQuery(Name = "horseIds")] Guid[]? horseIds,
            Guid? requestId,
            ClaimsPrincipal principal,
            ITransportRequestService service,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetCustomerHorseAvailabilityAsync(GetUserId(principal), horseIds ?? [], requestId, cancellationToken);
            return Results.Ok(ApiResponse<IReadOnlyList<CustomerHorseAvailability>>.Success(result, requestId: GetRequestId(context)));
        }).WithName("GetCustomerHorseAvailabilityForTransportRequest");
        customer.MapGet("/{requestId:guid}", async (Guid requestId, ClaimsPrincipal principal, ITransportRequestService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var request = await service.GetCustomerRequestAsync(GetUserId(principal), requestId, cancellationToken);
            if (request is null)
            {
                var error = new ApiError { Field = "requestId", Reason = "TRANSPORT_REQUEST_NOT_FOUND", Message = "Transport request was not found." };
                return Results.NotFound(ApiResponse<object>.Failure(40400, "Transport request was not found.", new List<ApiError> { error }, GetRequestId(context)));
            }
            return Results.Ok(ApiResponse<TransportRequestRecord>.Success(request, requestId: GetRequestId(context)));
        }).WithName("GetCustomerTransportRequest");
        customer.MapGet("/{requestId:guid}/history", async (Guid requestId, ClaimsPrincipal principal, ITransportRequestService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var history = await service.GetCustomerRequestHistoryAsync(GetUserId(principal), requestId, cancellationToken);
            return Results.Ok(ApiResponse<IReadOnlyList<TransportRequestHistoryEntry>>.Success(history, requestId: GetRequestId(context)));
        }).WithName("GetCustomerTransportRequestHistory");
        customer.MapGet("/{requestId:guid}/document-checklist", async (
            Guid requestId,
            ClaimsPrincipal principal,
            ITransportRequestService service,
            PlanningQuery.PlanningQueryClient planning,
            ComplianceQuery.ComplianceQueryClient compliance,
            IConfiguration configuration,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var identityUserId = GetUserId(principal);
            var current = await service.GetCustomerRequestAsync(identityUserId, requestId, cancellationToken);
            if (current is null) return Results.NotFound(ApiResponse<object>.Failure(40400, "Transport request was not found.", requestId: GetRequestId(context)));
            if (current.OriginLocationId is null || current.DestinationLocationId is null)
                return Results.Ok(ApiResponse<TransportRequestDocumentChecklist>.Success(new(false, []), requestId: GetRequestId(context)));

            try
            {
                var route = await planning.GetRouteTransportModesAsync(new GetRouteTransportModesRequest
                {
                    OriginLocationId = current.OriginLocationId.Value.ToString(),
                    DestinationLocationId = current.DestinationLocationId.Value.ToString()
                }, headers: InternalServiceHeaders(configuration), cancellationToken: cancellationToken);
                if (!Guid.TryParse(route.OriginCountryId, out var originCountryId) || !Guid.TryParse(route.DestinationCountryId, out var destinationCountryId))
                    return Results.Problem("The selected locations do not have active country information.", statusCode: StatusCodes.Status503ServiceUnavailable);
                var checklist = await compliance.GetHorseDocumentChecklistAsync(ToChecklistRequest(identityUserId, current, originCountryId, destinationCountryId), headers: InternalServiceHeaders(configuration), cancellationToken: cancellationToken);
                return Results.Ok(ApiResponse<TransportRequestDocumentChecklist>.Success(MapChecklist(checklist), requestId: GetRequestId(context)));
            }
            catch (RpcException)
            {
                return Results.Problem("Compliance readiness could not be checked. Try again shortly.", statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        }).WithName("GetCustomerTransportRequestDocumentChecklist");
        customer.MapPut("/{requestId:guid}", async (Guid requestId, SaveTransportRequestRequest request, ClaimsPrincipal principal, ITransportRequestService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var updated = await service.UpdateDraftAsync(GetUserId(principal), requestId, ToInput(request), cancellationToken);
            return Results.Ok(ApiResponse<TransportRequestRecord>.Success(updated, "Draft request updated.", GetRequestId(context)));
        }).WithName("UpdateTransportRequestDraft");
        customer.MapPost("/{requestId:guid}/submit", async (Guid requestId, ClaimsPrincipal principal, ITransportRequestService service, PlanningQuery.PlanningQueryClient planning, ComplianceQuery.ComplianceQueryClient compliance, IConfiguration configuration, HttpContext context, CancellationToken cancellationToken) =>
        {
            var current = await service.GetCustomerRequestAsync(GetUserId(principal), requestId, cancellationToken);
            if (current is null) return Results.NotFound(ApiResponse<object>.Failure(40400, "Transport request was not found.", requestId: GetRequestId(context)));
            if (current.OriginLocationId is null || current.DestinationLocationId is null)
            {
                var error = new ApiError { Field = "originLocationId", Reason = "REQUEST_ROUTE_REQUIRED", Message = "Choose an origin and destination before submitting." };
                return Results.BadRequest(ApiResponse<object>.Failure(40000, error.Message, new List<ApiError> { error }, GetRequestId(context)));
            }
            if (current.HorseIds.Count == 0)
            {
                var error = new ApiError { Field = "horseIds", Reason = "REQUEST_HORSE_REQUIRED", Message = "Select at least one active horse before submitting." };
                return Results.BadRequest(ApiResponse<object>.Failure(40000, error.Message, new List<ApiError> { error }, GetRequestId(context)));
            }
            RouteTransportModesReply routeModes;
            try
            {
                routeModes = await planning.GetRouteTransportModesAsync(new GetRouteTransportModesRequest
                {
                    OriginLocationId = current.OriginLocationId?.ToString() ?? string.Empty,
                    DestinationLocationId = current.DestinationLocationId?.ToString() ?? string.Empty
                }, headers: InternalServiceHeaders(configuration), cancellationToken: cancellationToken);
            }
            catch (RpcException)
            {
                return Results.Problem("Route capabilities could not be checked. Try again shortly.", statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            if (!routeModes.Configured || current.PreferredTransportMode is null ||
                !routeModes.AllowedModes.Contains(current.PreferredTransportMode, StringComparer.OrdinalIgnoreCase))
            {
                var error = new ApiError { Field = "preferredTransportMode", Reason = "TRANSPORT_MODE_UNAVAILABLE_FOR_ROUTE", Message = "Choose a transport mode configured for this route before submitting." };
                return Results.BadRequest(ApiResponse<object>.Failure(40000, error.Message, new List<ApiError> { error }, GetRequestId(context)));
            }
            if (string.Equals(current.DeliveryPriority, "EXPRESS", StringComparison.OrdinalIgnoreCase) &&
                !routeModes.EstimatedDurations.Any(item => string.Equals(item.Mode, current.PreferredTransportMode, StringComparison.OrdinalIgnoreCase) && item.MinExpressDurationMinutes > 0 && item.MaxExpressDurationMinutes >= item.MinExpressDurationMinutes))
            {
                var error = new ApiError { Field = "deliveryPriority", Reason = "EXPRESS_UNAVAILABLE_FOR_ROUTE", Message = "Express delivery is not configured for this route and mode. Choose Standard or ask Logistics to configure it." };
                return Results.BadRequest(ApiResponse<object>.Failure(40000, error.Message, new List<ApiError> { error }, GetRequestId(context)));
            }
            if (!Guid.TryParse(routeModes.OriginCountryId, out var originCountryId) || !Guid.TryParse(routeModes.DestinationCountryId, out var destinationCountryId))
                return Results.Problem("The selected locations do not have active country information.", statusCode: StatusCodes.Status503ServiceUnavailable);
            HorseDocumentChecklistReply documents;
            try
            {
                documents = await compliance.GetHorseDocumentChecklistAsync(ToChecklistRequest(GetUserId(principal), current, originCountryId, destinationCountryId), headers: InternalServiceHeaders(configuration), cancellationToken: cancellationToken);
            }
            catch (RpcException)
            {
                return Results.Problem("Compliance readiness could not be checked. Try again shortly.", statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            if (documents.Items.Count == 0 || documents.Items.Any(item => !item.Uploaded))
            {
                var error = new ApiError { Field = "documents", Reason = "DOCUMENTS_INCOMPLETE", Message = "Upload every required horse document before submitting. Rejected documents do not count." };
                return Results.BadRequest(ApiResponse<object>.Failure(40000, error.Message, new List<ApiError> { error }, GetRequestId(context)));
            }
            var submitted = await service.SubmitAsync(GetUserId(principal), requestId, GetCorrelationId(context), cancellationToken);
            return Results.Ok(ApiResponse<TransportRequestRecord>.Success(submitted, "Request submitted.", GetRequestId(context)));
        }).WithName("SubmitTransportRequest");
        customer.MapPost("/{requestId:guid}/cancel", async (Guid requestId, CustomerCancellationRequest request, ClaimsPrincipal principal, ITransportRequestService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var cancelled = await service.CancelCustomerRequestAsync(GetUserId(principal), requestId, request.Reason, GetCorrelationId(context), cancellationToken);
            return Results.Ok(ApiResponse<TransportRequestRecord>.Success(cancelled, "Request cancelled.", GetRequestId(context)));
        }).WithName("CancelCustomerTransportRequest");

        var manager = app.MapGroup("/api/v1/manager/transport-requests")
            .RequireAuthorization("LogisticsManager");
        manager.MapGet("/", async (int? page, int? pageSize, string? search, string? status, string? sortBy, string? sortDirection, ClaimsPrincipal principal, ITransportRequestService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var result = await service.GetManagerQueueAsync(GetUserId(principal), page ?? 0, pageSize ?? 20, search, status, sortBy, sortDirection, cancellationToken);
            return Results.Ok(ApiResponse<IReadOnlyList<TransportRequestListItem>>.Success(result.Items, ToPagination(result), requestId: GetRequestId(context)));
        }).WithName("GetManagerRequestQueue");
        manager.MapGet("/{requestId:guid}", async (Guid requestId, ITransportRequestService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var request = await service.GetManagerRequestAsync(requestId, cancellationToken);
            if (request is null)
            {
                var error = new ApiError { Field = "requestId", Reason = "TRANSPORT_REQUEST_NOT_FOUND", Message = "Transport request was not found." };
                return Results.NotFound(ApiResponse<object>.Failure(40400, "Transport request was not found.", new List<ApiError> { error }, GetRequestId(context)));
            }
            return Results.Ok(ApiResponse<TransportRequestRecord>.Success(request, requestId: GetRequestId(context)));
        }).WithName("GetManagerTransportRequest");
        manager.MapGet("/{requestId:guid}/document-checklist", async (
            Guid requestId,
            ClaimsPrincipal principal,
            ITransportRequestService service,
            PlanningQuery.PlanningQueryClient planning,
            ComplianceQuery.ComplianceQueryClient compliance,
            IConfiguration configuration,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var current = await service.GetManagerRequestAsync(requestId, cancellationToken);
            if (current is null) return Results.NotFound(ApiResponse<object>.Failure(40400, "Transport request was not found.", requestId: GetRequestId(context)));
            if (current.OriginLocationId is null || current.DestinationLocationId is null)
                return Results.Ok(ApiResponse<TransportRequestDocumentChecklist>.Success(new(false, []), requestId: GetRequestId(context)));
            try
            {
                var route = await planning.GetRouteTransportModesAsync(new GetRouteTransportModesRequest
                {
                    OriginLocationId = current.OriginLocationId.Value.ToString(),
                    DestinationLocationId = current.DestinationLocationId.Value.ToString()
                }, headers: InternalServiceHeaders(configuration), cancellationToken: cancellationToken);
                if (!route.Configured || current.PreferredTransportMode is null || !route.AllowedModes.Contains(current.PreferredTransportMode, StringComparer.OrdinalIgnoreCase))
                    return Results.Conflict(ApiResponse<object>.Failure(40900, "The requested transport mode is no longer configured for this route."));
                if (string.Equals(current.DeliveryPriority, "EXPRESS", StringComparison.OrdinalIgnoreCase) &&
                    !route.EstimatedDurations.Any(item => string.Equals(item.Mode, current.PreferredTransportMode, StringComparison.OrdinalIgnoreCase) && item.MinExpressDurationMinutes > 0 && item.MaxExpressDurationMinutes >= item.MinExpressDurationMinutes))
                    return Results.Conflict(ApiResponse<object>.Failure(40900, "Express delivery is no longer configured for this route. Return the request for changes or update the route estimate."));
                if (!Guid.TryParse(route.OriginCountryId, out var originCountryId) || !Guid.TryParse(route.DestinationCountryId, out var destinationCountryId))
                    return Results.Problem("The selected locations do not have active country information.", statusCode: StatusCodes.Status503ServiceUnavailable);
                var checklist = await compliance.GetHorseDocumentChecklistAsync(ToChecklistRequest(current.CreatedByUserId, current, originCountryId, destinationCountryId), headers: InternalServiceHeaders(configuration), cancellationToken: cancellationToken);
                return Results.Ok(ApiResponse<TransportRequestDocumentChecklist>.Success(MapChecklist(checklist), requestId: GetRequestId(context)));
            }
            catch (RpcException)
            {
                return Results.Problem("Compliance checklist could not be checked. Try again shortly.", statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        }).WithName("GetManagerTransportRequestDocumentChecklist");
        manager.MapPost("/{requestId:guid}/horses/{horseId:guid}/documents/{documentId:guid}/approve", async (
            Guid requestId,
            Guid horseId,
            Guid documentId,
            ManagerApproveRequestDocumentRequest request,
            ClaimsPrincipal principal,
            ITransportRequestService service,
            ComplianceQuery.ComplianceQueryClient compliance,
            IConfiguration configuration,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var managerUserId = GetUserId(principal);
            var current = await service.GetManagerRequestAsync(requestId, cancellationToken);
            if (current is null) return Results.NotFound(ApiResponse<object>.Failure(40400, "Transport request was not found.", requestId: GetRequestId(context)));
            if (current.Status != "UNDER_REVIEW")
                return Results.Conflict(ApiResponse<object>.Failure(40900, "Take the request into review before approving its documents.", requestId: GetRequestId(context)));
            if (current.AssignedManagerUserId is not null && current.AssignedManagerUserId != managerUserId && principal.FindFirstValue("role") != "ADMIN")
                return Results.Forbid();
            if (!current.HorseIds.Contains(horseId))
                return Results.NotFound(ApiResponse<object>.Failure(40400, "The horse is not part of this request.", requestId: GetRequestId(context)));

            try
            {
                var result = await compliance.ReviewTransportRequestDocumentAsync(new ReviewTransportRequestDocumentRequest
                {
                    RequestId = requestId.ToString(),
                    HorseId = horseId.ToString(),
                    DocumentId = documentId.ToString(),
                    ReviewerUserId = managerUserId.ToString(),
                    CustomerUserId = current.CreatedByUserId.ToString(),
                    Comment = request.Comment ?? string.Empty
                }, headers: InternalServiceHeaders(configuration), cancellationToken: cancellationToken);
                return Results.Ok(ApiResponse<ReviewTransportRequestDocumentReply>.Success(result, "Request document approved.", GetRequestId(context)));
            }
            catch (RpcException error) when (error.StatusCode == StatusCode.NotFound)
            {
                return Results.NotFound(ApiResponse<object>.Failure(40400, error.Status.Detail, requestId: GetRequestId(context)));
            }
            catch (RpcException error) when (error.StatusCode == StatusCode.InvalidArgument)
            {
                return Results.BadRequest(ApiResponse<object>.Failure(40000, error.Status.Detail, requestId: GetRequestId(context)));
            }
            catch (RpcException)
            {
                return Results.Problem("The request document could not be reviewed. Try again shortly.", statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        }).WithName("ApproveTransportRequestDocument");
        manager.MapPost("/{requestId:guid}/start-review", async (Guid requestId, ClaimsPrincipal principal, ITransportRequestService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var request = await service.StartReviewAsync(GetUserId(principal), requestId, GetCorrelationId(context), cancellationToken);
            return Results.Ok(ApiResponse<TransportRequestRecord>.Success(request, "Review started.", GetRequestId(context)));
        }).WithName("StartTransportRequestReview");
        manager.MapPost("/{requestId:guid}/request-information", async (Guid requestId, ManagerDecisionRequest request, ClaimsPrincipal principal, ITransportRequestService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var updated = await service.RequestInformationAsync(GetUserId(principal), requestId, request.Reason, GetCorrelationId(context), cancellationToken);
            return Results.Ok(ApiResponse<TransportRequestRecord>.Success(updated, "Additional information requested.", GetRequestId(context)));
        }).WithName("RequestTransportInformation");
        manager.MapPost("/{requestId:guid}/approve", async (Guid requestId, ManagerDecisionRequest request, ClaimsPrincipal principal, ITransportRequestService service, PlanningQuery.PlanningQueryClient planning, ComplianceQuery.ComplianceQueryClient compliance, IConfiguration configuration, HttpContext context, CancellationToken cancellationToken) =>
        {
            var current = await service.GetManagerRequestAsync(requestId, cancellationToken);
            if (current is null) return Results.NotFound(ApiResponse<object>.Failure(40400, "Transport request was not found.", requestId: GetRequestId(context)));
            if (current.OriginLocationId is null || current.DestinationLocationId is null || current.HorseIds.Count == 0)
                return Results.Conflict(ApiResponse<object>.Failure(40900, "Request route and horses must be complete before approval.", requestId: GetRequestId(context)));
            HorseDocumentChecklistReply documents;
            try
            {
                var route = await planning.GetRouteTransportModesAsync(new GetRouteTransportModesRequest
                {
                    OriginLocationId = current.OriginLocationId.Value.ToString(),
                    DestinationLocationId = current.DestinationLocationId.Value.ToString()
                }, headers: InternalServiceHeaders(configuration), cancellationToken: cancellationToken);
                if (!Guid.TryParse(route.OriginCountryId, out var originCountryId) || !Guid.TryParse(route.DestinationCountryId, out var destinationCountryId))
                    return Results.Problem("The selected locations do not have active country information.", statusCode: StatusCodes.Status503ServiceUnavailable);
                documents = await compliance.GetHorseDocumentChecklistAsync(ToChecklistRequest(current.CreatedByUserId, current, originCountryId, destinationCountryId), headers: InternalServiceHeaders(configuration), cancellationToken: cancellationToken);
            }
            catch (RpcException)
            {
                return Results.Problem("Compliance approvals could not be checked. Try again shortly.", statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            if (documents.Items.Count == 0 || documents.Items.Any(item => !item.Met))
            {
                var blockers = documents.Items.Where(item => !item.Met)
                    .Select(item => new ApiError { Field = "documents", Reason = "DOCUMENT_NOT_APPROVED", Message = $"{item.DocumentTypeName} for horse {item.HorseId} must be approved and valid before request approval." })
                    .ToList();
                if (blockers.Count == 0) blockers.Add(new ApiError { Field = "documents", Reason = "DOCUMENTS_INCOMPLETE", Message = "Required horse documents are not configured or available." });
                return Results.Conflict(ApiResponse<object>.Failure(40900, "Required horse documents must be approved before the request can be approved.", blockers, GetRequestId(context)));
            }
            var approved = await service.ApproveAsync(GetUserId(principal), requestId, request.Reason, GetCorrelationId(context), cancellationToken, principal.FindFirstValue("name") ?? principal.FindFirstValue(ClaimTypes.Name));
            return Results.Ok(ApiResponse<TransportRequestRecord>.Success(approved, "Request approved and order created.", GetRequestId(context)));
        }).WithName("ApproveTransportRequest");
        manager.MapPost("/{requestId:guid}/reject", async (Guid requestId, ManagerDecisionRequest request, ClaimsPrincipal principal, ITransportRequestService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var rejected = await service.RejectAsync(GetUserId(principal), requestId, request.Reason, GetCorrelationId(context), cancellationToken);
            return Results.Ok(ApiResponse<TransportRequestRecord>.Success(rejected, "Request rejected.", GetRequestId(context)));
        }).WithName("RejectTransportRequest");
    }

    private static Guid GetUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue("sub"), out var userId) ? userId : throw new UnauthorizedException();

    private static Guid GetRequestId(HttpContext context) =>
        Guid.TryParse(context.Items["X-Request-ID"]?.ToString(), out var requestId) ? requestId : Guid.NewGuid();

    private static Guid GetCorrelationId(HttpContext context) =>
        Guid.TryParse(context.Items["X-Correlation-ID"]?.ToString(), out var correlationId) ? correlationId : Guid.NewGuid();

    private static PaginationMeta ToPagination<T>(PageResult<T> result) => new()
    {
        Page = result.Page,
        PageSize = result.PageSize,
        TotalItems = result.TotalItems,
        TotalPages = result.TotalPages
    };

    private static TransportRequestInput ToInput(SaveTransportRequestRequest request) => new(
        request.OriginLocationId,
        request.DestinationLocationId,
        request.RequestedDepartureAt,
        request.RequestedArrivalAt,
        request.PreferredTransportMode,
        request.SpecialRequirements,
        request.Notes,
        request.HorseIds,
        request.VersionNo,
        request.DeliveryPriority,
        CustomPickupLatitude: request.CustomPickupLatitude,
        CustomPickupLongitude: request.CustomPickupLongitude,
        CustomPickupLabel: request.CustomPickupLabel);

    private static GetHorseDocumentChecklistRequest ToChecklistRequest(Guid identityUserId, TransportRequestRecord request, Guid originCountryId, Guid destinationCountryId)
    {
        var checklist = new GetHorseDocumentChecklistRequest
        {
            CustomerUserId = identityUserId.ToString(),
            OriginCountryId = originCountryId.ToString(),
            DestinationCountryId = destinationCountryId.ToString(),
            DepartureDate = DateOnly.FromDateTime(request.RequestedDepartureAt.UtcDateTime).ToString("yyyy-MM-dd"),
            RequestId = request.Id.ToString()
        };
        checklist.HorseIds.AddRange(request.HorseIds.Select(id => id.ToString()));
        return checklist;
    }

    private static TransportRequestDocumentChecklist MapChecklist(HorseDocumentChecklistReply reply)
    {
        var items = reply.Items.Select(item => new TransportRequestDocumentChecklistItem(
            Guid.TryParse(item.HorseId, out var horseId) ? horseId : Guid.Empty,
            item.DocumentTypeCode,
            item.DocumentTypeName,
            item.Met,
            item.Uploaded,
            item.Status,
            string.IsNullOrWhiteSpace(item.DocumentNo) ? null : item.DocumentNo,
            DateOnly.TryParse(item.ExpiryDate, out var expiry) ? expiry : null)).ToList();
        return new TransportRequestDocumentChecklist(items.Count > 0 && items.All(item => item.Met), items);
    }

    private static Metadata InternalServiceHeaders(IConfiguration configuration)
    {
        var key = configuration["Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Jwt:SigningKey is required for internal service calls.");
        return new Metadata { { "x-internal-service-key", key } };
    }
}

public sealed record SaveTransportRequestRequest
{
    [Required]
    public Guid OriginLocationId { get; init; }

    [Required]
    public Guid DestinationLocationId { get; init; }

    [Required]
    public DateTimeOffset RequestedDepartureAt { get; init; }

    public DateTimeOffset? RequestedArrivalAt { get; init; }

    [MaxLength(20)]
    public string? DeliveryPriority { get; init; } = "STANDARD";

    [MaxLength(30)]
    public string? PreferredTransportMode { get; init; }

    [MaxLength(2000)]
    public string? SpecialRequirements { get; init; }

    [MaxLength(2000)]
    public string? Notes { get; init; }

    public Guid[] HorseIds { get; init; } = [];

    public int? VersionNo { get; init; }
    public decimal? CustomPickupLatitude { get; init; }
    public decimal? CustomPickupLongitude { get; init; }
    [MaxLength(300)] public string? CustomPickupLabel { get; init; }
}

public sealed record ManagerDecisionRequest
{
    [MaxLength(2000)]
    public string Reason { get; init; } = string.Empty;
}

public sealed record ManagerApproveRequestDocumentRequest
{
    [MaxLength(2000)]
    public string? Comment { get; init; }
}
