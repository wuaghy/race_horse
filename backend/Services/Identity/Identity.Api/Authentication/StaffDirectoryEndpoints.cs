using BuildingBlocks.Api;
using Identity.Application.Authentication;

namespace Identity.Api.Authentication;

public static class StaffDirectoryEndpoints
{
    public static IEndpointRouteBuilder MapStaffDirectoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/v1/manager/staff")
            .RequireAuthorization("StaffDirectory")
            .MapGet("/", async (IIdentityStore store, CancellationToken cancellationToken) =>
            {
                var accounts = await store.ListStaffAccountsAsync(cancellationToken);
                return Results.Ok(ApiResponse<IReadOnlyList<StaffDirectoryAccount>>.Success(accounts));
            })
            .WithName("ListActiveTransportStaff");

        endpoints.MapGroup("/api/v1/staff/display-names")
            .RequireAuthorization()
            .MapGet("/", async (Guid[] ids, IIdentityStore store, CancellationToken cancellationToken) =>
            {
                if (ids.Length is < 1 or > 100 || ids.Any(id => id == Guid.Empty))
                    return Results.BadRequest(ApiResponse<object>.Failure(40000, "Provide between 1 and 100 valid staff IDs."));
                var names = await store.GetStaffDisplayNamesAsync(ids.Distinct().ToArray(), cancellationToken);
                return Results.Ok(ApiResponse<IReadOnlyList<StaffDisplayName>>.Success(names));
            })
            .WithName("GetStaffDisplayNames");

        return endpoints;
    }
}
