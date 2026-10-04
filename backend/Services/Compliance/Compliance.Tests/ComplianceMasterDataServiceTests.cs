using Compliance.Application.DTOs.MasterData;
using Compliance.Infrastructure.Services;
using Xunit;

namespace Compliance.Tests;

public class ComplianceMasterDataServiceTests
{
    [Fact]
    public async Task CreateDocumentType_ReturnsNewType()
    {
        using var db = TestDbContextFactory.Create();
        var svc = new ComplianceMasterDataService(db);

        var result = await svc.CreateDocumentTypeAsync(
            new CreateDocumentTypeRequest("HEALTH_CERT", "Health Certificate", "Horse", "Horse health certificate"),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Health Certificate", result.Name);
        Assert.Equal("HEALTH_CERT", result.Code);
        Assert.Equal("HORSE", result.Scope);
        Assert.True(result.Active);
    }

    [Fact]
    public async Task GetDocumentTypes_FiltersActive()
    {
        using var db = TestDbContextFactory.Create();
        var svc = new ComplianceMasterDataService(db);

        await svc.CreateDocumentTypeAsync(
            new CreateDocumentTypeRequest("ACT", "Active Type", "Horse"),
            CancellationToken.None);

        // Need to create and then deactivate via update
        var inactive = await svc.CreateDocumentTypeAsync(
            new CreateDocumentTypeRequest("INACT", "Inactive Type", "Horse"),
            CancellationToken.None);
        await svc.UpdateDocumentTypeAsync(inactive.Id,
            new UpdateDocumentTypeRequest("Inactive Type", "Horse", Active: false),
            CancellationToken.None);

        var activeOnly = await svc.GetDocumentTypesAsync(null, true, CancellationToken.None);
        Assert.All(activeOnly, t => Assert.True(t.Active));

        var inactiveOnly = await svc.GetDocumentTypesAsync(null, false, CancellationToken.None);
        Assert.All(inactiveOnly, t => Assert.False(t.Active));
    }

    [Fact]
    public async Task GetDocumentTypes_FiltersByScope()
    {
        using var db = TestDbContextFactory.Create();
        var svc = new ComplianceMasterDataService(db);

        await svc.CreateDocumentTypeAsync(
            new CreateDocumentTypeRequest("HD1", "Horse Doc", "Horse"),
            CancellationToken.None);

        await svc.CreateDocumentTypeAsync(
            new CreateDocumentTypeRequest("TD1", "Trip Doc", "Trip"),
            CancellationToken.None);

        var horseTypes = await svc.GetDocumentTypesAsync("Horse", null, CancellationToken.None);
        Assert.Single(horseTypes);
        Assert.Equal("HORSE", horseTypes[0].Scope);
    }

    [Fact]
    public async Task GetDocumentTypeById_NotFound_ReturnsNull()
    {
        using var db = TestDbContextFactory.Create();
        var svc = new ComplianceMasterDataService(db);

        var result = await svc.GetDocumentTypeByIdAsync(Guid.NewGuid(), CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateDocumentType_UpdatesFields()
    {
        using var db = TestDbContextFactory.Create();
        var svc = new ComplianceMasterDataService(db);

        var created = await svc.CreateDocumentTypeAsync(
            new CreateDocumentTypeRequest("ON", "Old Name", "Horse"),
            CancellationToken.None);

        var updated = await svc.UpdateDocumentTypeAsync(
            created.Id,
            new UpdateDocumentTypeRequest("New Name", "Trip", "Updated desc", 30, false),
            CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("New Name", updated!.Name);
        Assert.Equal("TRIP", updated.Scope);
        Assert.False(updated.Active);
    }

    [Fact]
    public async Task CreateRegulationRule_ReturnsNewRule()
    {
        using var db = TestDbContextFactory.Create();
        var svc = new ComplianceMasterDataService(db);

        var originId = Guid.NewGuid();
        var destId = Guid.NewGuid();
        var result = await svc.CreateRegulationRuleAsync(
            new CreateRegulationRuleRequest(
                "RULE-01", originId, destId, null, "Test Rule", "desc",
                true, false, true,
                DateOnly.FromDateTime(DateTime.UtcNow)),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("RULE-01", result.RuleCode);
        Assert.Equal("Test Rule", result.Title);
        Assert.Equal(originId, result.OriginCountryId);
        Assert.Equal(destId, result.DestinationCountryId);
    }

    [Fact]
    public async Task GetRegulationRules_FiltersCountry()
    {
        using var db = TestDbContextFactory.Create();
        var svc = new ComplianceMasterDataService(db);

        var originA = Guid.NewGuid();
        var originB = Guid.NewGuid();
        var dest = Guid.NewGuid();

        await svc.CreateRegulationRuleAsync(
            new CreateRegulationRuleRequest(
                "RA", originA, dest, null, "Rule A", null,
                false, false, false,
                DateOnly.FromDateTime(DateTime.UtcNow)),
            CancellationToken.None);

        await svc.CreateRegulationRuleAsync(
            new CreateRegulationRuleRequest(
                "RB", originB, dest, null, "Rule B", null,
                false, false, false,
                DateOnly.FromDateTime(DateTime.UtcNow)),
            CancellationToken.None);

        var filtered = await svc.GetRegulationRulesAsync(originA, null, null, CancellationToken.None);
        Assert.Single(filtered);
        Assert.Equal("Rule A", filtered[0].Title);
    }

    [Fact]
    public async Task LookupRules_ReturnsMatchingRules()
    {
        using var db = TestDbContextFactory.Create();
        var svc = new ComplianceMasterDataService(db);

        var origin = Guid.NewGuid();
        var dest = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await svc.CreateRegulationRuleAsync(
            new CreateRegulationRuleRequest(
                "R-ACTIVE", origin, dest, null, "Active Rule", null,
                false, false, false,
                today.AddDays(-10)),
            CancellationToken.None);

        await svc.CreateRegulationRuleAsync(
            new CreateRegulationRuleRequest(
                "R-FUTURE", origin, dest, null, "Future Rule", null,
                false, false, false,
                today.AddDays(10)),
            CancellationToken.None);

        var result = await svc.LookupRulesAsync(origin, dest, today, CancellationToken.None);
        Assert.Single(result);
        Assert.Equal("Active Rule", result[0].Title);
    }
}
