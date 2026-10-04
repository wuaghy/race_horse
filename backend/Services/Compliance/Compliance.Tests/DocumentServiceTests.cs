using Compliance.Application.DTOs.Documents;
using Compliance.Infrastructure.Services;
using Compliance.Application.DTOs.MasterData;
using Xunit;

namespace Compliance.Tests;

public class DocumentServiceTests
{
    [Fact]
    public async Task UploadDocument_CreatesDocumentWithPendingStatus()
    {
        using var db = TestDbContextFactory.Create();
        var masterSvc = new ComplianceMasterDataService(db);
        var svc = new DocumentService(db);

        var docType = await masterSvc.CreateDocumentTypeAsync(
            new CreateDocumentTypeRequest("PASSPORT", "Passport", "Horse"),
            CancellationToken.None);

        var horseId = Guid.NewGuid();
        var request = new UploadDocumentRequest(
            DocumentTypeId: docType.Id,
            HorseId: horseId,
            RequestId: null,
            TripId: null,
            FileName: "passport.pdf",
            FileUrl: "https://storage/passport.pdf",
            FileSize: 1024,
            MimeType: "application/pdf",
            IssueDate: DateOnly.FromDateTime(DateTime.UtcNow),
            ExpiryDate: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)),
            IssuingAuthority: "Customs");

        var userId = Guid.NewGuid();
        var result = await svc.UploadDocumentAsync(userId, request, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("UPLOADED", result.Status);
        Assert.Equal("passport.pdf", result.FileName);
        Assert.Equal(horseId, result.HorseId);
        Assert.Equal(docType.Id, result.DocumentTypeId);
    }

    [Fact]
    public async Task GetDocuments_FiltersByHorseId()
    {
        using var db = TestDbContextFactory.Create();
        var masterSvc = new ComplianceMasterDataService(db);
        var svc = new DocumentService(db);

        var docType = await masterSvc.CreateDocumentTypeAsync(
            new CreateDocumentTypeRequest("CERT", "Cert", "Horse"),
            CancellationToken.None);

        var horse1 = Guid.NewGuid();
        var horse2 = Guid.NewGuid();

        await svc.UploadDocumentAsync(Guid.NewGuid(),
            new UploadDocumentRequest(docType.Id, horse1, null, null, "a.pdf", "url/a", 100, "application/pdf", null, null, null),
            CancellationToken.None);

        await svc.UploadDocumentAsync(Guid.NewGuid(),
            new UploadDocumentRequest(docType.Id, horse2, null, null, "b.pdf", "url/b", 200, "application/pdf", null, null, null),
            CancellationToken.None);

        var filter = new DocumentFilter(horse1, null, null, null, null);
        var results = await svc.GetDocumentsAsync(filter, CancellationToken.None);

        Assert.Single(results);
        Assert.Equal(horse1, results[0].HorseId);
    }

    [Fact]
    public async Task GetDocumentById_NotFound_ReturnsNull()
    {
        using var db = TestDbContextFactory.Create();
        var svc = new DocumentService(db);

        var result = await svc.GetDocumentByIdAsync(Guid.NewGuid(), CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task ReviewDocument_ApproveSetsStatusAndCreatesOutbox()
    {
        using var db = TestDbContextFactory.Create();
        var masterSvc = new ComplianceMasterDataService(db);
        var svc = new DocumentService(db);

        var docType = await masterSvc.CreateDocumentTypeAsync(
            new CreateDocumentTypeRequest("VET", "Vet Cert", "Horse"),
            CancellationToken.None);

        var doc = await svc.UploadDocumentAsync(Guid.NewGuid(),
            new UploadDocumentRequest(docType.Id, Guid.NewGuid(), null, null, "vet.pdf", "url/vet", 500, "application/pdf", null, null, null),
            CancellationToken.None);

        var reviewerId = Guid.NewGuid();
        var result = await svc.ReviewDocumentAsync(
            doc.Id, reviewerId,
            new ReviewDocumentRequest("Approved", "Looks good"),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("APPROVED", result!.Status);

        // Verify outbox message was created
        var outboxMessages = db.OutboxMessages.ToList();
        Assert.Single(outboxMessages);
        Assert.Equal("Compliance.DocumentApproved", outboxMessages[0].EventType);
    }

    [Fact]
    public async Task ReviewDocument_Reject_SetsRejectedStatus()
    {
        using var db = TestDbContextFactory.Create();
        var masterSvc = new ComplianceMasterDataService(db);
        var svc = new DocumentService(db);

        var docType = await masterSvc.CreateDocumentTypeAsync(
            new CreateDocumentTypeRequest("IMP", "Import Permit", "Trip"),
            CancellationToken.None);

        var doc = await svc.UploadDocumentAsync(Guid.NewGuid(),
            new UploadDocumentRequest(docType.Id, null, null, Guid.NewGuid(), "permit.pdf", "url/permit", 300, "application/pdf", null, null, null),
            CancellationToken.None);

        var result = await svc.ReviewDocumentAsync(
            doc.Id, Guid.NewGuid(),
            new ReviewDocumentRequest("Rejected", "Missing signature"),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("REJECTED", result!.Status);
    }
}
