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

        return endpoints;
    }
}
