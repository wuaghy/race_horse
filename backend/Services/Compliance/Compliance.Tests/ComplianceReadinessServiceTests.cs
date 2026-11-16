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

    [Fact]
    public async Task EvaluateDispatchTripReadiness_RequiresRequestDocumentAndRouteLegClearance()
    {
        using var db = TestDbContextFactory.Create();
        var service = new ComplianceReadinessService(db, NullLogger<ComplianceReadinessService>.Instance);
        var tripId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var horseId = Guid.NewGuid();
        var routeLegId = Guid.NewGuid();
        var originCountryId = Guid.NewGuid();
        var destinationCountryId = Guid.NewGuid();
        var departureAt = DateTime.UtcNow.AddDays(1);
        var targetDate = DateOnly.FromDateTime(departureAt);
        var documentType = new DocumentType
        {
            Code = "IMPORT_PERMIT",
            Name = "Import permit",
            Scope = DocumentScope.Request,
            Active = true
        };
        var rule = new RegulationRule
        {
            RuleCode = "IMPORT_CLEARANCE",
            OriginCountryId = originCountryId,
            DestinationCountryId = destinationCountryId,
            Title = "Import requirements",
            CustomsRequired = true,
            EffectiveFrom = targetDate.AddDays(-1),
            Active = true,
            Requirements =
            [
                new RegulationDocumentRequirement
                {
                    DocumentTypeId = documentType.Id,
                    DocumentType = documentType,
                    Required = true
                }
            ]
        };
        db.RegulationRules.Add(rule);
        db.Documents.Add(new Document
        {
            DocumentTypeId = documentType.Id,
            DocumentType = documentType,
            RequestId = requestId,
            UploadedByUserId = Guid.NewGuid(),
            DocumentNo = "PERMIT-1",
            FileName = "permit.pdf",
            FileUrl = "local/permit.pdf",
            Status = DocumentStatus.Approved,
            ExpiryDate = targetDate.AddDays(30)
        });
        await db.SaveChangesAsync();

        var request = new EvaluateDispatchTripReadinessRequest(
            tripId,
            [requestId],
            [horseId],
            [new ComplianceRouteLegContext(routeLegId, originCountryId, destinationCountryId, departureAt, null)]);

        var missingClearance = await service.EvaluateDispatchTripReadinessAsync(request);

        Assert.False(missingClearance.IsReady);
        Assert.Contains(missingClearance.Blockers, blocker => blocker.Contains("required CUSTOMS clearance is MISSING", StringComparison.Ordinal));
        Assert.Single(missingClearance.Requirements);
        Assert.True(missingClearance.Requirements[0].IsMet);
        Assert.Empty(db.OutboxMessages);

        db.ClearanceCases.Add(new ClearanceCase
        {
            CaseNo = "CLR-TEST-1",
            TripId = tripId,
            RouteLegId = routeLegId,
            CountryId = destinationCountryId,
            ClearanceType = ClearanceTypes.Customs,
            AuthorityName = "Customs office",
            Status = ClearanceStatus.Approved
        });
        await db.SaveChangesAsync();

        var ready = await service.EvaluateDispatchTripReadinessAsync(request);

        Assert.True(ready.IsReady);
        Assert.Empty(ready.Blockers);
        Assert.Single(ready.Clearances);
        Assert.True(ready.Clearances[0].IsMet);
        var outbox = db.OutboxMessages.ToList();
        Assert.Single(outbox);
        Assert.Equal("Compliance.ComplianceReady", outbox[0].EventType);
    }
}
