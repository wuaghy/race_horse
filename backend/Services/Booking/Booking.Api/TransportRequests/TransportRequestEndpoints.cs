using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Booking.Application.Customers;
using Booking.Application.TransportRequests;
using BuildingBlocks.Api;
using BuildingBlocks.Exceptions;

namespace Booking.Api.TransportRequests;

public static class TransportRequestEndpoints
{
    public static void MapTransportRequestEndpoints(this WebApplication app)
    {
        var customer = app.MapGroup("/api/v1/transport-requests")
            .RequireAuthorization("Customer");
        customer.MapGet("/", async (int? page, int? pageSize, string? search, ClaimsPrincipal principal, ITransportRequestService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var result = await service.GetCustomerRequestsAsync(GetUserId(principal), page ?? 0, pageSize ?? 20, search, cancellationToken);
            return Results.Ok(ApiResponse<IReadOnlyList<TransportRequestListItem>>.Success(result.Items, ToPagination(result), requestId: GetRequestId(context)));
        }).WithName("ListCustomerTransportRequests");
        customer.MapPost("/", async (SaveTransportRequestRequest request, ClaimsPrincipal principal, ITransportRequestService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var created = await service.CreateDraftAsync(GetUserId(principal), ToInput(request), cancellationToken);
            return Results.Created($"/api/v1/transport-requests/{created.Id}", ApiResponse<TransportRequestRecord>.Success(created, "Draft request created.", GetRequestId(context)));
        }).WithName("CreateTransportRequestDraft");
        customer.MapGet("/{requestId:guid}", async (Guid requestId, ClaimsPrincipal principal, ITransportRequestService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var request = await service.GetCustomerRequestAsync(GetUserId(principal), requestId, cancellationToken);
            return request is null
                ? Results.NotFound(ApiResponse<object>.Failure(40400, "Transport request was not found.", requestId: GetRequestId(context)))
                : Results.Ok(ApiResponse<TransportRequestRecord>.Success(request, requestId: GetRequestId(context)));
        }).WithName("GetCustomerTransportRequest");
        customer.MapPut("/{requestId:guid}", async (Guid requestId, SaveTransportRequestRequest request, ClaimsPrincipal principal, ITransportRequestService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var updated = await service.UpdateDraftAsync(GetUserId(principal), requestId, ToInput(request), cancellationToken);
            return Results.Ok(ApiResponse<TransportRequestRecord>.Success(updated, "Draft request updated.", GetRequestId(context)));
        }).WithName("UpdateTransportRequestDraft");
        customer.MapPost("/{requestId:guid}/submit", async (Guid requestId, ClaimsPrincipal principal, ITransportRequestService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var submitted = await service.SubmitAsync(GetUserId(principal), requestId, GetCorrelationId(context), cancellationToken);
            return Results.Ok(ApiResponse<TransportRequestRecord>.Success(submitted, "Request submitted.", GetRequestId(context)));
        }).WithName("SubmitTransportRequest");

        var manager = app.MapGroup("/api/v1/manager/transport-requests")
            .RequireAuthorization("LogisticsManager");
        manager.MapGet("/", async (int? page, int? pageSize, ClaimsPrincipal principal, ITransportRequestService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var result = await service.GetManagerQueueAsync(GetUserId(principal), page ?? 0, pageSize ?? 20, cancellationToken);
            return Results.Ok(ApiResponse<IReadOnlyList<TransportRequestListItem>>.Success(result.Items, ToPagination(result), requestId: GetRequestId(context)));
        }).WithName("GetManagerRequestQueue");
        manager.MapGet("/{requestId:guid}", async (Guid requestId, ITransportRequestService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var request = await service.GetManagerRequestAsync(requestId, cancellationToken);
            return request is null
                ? Results.NotFound(ApiResponse<object>.Failure(40400, "Transport request was not found.", requestId: GetRequestId(context)))
                : Results.Ok(ApiResponse<TransportRequestRecord>.Success(request, requestId: GetRequestId(context)));
        }).WithName("GetManagerTransportRequest");
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
        manager.MapPost("/{requestId:guid}/approve", async (Guid requestId, ManagerDecisionRequest request, ClaimsPrincipal principal, ITransportRequestService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var approved = await service.ApproveAsync(GetUserId(principal), requestId, request.Reason, GetCorrelationId(context), cancellationToken);
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
        request.VersionNo);
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

    [MaxLength(30)]
    public string? PreferredTransportMode { get; init; }

    [MaxLength(2000)]
    public string? SpecialRequirements { get; init; }

    [MaxLength(2000)]
    public string? Notes { get; init; }

    public Guid[] HorseIds { get; init; } = [];

    public int? VersionNo { get; init; }
}

public sealed record ManagerDecisionRequest
{
    [Required, MinLength(1), MaxLength(2000)]
    public required string Reason { get; init; }
}
