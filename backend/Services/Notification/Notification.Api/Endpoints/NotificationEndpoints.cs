using System.Security.Claims;
using BuildingBlocks.Api;
using Microsoft.AspNetCore.Mvc;
using Notification.Application.DTOs;
using Notification.Application.Services;

namespace Notification.Api.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/notifications")
            .WithTags("Notifications")
            .RequireAuthorization();

        group.MapGet("/", async (
            [FromQuery] bool? unreadOnly,
            [FromQuery] int page,
            [FromQuery] int pageSize,
            INotificationService service,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            var result = await service.GetUserNotificationsAsync(userId, unreadOnly, page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize, ct);
            var pagination = new PaginationMeta
            {
                Page = result.PageNumber,
                PageSize = result.PageSize,
                TotalItems = result.TotalCount,
                TotalPages = result.TotalPages
            };
            return Results.Ok(ApiResponse<IReadOnlyList<NotificationDto>>.Success(result.Items, pagination));
        })
        .WithName("GetUserNotifications")
        .WithSummary("List user notifications with optional unread filter");

        group.MapGet("/unread-count", async (
            INotificationService service,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            var result = await service.GetUnreadCountAsync(userId, ct);
            return Results.Ok(ApiResponse<UnreadCountDto>.Success(result));
        })
        .WithName("GetUnreadNotificationCount")
        .WithSummary("Get unread notification count badge for the current user");

        group.MapPatch("/{id:guid}/read", async (
            Guid id,
            INotificationService service,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            var success = await service.MarkAsReadAsync(id, userId, ct);
            return success
                ? Results.Ok(ApiResponse<object>.Success(new { id, read = true }, "Notification marked as read."))
                : Results.NotFound(ApiResponse<object>.Failure(404, "Notification not found."));
        })
        .WithName("MarkNotificationAsRead")
        .WithSummary("Mark a single notification as read");

        group.MapPost("/read-all", async (
            INotificationService service,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            var updatedCount = await service.MarkAllAsReadAsync(userId, ct);
            return Results.Ok(ApiResponse<object>.Success(new { updatedCount }, "All notifications marked as read."));
        })
        .WithName("MarkAllNotificationsAsRead")
        .WithSummary("Mark all unread notifications as read");

        group.MapPost("/device-tokens", async (
            [FromBody] RegisterDeviceTokenRequest request,
            INotificationService service,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            await service.RegisterDeviceTokenAsync(userId, request, ct);
            return Results.Ok(ApiResponse<object>.Success(new { success = true }, "Device token registered."));
        })
        .WithName("RegisterDeviceToken")
        .WithSummary("Register mobile push device token");

        group.MapDelete("/device-tokens/{token}", async (
            string token,
            INotificationService service,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            await service.UnregisterDeviceTokenAsync(userId, token, ct);
            return Results.Ok(ApiResponse<object>.Success(new { unregistered = true }, "Device token deactivated."));
        })
        .WithName("UnregisterDeviceToken")
        .WithSummary("Deactivate mobile push device token");

        return endpoints;
    }

    private static Guid GetUserId(ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? user.FindFirstValue("sub")
               ?? user.FindFirstValue("userId");

        if (Guid.TryParse(sub, out var parsed))
        {
            return parsed;
        }

        return Guid.Parse("00000000-0000-0000-0000-000000000001");
    }
}
