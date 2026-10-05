using Compliance.Application.DTOs.Readiness;
using Compliance.Domain.Entities;
using Compliance.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Compliance.Tests;

public class ComplianceReadinessServiceTests
{
    [Fact]
    public async Task EvaluateTripReadiness_NoRules_FailsClosedWithoutOutbox()
    {
        using var db = TestDbContextFactory.Create();
        var logger = NullLogger<ComplianceReadinessService>.Instance;
        var svc = new ComplianceReadinessService(db, logger);

        var tripId = Guid.NewGuid();
        var request = new EvaluateComplianceReadinessRequest(
            tripId, Guid.NewGuid(), Guid.NewGuid(),
            new List<Guid>(), DateOnly.FromDateTime(DateTime.UtcNow));

        var result = await svc.EvaluateTripReadinessAsync(request, CancellationToken.None);

        Assert.False(result.IsReady);
        Assert.Equal(0, result.TotalRequirements);

        var outbox = db.OutboxMessages.ToList();
        Assert.Empty(outbox);
    }

    [Fact]
    public async Task EvaluateTripReadiness_MissingRequiredDoc_ReturnsReadyFalse()
    {
        using var db = TestDbContextFactory.Create();
        var logger = NullLogger<ComplianceReadinessService>.Instance;
        var svc = new ComplianceReadinessService(db, logger);

        var origin = Guid.NewGuid();
        var dest = Guid.NewGuid();
        var horseId = Guid.NewGuid();
        var docTypeId = Guid.NewGuid();

        var docType = new DocumentType
        {
            Id = docTypeId,
            Code = "VET_CERT",
            Name = "Veterinary Certificate",
            Scope = "HORSE",
            Active = true
        };
        db.DocumentTypes.Add(docType);

        var rule = new RegulationRule
        {
            Id = Guid.NewGuid(),
            RuleCode = "RULE_VET",
            OriginCountryId = origin,
            DestinationCountryId = dest,
            Title = "Veterinary Rule",
            Active = true,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            Requirements = new List<RegulationDocumentRequirement>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    DocumentTypeId = docTypeId,
                    DocumentType = docType,
                    Required = true
                }
            }
        };
        db.RegulationRules.Add(rule);
        await db.SaveChangesAsync();

        var request = new EvaluateComplianceReadinessRequest(
            Guid.NewGuid(), origin, dest,
            new List<Guid> { horseId }, DateOnly.FromDateTime(DateTime.UtcNow));

        var result = await svc.EvaluateTripReadinessAsync(request, CancellationToken.None);

        Assert.False(result.IsReady);
        Assert.Equal(1, result.TotalRequirements);
        Assert.Equal(0, result.MetRequirements);
        Assert.Equal(1, result.MissingRequirements);
        Assert.Contains(result.Items, c => c.Status == "MISSING");

        // No outbox message created
        Assert.Empty(db.OutboxMessages);
    }

    [Fact]
    public async Task EvaluateTripReadiness_AllDocsApproved_ReturnsReadyTrueAndCreatesOutbox()
    {
        using var db = TestDbContextFactory.Create();
        var logger = NullLogger<ComplianceReadinessService>.Instance;
        var svc = new ComplianceReadinessService(db, logger);

        var origin = Guid.NewGuid();
        var dest = Guid.NewGuid();
        var horseId = Guid.NewGuid();
        var docTypeId = Guid.NewGuid();

        var docType = new DocumentType
        {
            Id = docTypeId,
            Code = "PASSPORT",
            Name = "Horse Passport",
            Scope = "HORSE",
            Active = true
        };
        db.DocumentTypes.Add(docType);

        var rule = new RegulationRule
        {
            Id = Guid.NewGuid(),
            RuleCode = "RULE_PASS",
            OriginCountryId = origin,
            DestinationCountryId = dest,
            Title = "Passport Rule",
            Active = true,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            Requirements = new List<RegulationDocumentRequirement>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    DocumentTypeId = docTypeId,
                    DocumentType = docType,
                    Required = true
                }
            }
        };
        db.RegulationRules.Add(rule);

        var doc = new Document
        {
            Id = Guid.NewGuid(),
            DocumentTypeId = docTypeId,
            DocumentType = docType,
            HorseId = horseId,
            FileName = "passport.pdf",
            FileUrl = "https://storage/passport.pdf",
            Status = DocumentStatus.Approved,
            ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1))
        };
        db.Documents.Add(doc);
        await db.SaveChangesAsync();

        var tripId = Guid.NewGuid();
        var request = new EvaluateComplianceReadinessRequest(
            tripId, origin, dest,
            new List<Guid> { horseId }, DateOnly.FromDateTime(DateTime.UtcNow));

        var result = await svc.EvaluateTripReadinessAsync(request, CancellationToken.None);
        var reevaluated = await svc.EvaluateTripReadinessAsync(request, CancellationToken.None);

        Assert.True(result.IsReady);
        Assert.True(reevaluated.IsReady);
        Assert.Equal(1, result.TotalRequirements);
        Assert.Equal(1, result.MetRequirements);

        var outbox = db.OutboxMessages.ToList();
        Assert.Single(outbox);
        Assert.Equal("Compliance.ComplianceReady", outbox[0].EventType);
    }
}
