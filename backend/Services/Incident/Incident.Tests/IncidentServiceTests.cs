using System.Text;
using FluentValidation;
using Incident.Application.Abstractions;
using Incident.Application.DTOs;
using Incident.Application.Validators;
using Incident.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Incident.Tests;

public class IncidentServiceTests
{
    private class FakeFileStorage : IFileStorageService
    {
        public Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default)
        {
            return Task.FromResult($"/uploads/{Guid.NewGuid()}_{fileName}");
        }

        public Task<Stream?> DownloadFileAsync(string fileUrl, CancellationToken cancellationToken = default)
        {
            Stream stream = new MemoryStream(Encoding.UTF8.GetBytes("fake content"));
            return Task.FromResult<Stream?>(stream);
        }

        public Task DeleteFileAsync(string fileUrl, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task CreateIncidentAsync_WithValidData_CreatesIncidentAndOutboxMessage()
    {
        // Arrange
        var db = TestDbContextFactory.CreateInMemoryDbContext(nameof(CreateIncidentAsync_WithValidData_CreatesIncidentAndOutboxMessage));
        var service = new IncidentService(
            db,
            new FakeFileStorage(),
            new CreateIncidentRequestValidator(),
            new UpdateIncidentStatusRequestValidator(),
            new ResolveIncidentRequestValidator(),
            NullLogger<IncidentService>.Instance);

        var request = new CreateIncidentRequest(
            TripId: Guid.NewGuid(),
            RouteLegId: Guid.NewGuid(),
            CheckpointId: Guid.NewGuid(),
            HorseId: Guid.NewGuid(),
            Type: "VEHICLE_BREAKDOWN",
            Severity: "HIGH",
            Title: "Flat Tire on Highway",
            Description: "Transport vehicle rear tire puncture near checkpoint 2",
            OccurredAt: DateTime.UtcNow,
            Latitude: 13.7563m,
            Longitude: 100.5018m);

        var reporterId = Guid.NewGuid();

        // Act
        var result = await service.CreateIncidentAsync(request, reporterId);

        // Assert
        Assert.NotNull(result);
        Assert.StartsWith("INC-", result.IncidentNo);
        Assert.Equal("OPEN", result.Status);
        Assert.Equal("HIGH", result.Severity);
        Assert.Equal("Flat Tire on Highway", result.Title);

        var savedIncident = await db.Incidents.FirstOrDefaultAsync(i => i.Id == result.Id);
        Assert.NotNull(savedIncident);
        Assert.Equal(reporterId, savedIncident.ReportedByUserId);

        var outbox = await db.OutboxMessages.FirstOrDefaultAsync(o => o.AggregateId == result.Id);
        Assert.NotNull(outbox);
        Assert.Equal("Incident.IncidentCreated", outbox.EventType);
        Assert.Equal("PENDING", outbox.Status);

        var audit = await db.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == result.Id && a.Action == "CREATE");
        Assert.NotNull(audit);
        Assert.Equal("OPEN", audit.NewState);
    }

    [Fact]
    public async Task CreateIncidentAsync_WithInvalidSeverity_ThrowsValidationException()
    {
        // Arrange
        var db = TestDbContextFactory.CreateInMemoryDbContext(nameof(CreateIncidentAsync_WithInvalidSeverity_ThrowsValidationException));
        var service = new IncidentService(
            db,
            new FakeFileStorage(),
            new CreateIncidentRequestValidator(),
            new UpdateIncidentStatusRequestValidator(),
            new ResolveIncidentRequestValidator(),
            NullLogger<IncidentService>.Instance);

        var request = new CreateIncidentRequest(
            TripId: Guid.NewGuid(),
            RouteLegId: null,
            CheckpointId: null,
            HorseId: null,
            Type: "VEHICLE_BREAKDOWN",
            Severity: "SUPER_CRITICAL", // Invalid
            Title: "Test",
            Description: "Test Description",
            OccurredAt: DateTime.UtcNow);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateIncidentAsync(request, Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdateStatusAsync_ValidTransition_UpdatesStatusAndAuditLog()
    {
        // Arrange
        var db = TestDbContextFactory.CreateInMemoryDbContext(nameof(UpdateStatusAsync_ValidTransition_UpdatesStatusAndAuditLog));
        var service = new IncidentService(
            db,
            new FakeFileStorage(),
            new CreateIncidentRequestValidator(),
            new UpdateIncidentStatusRequestValidator(),
            new ResolveIncidentRequestValidator(),
            NullLogger<IncidentService>.Instance);

        var createReq = new CreateIncidentRequest(
            TripId: Guid.NewGuid(),
            RouteLegId: null,
            CheckpointId: null,
            HorseId: null,
            Type: "WEATHER",
            Severity: "MEDIUM",
            Title: "Storm Delay",
            Description: "Heavy rain causing slow driving",
            OccurredAt: DateTime.UtcNow);

        var incident = await service.CreateIncidentAsync(createReq, Guid.NewGuid());
        var investigatorId = Guid.NewGuid();

        // Act: OPEN -> INVESTIGATING
        var updated = await service.UpdateStatusAsync(
            incident.Id,
            new UpdateIncidentStatusRequest("INVESTIGATING", "Assigned coordinator investigating detour"),
            investigatorId);

        // Assert
        Assert.Equal("INVESTIGATING", updated.Status);
        Assert.Equal(2, updated.VersionNo);

        var audit = await db.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == incident.Id && a.Action == "STATUS_CHANGE");
        Assert.NotNull(audit);
        Assert.Equal("OPEN", audit.OldState);
        Assert.Equal("INVESTIGATING", audit.NewState);
        Assert.Equal(investigatorId, audit.ActorUserId);
    }

    [Fact]
    public async Task UpdateStatusAsync_InvalidTransition_ThrowsInvalidOperationException()
    {
        // Arrange
        var db = TestDbContextFactory.CreateInMemoryDbContext(nameof(UpdateStatusAsync_InvalidTransition_ThrowsInvalidOperationException));
        var service = new IncidentService(
            db,
            new FakeFileStorage(),
            new CreateIncidentRequestValidator(),
            new UpdateIncidentStatusRequestValidator(),
            new ResolveIncidentRequestValidator(),
            NullLogger<IncidentService>.Instance);

        var createReq = new CreateIncidentRequest(
            TripId: Guid.NewGuid(),
            RouteLegId: null,
            CheckpointId: null,
            HorseId: null,
            Type: "OTHER",
            Severity: "LOW",
            Title: "Minor Delay",
            Description: "Traffic jam",
            OccurredAt: DateTime.UtcNow);

        var incident = await service.CreateIncidentAsync(createReq, Guid.NewGuid());

        // Act & Assert: OPEN -> CLOSED (Illegal without RESOLVED)
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateStatusAsync(incident.Id, new UpdateIncidentStatusRequest("CLOSED"), Guid.NewGuid()));
    }

    [Fact]
    public async Task ResolveIncidentAsync_SetsResolvedAtAndEnqueuesOutboxEvent()
    {
        // Arrange
        var db = TestDbContextFactory.CreateInMemoryDbContext(nameof(ResolveIncidentAsync_SetsResolvedAtAndEnqueuesOutboxEvent));
        var service = new IncidentService(
            db,
            new FakeFileStorage(),
            new CreateIncidentRequestValidator(),
            new UpdateIncidentStatusRequestValidator(),
            new ResolveIncidentRequestValidator(),
            NullLogger<IncidentService>.Instance);

        var createReq = new CreateIncidentRequest(
            TripId: Guid.NewGuid(),
            RouteLegId: null,
            CheckpointId: null,
            HorseId: null,
            Type: "HORSE_MEDICAL",
            Severity: "HIGH",
            Title: "Horse Elevated Heart Rate",
            Description: "Heart rate exceeded 65 bpm",
            OccurredAt: DateTime.UtcNow);

        var incident = await service.CreateIncidentAsync(createReq, Guid.NewGuid());
        var resolverId = Guid.NewGuid();

        // Act
        var resolved = await service.ResolveIncidentAsync(
            incident.Id,
            new ResolveIncidentRequest("Administered hydration and rest for 30 minutes. Heart rate returned to normal."),
            resolverId);

        // Assert
        Assert.Equal("RESOLVED", resolved.Status);
        Assert.NotNull(resolved.ResolvedAt);
        Assert.Contains("Administered hydration", resolved.ResolutionNote);

        var resolveOutbox = await db.OutboxMessages.FirstOrDefaultAsync(o => o.AggregateId == incident.Id && o.EventType == "Incident.IncidentResolved");
        Assert.NotNull(resolveOutbox);
        Assert.Equal("PENDING", resolveOutbox.Status);
    }

    [Fact]
    public async Task AddAttachmentAsync_ValidFile_SavesAttachmentMetadata()
    {
        // Arrange
        var db = TestDbContextFactory.CreateInMemoryDbContext(nameof(AddAttachmentAsync_ValidFile_SavesAttachmentMetadata));
        var service = new IncidentService(
            db,
            new FakeFileStorage(),
            new CreateIncidentRequestValidator(),
            new UpdateIncidentStatusRequestValidator(),
            new ResolveIncidentRequestValidator(),
            NullLogger<IncidentService>.Instance);

        var createReq = new CreateIncidentRequest(
            TripId: Guid.NewGuid(),
            RouteLegId: null,
            CheckpointId: null,
            HorseId: null,
            Type: "VEHICLE_BREAKDOWN",
            Severity: "MEDIUM",
            Title: "Engine warning light",
            Description: "Check engine on dashboard",
            OccurredAt: DateTime.UtcNow);

        var incident = await service.CreateIncidentAsync(createReq, Guid.NewGuid());
        var uploaderId = Guid.NewGuid();

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("fake-image-bytes"));

        // Act
        var attachment = await service.AddAttachmentAsync(
            incident.Id,
            "dashboard_warning.jpg",
            "image/jpeg",
            stream,
            uploaderId);

        // Assert
        Assert.NotNull(attachment);
        Assert.Equal("dashboard_warning.jpg", attachment.FileName);
        Assert.StartsWith("/uploads/", attachment.FileUrl);

        var updatedIncident = await service.GetIncidentByIdAsync(incident.Id);
        Assert.NotNull(updatedIncident);
        Assert.Single(updatedIncident.Attachments);
    }

    [Fact]
    public async Task GetIncidentsAsync_FilteringByTripAndSeverity_ReturnsFilteredResults()
    {
        // Arrange
        var db = TestDbContextFactory.CreateInMemoryDbContext(nameof(GetIncidentsAsync_FilteringByTripAndSeverity_ReturnsFilteredResults));
        var service = new IncidentService(
            db,
            new FakeFileStorage(),
            new CreateIncidentRequestValidator(),
            new UpdateIncidentStatusRequestValidator(),
            new ResolveIncidentRequestValidator(),
            NullLogger<IncidentService>.Instance);

        var tripId1 = Guid.NewGuid();
        var tripId2 = Guid.NewGuid();

        await service.CreateIncidentAsync(new CreateIncidentRequest(tripId1, null, null, null, "WEATHER", "LOW", "Rain 1", "Desc", DateTime.UtcNow), Guid.NewGuid());
        await service.CreateIncidentAsync(new CreateIncidentRequest(tripId1, null, null, null, "WEATHER", "HIGH", "Storm 1", "Desc", DateTime.UtcNow), Guid.NewGuid());
        await service.CreateIncidentAsync(new CreateIncidentRequest(tripId2, null, null, null, "BORDER_ISSUE", "HIGH", "Border delay", "Desc", DateTime.UtcNow), Guid.NewGuid());

        // Act
        var resultTrip1High = await service.GetIncidentsAsync(new IncidentFilterDto(TripId: tripId1, Severity: "HIGH"));

        // Assert
        Assert.Single(resultTrip1High.Items);
        Assert.Equal("Storm 1", resultTrip1High.Items[0].Title);
    }
}
