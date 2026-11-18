using Compliance.Api.Services;
using Compliance.Domain.Entities;
using Compliance.Grpc;
using Compliance.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Compliance.Tests;

public class ComplianceRpcServiceTests
{
    [Fact]
    public async Task VerifyTripCompliance_ReturnsNonCompliant_WhenDocumentsMissing()
    {
        using var db = TestDbContextFactory.Create();
        var readinessService = new ComplianceReadinessService(db, NullLogger<ComplianceReadinessService>.Instance);
        var rpcService = new ComplianceRpcService(readinessService, NullLogger<ComplianceRpcService>.Instance);

        var tripId = Guid.NewGuid();
        var originId = Guid.NewGuid();
        var destId = Guid.NewGuid();
        var horseId = Guid.NewGuid();

        var docType = new DocumentType
        {
            Id = Guid.NewGuid(),
            Code = "HEALTH_CERT",
            Name = "Health Certificate",
            Scope = "HORSE",
            Active = true
        };
        db.DocumentTypes.Add(docType);

        var rule = new RegulationRule
        {
            Id = Guid.NewGuid(),
            RuleCode = "RULE_HEALTH",
            OriginCountryId = originId,
            DestinationCountryId = destId,
            Title = "Mandatory Health Cert",
            Active = true,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10))
        };
        rule.Requirements.Add(new RegulationDocumentRequirement
        {
            Id = Guid.NewGuid(),
            RegulationRuleId = rule.Id,
            DocumentTypeId = docType.Id,
            DocumentType = docType,
            Required = true
        });
        db.RegulationRules.Add(rule);
        await db.SaveChangesAsync();

        var request = new VerifyTripComplianceRequest
        {
            TripId = tripId.ToString(),
            OriginCountryId = originId.ToString(),
            DestinationCountryId = destId.ToString(),
            PlannedDepartureDate = DateTime.UtcNow.ToString("yyyy-MM-dd")
        };
        request.HorseIds.Add(horseId.ToString());

        var response = await rpcService.VerifyTripCompliance(request, null!);

        Assert.Equal(tripId.ToString(), response.TripId);
        Assert.False(response.IsCompliant);
        Assert.Equal(1, response.TotalRequired);
        Assert.Equal(0, response.TotalMet);
        Assert.Equal(1, response.TotalMissing);
        Assert.NotEmpty(response.BlockerMessages);
        Assert.Single(response.CheckItems);
        Assert.Equal("HEALTH_CERT", response.CheckItems[0].DocumentTypeCode);
        Assert.False(response.CheckItems[0].IsMet);
    }

    [Fact]
    public async Task VerifyTripCompliance_ReturnsCompliant_WhenAllRequirementsMet()
    {
        using var db = TestDbContextFactory.Create();
        var readinessService = new ComplianceReadinessService(db, NullLogger<ComplianceReadinessService>.Instance);
        var rpcService = new ComplianceRpcService(readinessService, NullLogger<ComplianceRpcService>.Instance);

        var tripId = Guid.NewGuid();
        var originId = Guid.NewGuid();
        var destId = Guid.NewGuid();
        var horseId = Guid.NewGuid();

        var docType = new DocumentType
        {
            Id = Guid.NewGuid(),
            Code = "COGGINS",
            Name = "Coggins Test",
            Scope = "HORSE",
            Active = true
        };
        db.DocumentTypes.Add(docType);

        var rule = new RegulationRule
        {
            Id = Guid.NewGuid(),
            RuleCode = "RULE_COGGINS",
            OriginCountryId = originId,
            DestinationCountryId = destId,
            Title = "Coggins Rule",
            Active = true,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10))
        };
        rule.Requirements.Add(new RegulationDocumentRequirement
        {
            Id = Guid.NewGuid(),
            RegulationRuleId = rule.Id,
            DocumentTypeId = docType.Id,
            DocumentType = docType,
            Required = true
        });
        db.RegulationRules.Add(rule);

        var doc = new Document
        {
            Id = Guid.NewGuid(),
            TripId = tripId,
            HorseId = horseId,
            DocumentTypeId = docType.Id,
            DocumentType = docType,
            DocumentNo = "COG-12345",
            Status = DocumentStatus.Approved,
            UploadedAt = DateTime.UtcNow,
            ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30))
        };
        db.Documents.Add(doc);
        await db.SaveChangesAsync();

        var request = new VerifyTripComplianceRequest
        {
            TripId = tripId.ToString(),
            OriginCountryId = originId.ToString(),
            DestinationCountryId = destId.ToString(),
            PlannedDepartureDate = DateTime.UtcNow.ToString("yyyy-MM-dd")
        };
        request.HorseIds.Add(horseId.ToString());

        var response = await rpcService.VerifyTripCompliance(request, null!);

        Assert.Equal(tripId.ToString(), response.TripId);
        Assert.True(response.IsCompliant);
        Assert.Equal(1, response.TotalRequired);
        Assert.Equal(1, response.TotalMet);
        Assert.Equal(0, response.TotalMissing);
        Assert.Empty(response.BlockerMessages);
        Assert.Single(response.CheckItems);
        Assert.True(response.CheckItems[0].IsMet);
    }
}
