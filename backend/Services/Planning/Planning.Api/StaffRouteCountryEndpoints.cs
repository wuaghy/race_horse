using BuildingBlocks.Api;
using BuildingBlocks.Exceptions;
using Microsoft.EntityFrameworkCore;
using Planning.Application.Abstractions;
using Planning.Application.DTOs.Planning;
using Planning.Domain;

namespace Planning.Api;

public static class StaffRouteCountryEndpoints
{
    public static RouteGroupBuilder MapStaffRouteCountryEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/staff/{userId:guid}/route-countries").RequireAuthorization("PlanningRead");

        group.MapGet("", async (Guid userId, IPlanningDbContext db, CancellationToken ct) =>
        {
            var rows = await (from authorization in db.StaffRouteCountryAuthorizations.AsNoTracking()
                              join country in db.Countries.AsNoTracking() on authorization.CountryId equals country.Id
                              where authorization.UserId == userId
                              orderby country.Name
                              select new StaffRouteCountryDto(country.Id, country.Name, authorization.ValidUntil))
                .ToListAsync(ct);
            return Results.Ok(ApiResponse<IReadOnlyList<StaffRouteCountryDto>>.Success(rows));
        }).WithName("GetStaffRouteCountryAuthorizations");

        group.MapPut("", async (Guid userId, StaffRouteCountriesInput input, IPlanningDbContext db, CancellationToken ct) =>
        {
            if (userId == Guid.Empty || input.CountryIds is null || input.CountryIds.Count > 100 || input.CountryIds.Any(id => id == Guid.Empty))
                throw new BusinessRuleException("Choose up to 100 valid countries.", "INVALID_STAFF_ROUTE_COUNTRIES");

            if (input.ValidUntil is { } validUntil && validUntil < DateOnly.FromDateTime(DateTime.UtcNow))
                throw new BusinessRuleException("The authorization end date cannot be in the past.", "STAFF_AUTHORIZATION_EXPIRED", "validUntil");

            var countryIds = input.CountryIds.Distinct().ToArray();
            var activeCountryIds = countryIds.Length == 0 ? new List<Guid>() : await db.Countries.AsNoTracking()
                .Where(country => country.Active && countryIds.Contains(country.Id))
                .Select(country => country.Id)
                .ToListAsync(ct);
            if (activeCountryIds.Count != countryIds.Length)
                throw new BusinessRuleException("Every selected country must exist and be active.", "STAFF_ROUTE_COUNTRY_INACTIVE", "countryIds");

            var existing = await db.StaffRouteCountryAuthorizations.Where(item => item.UserId == userId).ToListAsync(ct);
            db.StaffRouteCountryAuthorizations.RemoveRange(existing);
            foreach (var countryId in countryIds)
                db.StaffRouteCountryAuthorizations.Add(new StaffRouteCountryAuthorization
                {
                    UserId = userId,
                    CountryId = countryId,
                    ValidUntil = input.ValidUntil
                });

            await db.SaveChangesAsync(ct);
            var result = await (from authorization in db.StaffRouteCountryAuthorizations.AsNoTracking()
                                join country in db.Countries.AsNoTracking() on authorization.CountryId equals country.Id
                                where authorization.UserId == userId
                                orderby country.Name
                                select new StaffRouteCountryDto(country.Id, country.Name, authorization.ValidUntil))
                .ToListAsync(ct);
            return Results.Ok(ApiResponse<IReadOnlyList<StaffRouteCountryDto>>.Success(result, "Staff route countries updated."));
        }).RequireAuthorization("PlanningDataAdmin").WithName("ReplaceStaffRouteCountryAuthorizations");

        return group;
    }
}
